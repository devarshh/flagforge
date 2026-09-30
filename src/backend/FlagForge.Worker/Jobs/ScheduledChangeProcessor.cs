using FlagForge.Application.Schedules;

namespace FlagForge.Worker.Jobs;

public sealed record ProcessingSummary(int Claimed, int Executed, int Deferred, int Skipped, int Failed);

/// <summary>
/// Claims up to 20 due changes in one atomic statement, then executes each in its own scope through the same
/// <see cref="ScheduleService"/> the API uses. Safe to run in many replicas at once.
/// </summary>
public sealed partial class ScheduledChangeProcessor(IServiceScopeFactory scopes, TimeProvider timeProvider, ILogger<ScheduledChangeProcessor> logger)
{
    public const int BatchSize = 20;

    /// <summary>Long enough for any execution; a worker that dies mid-change releases its claim after this.</summary>
    public static readonly TimeSpan ClaimDuration = TimeSpan.FromMinutes(5);

    public async Task<ProcessingSummary> ProcessDueChangesAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<ClaimedChange> claims;
        await using (var scope = scopes.CreateAsyncScope())
        {
            var now = timeProvider.GetUtcNow();
            claims = await scope.ServiceProvider.GetRequiredService<IScheduledChangeClaimer>()
                .ClaimDueAsync(BatchSize, now, now + ClaimDuration, cancellationToken);
        }

        int executed = 0, deferred = 0, skipped = 0, failed = 0;
        foreach (var claim in claims)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                switch (await scope.ServiceProvider.GetRequiredService<ScheduleService>().ExecuteAsync(claim, cancellationToken))
                {
                    case ScheduledChangeOutcome.Executed:
                        executed++;
                        break;
                    case ScheduledChangeOutcome.Deferred:
                        deferred++;
                        break;
                    default:
                        skipped++;
                        break;
                }
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                LogChangeFailed(logger, ex, claim.Id, claim.AttemptCount);

                // A fresh scope: the failed unit of work may hold half-applied tracked entities.
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<ScheduleService>().RecordFailureAsync(claim, ex, cancellationToken);
                failed++;
            }
        }

        return new ProcessingSummary(claims.Count, executed, deferred, skipped, failed);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Scheduled change {ChangeId} failed on attempt {Attempt}")]
    private static partial void LogChangeFailed(ILogger logger, Exception exception, Guid changeId, int attempt);
}
