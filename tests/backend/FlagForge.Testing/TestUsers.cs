using FlagForge.Domain;
using Microsoft.AspNetCore.Identity;

namespace FlagForge.Testing;

public static class TestUsers
{
    // Hashing is deliberately slow, so every test user shares one precomputed hash of TestAuth.Password.
    private static readonly Lazy<string> PasswordHash = new(() =>
        new PasswordHasher<User>().HashPassword(new User { Email = "hash@test", DisplayName = "hash" }, TestAuth.Password));

    /// <summary>Inserts an active user directly into the database.</summary>
    public static async Task<User> CreateAsync(this FlagForgeFixture fixture, Role role, string? email = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        var now = fixture.Time.GetUtcNow();
        var user = new User
        {
            Id = Guid.CreateVersion7(now),
            Email = email ?? $"{role.ToString().ToLowerInvariant()}-{Guid.NewGuid():N}@test.local",
            DisplayName = $"Test {role}",
            PasswordHash = PasswordHash.Value,
            Role = role,
            CreatedAt = now,
        };
        await using var db = fixture.CreateDbContext();
        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);
        return user;
    }
}
