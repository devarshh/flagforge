using Microsoft.Extensions.Options;

namespace FlagForge.Worker.Jobs;

/// <summary>Processes due scheduled changes every <see cref="WorkerOptions.ScheduledChangePollSeconds"/> seconds.</summary>
public sealed class ScheduledChangeJob(
    IServiceScopeFactory scopes, TimeProvider timeProvider, IOptions<WorkerOptions> options, ILogger<ScheduledChangeJob> logger)
    : PeriodicJob(scopes, timeProvider, logger)
{
    protected override TimeSpan Period => TimeSpan.FromSeconds(options.Value.ScheduledChangePollSeconds);

    protected override Task RunOnceAsync(IServiceProvider services, CancellationToken cancellationToken) =>
        services.GetRequiredService<ScheduledChangeProcessor>().ProcessDueChangesAsync(cancellationToken);
}
