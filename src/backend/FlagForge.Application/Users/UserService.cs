using FlagForge.Application.Common;
using FlagForge.Domain;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FlagForge.Application.Users;

/// <summary>User administration (Admin only). The last active admin can never be demoted or deactivated.</summary>
public sealed class UserService(
    IFlagForgeDbContext db,
    IPasswordHasher<User> passwordHasher,
    IAuditWriter audit,
    TimeProvider timeProvider,
    IValidator<UserListQuery> listValidator,
    IValidator<CreateUserRequest> createValidator,
    IValidator<UpdateUserRequest> updateValidator)
{
    public async Task<PagedResult<UserResponse>> ListAsync(UserListQuery query, CancellationToken cancellationToken)
    {
        await listValidator.EnsureValidAsync(query, cancellationToken);
        var users = db.Users.AsNoTracking();
        var total = await users.CountAsync(cancellationToken);
        var page = await users
            .OrderBy(u => u.Email)
            .Page(query.Page, query.PageSize)
            .ToListAsync(cancellationToken);
        return new PagedResult<UserResponse>([.. page.Select(UserResponse.From)], query.Page, query.PageSize, total);
    }

    public async Task<UserResponse> GetAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new NotFoundException("That user does not exist.");
        return UserResponse.From(user);
    }

    public async Task<CreateUserResponse> CreateAsync(CreateUserRequest request, Actor actor, CancellationToken cancellationToken)
    {
        await createValidator.EnsureValidAsync(request, cancellationToken);
        var email = User.NormalizeEmail(request.Email);
        if (await db.Users.AnyAsync(u => u.Email == email, cancellationToken))
        {
            throw new ConflictException($"A user with the email {email} already exists.");
        }

        var now = timeProvider.GetUtcNow();
        var temporaryPassword = Generate.TemporaryPassword();
        var user = new User
        {
            Id = Guid.CreateVersion7(now),
            Email = email,
            DisplayName = request.DisplayName.Trim(),
            Role = request.Role,
            MustChangePassword = true,
            CreatedAt = now,
        };
        user.PasswordHash = passwordHasher.HashPassword(user, temporaryPassword);
        db.Users.Add(user);
        audit.Record(actor, AuditActions.UserCreated, AuditTarget.For(user), after: Snapshot(user));
        await db.SaveChangesAsync(cancellationToken);
        return new CreateUserResponse(UserResponse.From(user), temporaryPassword);
    }

    public async Task<UserResponse> UpdateAsync(Guid userId, UpdateUserRequest request, Actor actor, CancellationToken cancellationToken)
    {
        await updateValidator.EnsureValidAsync(request, cancellationToken);
        return await db.ExecuteInTransactionAsync(
            async token =>
            {
                var user = await GetUserAsync(userId, token);
                var before = Snapshot(user);
                var losesAdmin = user.Role == Role.Admin && user.IsActive
                    && ((request.Role is { } role && role != Role.Admin) || request.IsActive == false);
                if (losesAdmin && !await db.Users.AnyAsync(u => u.Id != user.Id && u.Role == Role.Admin && u.IsActive, token))
                {
                    throw new ConflictException("FlagForge needs at least one active admin. Promote another user to admin first.");
                }

                var deactivated = user.IsActive && request.IsActive == false;
                user.DisplayName = request.DisplayName?.Trim() ?? user.DisplayName;
                user.Role = request.Role ?? user.Role;
                user.IsActive = request.IsActive ?? user.IsActive;
                if (deactivated)
                {
                    await RevokeSessionsAsync(user.Id, token);
                }

                audit.Record(actor, deactivated ? AuditActions.UserDeactivated : AuditActions.UserUpdated, AuditTarget.For(user), before: before, after: Snapshot(user));
                await db.SaveChangesAsync(token);
                return UserResponse.From(user);
            },
            cancellationToken);
    }

    public async Task<ResetPasswordResponse> ResetPasswordAsync(Guid userId, Actor actor, CancellationToken cancellationToken)
    {
        return await db.ExecuteInTransactionAsync(
            async token =>
            {
                var user = await GetUserAsync(userId, token);
                var temporaryPassword = Generate.TemporaryPassword();
                user.PasswordHash = passwordHasher.HashPassword(user, temporaryPassword);
                user.MustChangePassword = true;
                user.FailedLoginCount = 0;
                user.LockoutEndsAt = null;
                await RevokeSessionsAsync(user.Id, token);
                audit.Record(actor, AuditActions.UserPasswordReset, AuditTarget.For(user));
                await db.SaveChangesAsync(token);
                return new ResetPasswordResponse(temporaryPassword);
            },
            cancellationToken);
    }

    private async Task<User> GetUserAsync(Guid userId, CancellationToken cancellationToken) =>
        await db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
        ?? throw new NotFoundException("That user does not exist.");

    private async Task RevokeSessionsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        await db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), cancellationToken);
    }

    private static object Snapshot(User user) => new { user.Email, user.DisplayName, user.Role, user.IsActive };
}
