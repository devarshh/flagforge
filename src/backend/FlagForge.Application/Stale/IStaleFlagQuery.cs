namespace FlagForge.Application.Stale;

/// <summary>
/// Finds stale flags in one project with a single set-based query over the hourly usage table, so usage rows are
/// never loaded into memory.
/// </summary>
public interface IStaleFlagQuery
{
    Task<IReadOnlyList<StaleFlagRow>> FindAsync(Guid projectId, StaleFlagCriteria criteria, CancellationToken cancellationToken);
}
