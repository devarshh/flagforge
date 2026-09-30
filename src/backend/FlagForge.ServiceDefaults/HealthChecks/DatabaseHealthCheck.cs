using FlagForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace FlagForge.ServiceDefaults.HealthChecks;

/// <summary>Ready when SQL Server is reachable and no EF Core migrations are pending. Healthy results are cached for 30 s.</summary>
internal sealed class DatabaseHealthCheck(FlagForgeDbContext db, DatabaseHealthCache cache, TimeProvider timeProvider) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (cache.IsHealthy(timeProvider.GetUtcNow()))
        {
            return HealthCheckResult.Healthy("SQL Server was reachable and migrated at the last check.");
        }

        if (!await db.Database.CanConnectAsync(cancellationToken))
        {
            return HealthCheckResult.Unhealthy("SQL Server is not reachable.");
        }

        var pending = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
        if (pending.Count > 0)
        {
            return HealthCheckResult.Unhealthy($"{pending.Count} database migration(s) are pending. Run the migrator.");
        }

        cache.MarkHealthy(timeProvider.GetUtcNow());
        return HealthCheckResult.Healthy("SQL Server is reachable and the schema is up to date.");
    }
}
