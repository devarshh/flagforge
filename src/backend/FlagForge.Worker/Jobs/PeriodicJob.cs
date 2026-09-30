namespace FlagForge.Worker.Jobs;

/// <summary>
/// Runs <see cref="RunOnceAsync"/> at startup and then on every tick of a <see cref="PeriodicTimer"/> driven by the
/// injected <see cref="TimeProvider"/>. Each run gets a fresh DI scope; failures are logged and the loop continues.
/// </summary>
public abstract partial class PeriodicJob(IServiceScopeFactory scopes, TimeProvider timeProvider, ILogger logger) : BackgroundService
{
    protected abstract TimeSpan Period { get; }

    protected abstract Task RunOnceAsync(IServiceProvider services, CancellationToken cancellationToken);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Period, timeProvider);
        try
        {
            do
            {
                try
                {
                    await using var scope = scopes.CreateAsyncScope();
                    await RunOnceAsync(scope.ServiceProvider, stoppingToken);
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    LogRunFailed(logger, ex, GetType().Name);
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "{Job} run failed; retrying on the next tick")]
    private static partial void LogRunFailed(ILogger logger, Exception exception, string job);
}
