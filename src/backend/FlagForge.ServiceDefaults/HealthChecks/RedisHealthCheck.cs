using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace FlagForge.ServiceDefaults.HealthChecks;

/// <summary>
/// Reports Redis problems as Degraded, never Unhealthy: without Redis, changes still arrive when snapshots expire,
/// so an outage must not take pods out of rotation.
/// </summary>
internal sealed class RedisHealthCheck(IConnectionMultiplexer redis) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!redis.IsConnected)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, "Not connected to Redis; live updates wait for snapshot expiry.");
        }

        try
        {
            var latency = await redis.GetDatabase().PingAsync();
            return HealthCheckResult.Healthy($"Redis answered in {latency.TotalMilliseconds:0} ms.");
        }
        catch (Exception ex) when (ex is RedisException or TimeoutException)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, "Redis did not answer a ping.", ex);
        }
    }
}
