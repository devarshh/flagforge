using FlagForge.Application.Common;
using FlagForge.Application.Users;
using FlagForge.Domain;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FlagForge.Application.Auth;

/// <summary>
/// Password sign-in with lockout, and rotating refresh tokens with reuse detection: presenting a token that was
/// already rotated revokes every session of that user, because only a stolen copy could be replayed.
/// </summary>
public sealed class AuthService(
    IFlagForgeDbContext db,
    IPasswordHasher<User> passwordHasher,
    IAccessTokenIssuer tokenIssuer,
    IOptions<AuthOptions> options,
    TimeProvider timeProvider,
    IValidator<LoginRequest> loginValidator,
    IValidator<ChangePasswordRequest> changePasswordValidator)
{
    public const int MaxFailedLogins = 5;
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    // One message for unknown emails, wrong passwords, locked and inactive accounts, so responses reveal nothing.
    private const string InvalidCredentials =
        "Email or password is incorrect. After 5 failed attempts, sign-in is paused for 15 minutes.";

    private const string SessionEnded = "Your session has ended. Sign in again.";

    private static readonly User DummyUser = new() { Email = "nobody@invalid", DisplayName = "nobody" };

    // Verified against unknown emails so they take as long as real ones.
    private static readonly Lazy<string> DummyPasswordHash = new(() => new PasswordHasher<User>().HashPassword(DummyUser, Generate.TemporaryPassword()));

    public async Task<AuthSession> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        await loginValidator.EnsureValidAsync(request, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var email = User.NormalizeEmail(request.Email);
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
        if (user is null)
        {
            passwordHasher.VerifyHashedPassword(DummyUser, DummyPasswordHash.Value, request.Password);
            throw new UnauthorizedException(InvalidCredentials);
        }

        var verification = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (!user.IsActive || user.LockoutEndsAt > now)
        {
            throw new UnauthorizedException(InvalidCredentials);
        }

        if (verification == PasswordVerificationResult.Failed)
        {
            user.FailedLoginCount++;
            if (user.FailedLoginCount >= MaxFailedLogins)
            {
                user.LockoutEndsAt = now + LockoutDuration;
                user.FailedLoginCount = 0;
            }

            await db.SaveChangesAsync(cancellationToken);
            throw new UnauthorizedException(InvalidCredentials);
        }

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
        }

        user.FailedLoginCount = 0;
        user.LockoutEndsAt = null;
        user.LastLoginAt = now;
        var session = StartSession(user, now);
        await db.SaveChangesAsync(cancellationToken);
        return session;
    }

    public async Task<AuthSession> RefreshAsync(string? refreshToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(refreshToken))
        {
            throw new UnauthorizedException(SessionEnded);
        }

        var now = timeProvider.GetUtcNow();
        var tokenHash = Hashing.Sha256Hex(refreshToken);
        var stored = await db.RefreshTokens
            .AsNoTracking()
            .Where(t => t.TokenHash == tokenHash)
            .Select(t => new { t.Id, t.UserId, t.RevokedAt, t.ExpiresAt })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new UnauthorizedException(SessionEnded);

        if (stored.RevokedAt is not null)
        {
            await RevokeAllSessionsAsync(stored.UserId, now, cancellationToken);
            throw new UnauthorizedException(SessionEnded);
        }

        if (stored.ExpiresAt <= now)
        {
            throw new UnauthorizedException(SessionEnded);
        }

        var session = await db.ExecuteInTransactionAsync<AuthSession?>(
            async token =>
            {
                var user = await db.Users.FirstAsync(u => u.Id == stored.UserId, token);
                if (!user.IsActive)
                {
                    return null;
                }

                var next = StartSession(user, now);
                var nextHash = Hashing.Sha256Hex(next.RefreshToken);

                // Conditional update: of two concurrent refreshes with the same token, exactly one rotates it.
                var rotated = await db.RefreshTokens
                    .Where(t => t.Id == stored.Id && t.RevokedAt == null)
                    .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now).SetProperty(t => t.ReplacedByTokenHash, nextHash), token);
                if (rotated == 0)
                {
                    return null;
                }

                await db.SaveChangesAsync(token);
                return next;
            },
            cancellationToken);

        return session ?? throw new UnauthorizedException(SessionEnded);
    }

    public async Task LogoutAsync(string? refreshToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(refreshToken))
        {
            return;
        }

        var now = timeProvider.GetUtcNow();
        var tokenHash = Hashing.Sha256Hex(refreshToken);
        await db.RefreshTokens
            .Where(t => t.TokenHash == tokenHash && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), cancellationToken);
    }

    public async Task<UserResponse> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId && u.IsActive, cancellationToken)
            ?? throw new UnauthorizedException(SessionEnded);
        return UserResponse.From(user);
    }

    /// <summary>Changes the password, clears <c>MustChangePassword</c>, and ends every other session.</summary>
    public async Task<UserResponse> ChangePasswordAsync(
        Guid userId, ChangePasswordRequest request, string? currentRefreshToken, CancellationToken cancellationToken)
    {
        await changePasswordValidator.EnsureValidAsync(request, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var keep = currentRefreshToken is null ? null : Hashing.Sha256Hex(currentRefreshToken);
        return await db.ExecuteInTransactionAsync(
            async token =>
            {
                var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId && u.IsActive, token)
                    ?? throw new UnauthorizedException(SessionEnded);
                if (passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.CurrentPassword) == PasswordVerificationResult.Failed)
                {
                    throw RequestValidationException.For("currentPassword", "Your current password is incorrect.");
                }

                user.PasswordHash = passwordHasher.HashPassword(user, request.NewPassword);
                user.MustChangePassword = false;
                await db.RefreshTokens
                    .Where(t => t.UserId == userId && t.RevokedAt == null && t.TokenHash != keep)
                    .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), token);
                await db.SaveChangesAsync(token);
                return UserResponse.From(user);
            },
            cancellationToken);
    }

    private AuthSession StartSession(User user, DateTimeOffset now)
    {
        var accessToken = tokenIssuer.Issue(user);
        var refreshToken = Generate.RefreshToken();
        var expiresAt = now.AddDays(options.Value.RefreshTokenDays);
        db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.CreateVersion7(now),
            UserId = user.Id,
            TokenHash = Hashing.Sha256Hex(refreshToken),
            ExpiresAt = expiresAt,
            CreatedAt = now,
        });
        return new AuthSession(new LoginResponse(accessToken.Token, accessToken.ExpiresAt, UserResponse.From(user)), refreshToken, expiresAt);
    }

    private Task<int> RevokeAllSessionsAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken) =>
        db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), cancellationToken);
}
