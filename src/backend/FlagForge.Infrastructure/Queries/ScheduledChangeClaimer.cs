using FlagForge.Application.Schedules;
using FlagForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FlagForge.Infrastructure.Queries;

/// <summary>
/// Claims due changes atomically. <c>READPAST</c> skips rows other workers have locked and <c>ROWLOCK</c> keeps the
/// locks narrow, so replicas claim disjoint sets; a claim that outlives <c>ClaimedUntil</c> can be reclaimed.
/// </summary>
internal sealed class ScheduledChangeClaimer(FlagForgeDbContext db) : IScheduledChangeClaimer
{
    public async Task<IReadOnlyList<ClaimedChange>> ClaimDueAsync(
        int maxCount, DateTimeOffset now, DateTimeOffset claimUntil, CancellationToken cancellationToken)
    {
        var rows = await db.Database.SqlQuery<ClaimedRow>($"""
            UPDATE TOP ({maxCount}) ScheduledChanges WITH (ROWLOCK, READPAST)
            SET Status = 'Processing', ClaimedUntil = {claimUntil}, AttemptCount = AttemptCount + 1
            OUTPUT inserted.Id, inserted.AttemptCount, inserted.ExecuteAt
            WHERE (Status = 'Pending' AND ExecuteAt <= {now})
               OR (Status = 'Processing' AND ClaimedUntil < {now})
            """).ToListAsync(cancellationToken);
        return [.. rows.OrderBy(r => r.ExecuteAt).Select(r => new ClaimedChange(r.Id, r.AttemptCount))];
    }

    private sealed class ClaimedRow
    {
        public Guid Id { get; init; }

        public int AttemptCount { get; init; }

        public DateTimeOffset ExecuteAt { get; init; }
    }
}
