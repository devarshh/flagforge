using FlagForge.Application.Common;

namespace FlagForge.Application.Stale;

/// <summary>
/// A flag is stale when it is not archived, not permanent, older than 30 days, and either had no evaluations in the
/// last 30 days or served only one variation in every environment that evaluated it in the last 14 days.
/// </summary>
public sealed class StaleFlagService(IFlagForgeDbContext db, IStaleFlagQuery query, TimeProvider timeProvider)
{
    public static readonly TimeSpan MinimumAge = TimeSpan.FromDays(30);
    public static readonly TimeSpan NoEvaluationsWindow = TimeSpan.FromDays(30);
    public static readonly TimeSpan SingleVariationWindow = TimeSpan.FromDays(14);

    public async Task<IReadOnlyList<StaleFlagResponse>> ListAsync(string projectKey, CancellationToken cancellationToken)
    {
        var project = await db.GetProjectAsync(projectKey, cancellationToken);
        var rows = await FindAsync(project.Id, cancellationToken);
        return [.. rows.Select(r => new StaleFlagResponse(
            r.FlagKey, r.Name, Enum.Parse<StaleReason>(r.Reason), r.LastEvaluatedAt, r.ServedVariationId))];
    }

    /// <summary>The raw rows, also used by the flag list to set <c>isStale</c>.</summary>
    public Task<IReadOnlyList<StaleFlagRow>> FindAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var criteria = new StaleFlagCriteria(now - MinimumAge, now - NoEvaluationsWindow, now - SingleVariationWindow);
        return query.FindAsync(projectId, criteria, cancellationToken);
    }
}
