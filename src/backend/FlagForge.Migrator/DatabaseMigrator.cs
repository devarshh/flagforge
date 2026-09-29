using FlagForge.Infrastructure;
using FlagForge.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FlagForge.Migrator;

internal sealed partial class DatabaseMigrator(
    FlagForgeDbContext db,
    IOptions<DatabaseOptions> databaseOptions,
    TimeProvider timeProvider,
    ILogger<DatabaseMigrator> logger)
{
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(3);

    public async Task MigrateAsync(CancellationToken cancellationToken)
    {
        await WaitForServerAsync(cancellationToken);

        var pending = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
        if (pending.Count == 0)
        {
            LogUpToDate(logger);
            return;
        }

        LogApplying(logger, pending.Count, pending);
        await db.Database.MigrateAsync(cancellationToken);
        LogApplied(logger);
    }

    /// <summary>
    /// SQL Server containers accept connections well after they start, so probe the server (via master, because the
    /// application database may not exist yet) for up to 60 seconds.
    /// </summary>
    private async Task WaitForServerAsync(CancellationToken cancellationToken)
    {
        var probe = new SqlConnectionStringBuilder(databaseOptions.Value.ConnectionString) { InitialCatalog = "master", ConnectTimeout = 5 };
        var deadline = timeProvider.GetUtcNow() + ConnectTimeout;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await using var connection = new SqlConnection(probe.ConnectionString);
                await connection.OpenAsync(cancellationToken);
                LogConnected(logger, attempt);
                return;
            }
            catch (SqlException ex) when (timeProvider.GetUtcNow() < deadline)
            {
                LogWaiting(logger, attempt, ex.Message);
                await Task.Delay(RetryDelay, timeProvider, cancellationToken);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Connected to SQL Server after {Attempts} attempt(s)")]
    private static partial void LogConnected(ILogger logger, int attempts);

    [LoggerMessage(Level = LogLevel.Information, Message = "SQL Server is not reachable yet (attempt {Attempt}): {Reason}")]
    private static partial void LogWaiting(ILogger logger, int attempt, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Database schema is up to date")]
    private static partial void LogUpToDate(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Applying {Count} migration(s): {Migrations}")]
    private static partial void LogApplying(ILogger logger, int count, IReadOnlyList<string> migrations);

    [LoggerMessage(Level = LogLevel.Information, Message = "Migrations applied")]
    private static partial void LogApplied(ILogger logger);
}
