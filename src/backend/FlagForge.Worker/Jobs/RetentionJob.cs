namespace FlagForge.Worker.Jobs;

/// <summary>Runs <see cref="RetentionCleanup"/> hourly.</summary>
public sealed class RetentionJob(IServiceScopeFactory scopes, TimeProvider timeProvider, ILogger<RetentionJob> logger)
    : PeriodicJob(scopes, timeProvider, logger)
{
    protected override TimeSpan Period => TimeSpan.FromHours(1);

    protected override Task RunOnceAsync(IServiceProvider services, CancellationToken cancellationToken) =>
        services.GetRequiredService<RetentionCleanup>().CleanUpAsync(cancellationToken);
}
