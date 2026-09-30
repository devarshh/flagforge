using FlagForge.Application.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FlagForge.Worker.Jobs;

public sealed record RetentionSummary(int UsageRows, int AuditEntries, int RefreshTokens);

/// <summary>
/// Deletes usage older than <c>Retention__UsageDays</c>, audit entries older than <c>Retention__AuditDays</c>, and
/// refresh tokens that expired more than 7 days ago, in batches of 5,000 so no statement holds locks for long.
/// </summary>
public sealed class RetentionCleanup(IFlagForgeDbContext db, TimeProvider timeProvider, IOptions<RetentionOptions> options)
{
    public const int BatchSize = 5000;
    public static readonly TimeSpan ExpiredRefreshTokenGrace = TimeSpan.FromDays(7);

    public async Task<RetentionSummary> CleanUpAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var usageCutoff = now.AddDays(-options.Value.UsageDays);
        var auditCutoff = now.AddDays(-options.Value.AuditDays);
        var tokenCutoff = now - ExpiredRefreshTokenGrace;
        return new RetentionSummary(
            await DeleteInBatchesAsync(db.FlagUsageHourly.Where(u => u.HourStart < usageCutoff), cancellationToken),
            await DeleteInBatchesAsync(db.AuditEntries.Where(a => a.OccurredAt < auditCutoff), cancellationToken),
            await DeleteInBatchesAsync(db.RefreshTokens.Where(t => t.ExpiresAt < tokenCutoff), cancellationToken));
    }

    private static async Task<int> DeleteInBatchesAsync<T>(IQueryable<T> expired, CancellationToken cancellationToken)
    {
        var total = 0;
        int deleted;
        do
        {
            deleted = await expired.Take(BatchSize).ExecuteDeleteAsync(cancellationToken);
            total += deleted;
        }
        while (deleted == BatchSize);
        return total;
    }
}
