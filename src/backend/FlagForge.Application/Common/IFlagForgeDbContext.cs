using FlagForge.Domain;
using Microsoft.EntityFrameworkCore;

namespace FlagForge.Application.Common;

/// <summary>The application's unit of work over the FlagForge database.</summary>
public interface IFlagForgeDbContext
{
    DbSet<User> Users { get; }

    DbSet<RefreshToken> RefreshTokens { get; }

    DbSet<Project> Projects { get; }

    DbSet<ProjectEnvironment> Environments { get; }

    DbSet<SdkKey> SdkKeys { get; }

    DbSet<Flag> Flags { get; }

    DbSet<FlagEnvironmentConfig> FlagEnvironmentConfigs { get; }

    DbSet<ScheduledChange> ScheduledChanges { get; }

    DbSet<AuditEntry> AuditEntries { get; }

    DbSet<FlagUsageHourly> FlagUsageHourly { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs <paramref name="operation"/> in a database transaction under the connection-resiliency strategy, retrying
    /// the whole operation on transient failures. Joins the current transaction when one is already open.
    /// </summary>
    Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken);

    /// <summary>
    /// Atomically increments an environment's <c>ConfigVersion</c> inside the current transaction and returns the new
    /// value (<c>UPDATE ... SET ConfigVersion = ConfigVersion + 1 OUTPUT inserted.ConfigVersion</c>).
    /// </summary>
    Task<long> IncrementConfigVersionAsync(Guid environmentId, CancellationToken cancellationToken);
}
