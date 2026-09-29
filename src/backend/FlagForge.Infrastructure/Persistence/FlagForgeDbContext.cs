using FlagForge.Application.Common;
using FlagForge.Domain;
using Microsoft.EntityFrameworkCore;

namespace FlagForge.Infrastructure.Persistence;

public sealed class FlagForgeDbContext(DbContextOptions<FlagForgeDbContext> options)
    : DbContext(options), IFlagForgeDbContext
{
    public DbSet<User> Users => Set<User>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<Project> Projects => Set<Project>();

    public DbSet<ProjectEnvironment> Environments => Set<ProjectEnvironment>();

    public DbSet<SdkKey> SdkKeys => Set<SdkKey>();

    public DbSet<Flag> Flags => Set<Flag>();

    public DbSet<FlagEnvironmentConfig> FlagEnvironmentConfigs => Set<FlagEnvironmentConfig>();

    public DbSet<ScheduledChange> ScheduledChanges => Set<ScheduledChange>();

    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    public DbSet<FlagUsageHourly> FlagUsageHourly => Set<FlagUsageHourly>();

    /// <summary>Applies the provider settings shared by the apps, the migrator, and design-time tooling.</summary>
    public static void Configure(DbContextOptionsBuilder builder, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.UseSqlServer(connectionString, sql =>
        {
            // Azure SQL serverless resumes from auto-pause with transient errors, so retries matter in production.
            sql.EnableRetryOnFailure(maxRetryCount: 6, maxRetryDelay: TimeSpan.FromSeconds(10), errorNumbersToAdd: null);

            // Pin SQL Server 2022 behaviour (JSON stays in nvarchar(max)) for both the container and Azure SQL.
            sql.UseCompatibilityLevel(160);
        });
    }

    public async Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (Database.CurrentTransaction is not null)
        {
            return await operation(cancellationToken);
        }

        var attempt = 0;
        var strategy = Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(
            operation,
            async (_, op, token) =>
            {
                // A retried attempt must not see entities modified by the failed attempt.
                if (attempt++ > 0)
                {
                    ChangeTracker.Clear();
                }

                await using var transaction = await Database.BeginTransactionAsync(token);
                var result = await op(token);
                await transaction.CommitAsync(token);
                return result;
            },
            verifySucceeded: null,
            cancellationToken);
    }

    public async Task<long> IncrementConfigVersionAsync(Guid environmentId, CancellationToken cancellationToken)
    {
        var versions = await Database
            .SqlQuery<long>($"UPDATE Environments SET ConfigVersion = ConfigVersion + 1 OUTPUT inserted.ConfigVersion AS Value WHERE Id = {environmentId}")
            .ToListAsync(cancellationToken);
        return versions.Count == 1
            ? versions[0]
            : throw new InvalidOperationException($"Environment {environmentId} does not exist.");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FlagForgeDbContext).Assembly);
}
