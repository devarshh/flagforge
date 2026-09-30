using FlagForge.Application.Usage;
using Microsoft.Extensions.Options;

namespace FlagForge.EvaluationApi.Usage;

/// <summary>
/// Writes buffered usage every <see cref="EvaluationOptions.UsageFlushSeconds"/> and once more during graceful
/// shutdown. The final flush runs in <see cref="StopAsync"/>, after the web server has drained in-flight requests
/// (the host stops the server before other hosted services), within <c>HostOptions.ShutdownTimeout</c> (25 s).
/// </summary>
public sealed partial class UsageFlushService(
    UsageAggregator aggregator,
    IServiceScopeFactory scopes,
    TimeProvider timeProvider,
    IOptions<EvaluationOptions> options,
    ILogger<UsageFlushService> logger) : BackgroundService
{
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        await FlushAsync(cancellationToken);
    }

    /// <summary>Writes all buffered counts; on failure they are kept for the next attempt. Returns rows written.</summary>
    public async Task<int> FlushAsync(CancellationToken cancellationToken)
    {
        var rows = aggregator.Drain();
        if (rows.Count == 0)
        {
            return 0;
        }

        try
        {
            await using var scope = scopes.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<IUsageStore>().AddCountsAsync(rows, cancellationToken);
            return rows.Count;
        }
        catch (Exception ex)
        {
            // Counts go back into the buffer whatever happened, so a flush cancelled by shutdown is retried by the
            // final flush instead of being lost.
            aggregator.Restore(rows);
            if (ex is OperationCanceledException && cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            var dropped = aggregator.TrimOldestHours();
            LogFlushFailed(logger, ex, rows.Count);
            if (dropped > 0)
            {
                LogDropped(logger, dropped, UsageAggregator.MaxKeys);
            }

            return 0;
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.Value.UsageFlushSeconds), timeProvider);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await FlushAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down; StopAsync performs the final flush.
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not write {Rows} usage rows; keeping them for the next flush")]
    private static partial void LogFlushFailed(ILogger logger, Exception exception, int rows);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Usage buffer exceeded {MaxKeys} keys; dropped {Dropped} counts from the oldest hours")]
    private static partial void LogDropped(ILogger logger, int dropped, int maxKeys);
}
