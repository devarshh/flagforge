namespace FlagForge.ServiceDefaults.HealthChecks;

/// <summary>Remembers the last healthy database check so readiness probes do not hit SQL on every call.</summary>
internal sealed class DatabaseHealthCache
{
    public static readonly TimeSpan Duration = TimeSpan.FromSeconds(30);

    private long _healthyUntilTicks;

    public bool IsHealthy(DateTimeOffset now) => now.UtcTicks < Interlocked.Read(ref _healthyUntilTicks);

    public void MarkHealthy(DateTimeOffset now) => Interlocked.Exchange(ref _healthyUntilTicks, (now + Duration).UtcTicks);
}
