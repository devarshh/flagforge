namespace FlagForge.Application.Schedules;

/// <summary>
/// Claims due scheduled changes in one atomic statement (<c>UPDATE TOP ... WITH (ROWLOCK, READPAST) ... OUTPUT</c>),
/// so several worker replicas never claim the same change. Expired claims are reclaimed.
/// </summary>
public interface IScheduledChangeClaimer
{
    Task<IReadOnlyList<ClaimedChange>> ClaimDueAsync(int maxCount, DateTimeOffset now, DateTimeOffset claimUntil, CancellationToken cancellationToken);
}
