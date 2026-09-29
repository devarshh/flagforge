using FlagForge.Application.Common;
using FlagForge.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FlagForge.Migrator.Seeding;

/// <summary>Creates the first administrator when the database has no users.</summary>
internal sealed partial class AdminSeeder(
    IFlagForgeDbContext db,
    IPasswordHasher<User> passwordHasher,
    IOptions<SeedOptions> options,
    TimeProvider timeProvider,
    ILogger<AdminSeeder> logger)
{
    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        if (await db.Users.AnyAsync(cancellationToken))
        {
            LogSkipped(logger);
            return;
        }

        var seed = options.Value;
        if (string.IsNullOrWhiteSpace(seed.AdminEmail) || string.IsNullOrEmpty(seed.AdminPassword))
        {
            throw new InvalidOperationException(
                "The database has no users. Set FF_SEED_ADMIN_EMAIL and FF_SEED_ADMIN_PASSWORD to create the first administrator.");
        }

        var now = timeProvider.GetUtcNow();
        var admin = new User
        {
            Id = Guid.CreateVersion7(now),
            Email = User.NormalizeEmail(seed.AdminEmail),
            DisplayName = "Administrator",
            Role = Role.Admin,
            CreatedAt = now,
        };
        admin.PasswordHash = passwordHasher.HashPassword(admin, seed.AdminPassword);
        db.Users.Add(admin);
        await db.SaveChangesAsync(cancellationToken);
        LogCreated(logger, admin.Email);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Users already exist; skipping administrator seed")]
    private static partial void LogSkipped(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Created administrator {Email}")]
    private static partial void LogCreated(ILogger logger, string email);
}
