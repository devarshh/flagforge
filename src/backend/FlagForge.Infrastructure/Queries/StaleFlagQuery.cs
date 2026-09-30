using FlagForge.Application.Stale;
using FlagForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FlagForge.Infrastructure.Queries;

/// <summary>
/// One set-based query: candidate flags, their last evaluation hour, and per-environment distinct variation counts in
/// the recent window. No usage rows leave the database.
/// </summary>
internal sealed class StaleFlagQuery(FlagForgeDbContext db) : IStaleFlagQuery
{
    public async Task<IReadOnlyList<StaleFlagRow>> FindAsync(Guid projectId, StaleFlagCriteria criteria, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        return await db.Database.SqlQuery<StaleFlagRow>($"""
            WITH Candidates AS (
                SELECT f.Id, f.[Key], f.Name
                FROM Flags f
                WHERE f.ProjectId = {projectId} AND f.IsArchived = 0 AND f.IsPermanent = 0 AND f.CreatedAt < {criteria.CreatedBefore}
            ),
            LastEvaluated AS (
                SELECT u.FlagId, MAX(u.HourStart) AS LastEvaluatedAt
                FROM FlagUsageHourly u
                JOIN Candidates c ON c.Id = u.FlagId
                WHERE u.[Count] > 0
                GROUP BY u.FlagId
            ),
            RecentPerEnvironment AS (
                SELECT u.FlagId, u.EnvironmentId, COUNT(DISTINCT u.VariationId) AS ServedVariations, MIN(u.VariationId) AS VariationId
                FROM FlagUsageHourly u
                JOIN Candidates c ON c.Id = u.FlagId
                WHERE u.HourStart >= {criteria.SingleVariationSince} AND u.[Count] > 0
                GROUP BY u.FlagId, u.EnvironmentId
            ),
            FullyRolledOut AS (
                SELECT FlagId, CASE WHEN COUNT(DISTINCT VariationId) = 1 THEN MIN(VariationId) END AS ServedVariationId
                FROM RecentPerEnvironment
                GROUP BY FlagId
                HAVING MAX(ServedVariations) = 1
            )
            SELECT
                c.[Key] AS FlagKey,
                c.Name,
                CASE WHEN l.LastEvaluatedAt IS NULL OR l.LastEvaluatedAt < {criteria.EvaluatedSince}
                     THEN 'NoRecentEvaluations' ELSE 'FullyRolledOut' END AS Reason,
                l.LastEvaluatedAt,
                CASE WHEN l.LastEvaluatedAt IS NULL OR l.LastEvaluatedAt < {criteria.EvaluatedSince}
                     THEN NULL ELSE r.ServedVariationId END AS ServedVariationId
            FROM Candidates c
            LEFT JOIN LastEvaluated l ON l.FlagId = c.Id
            LEFT JOIN FullyRolledOut r ON r.FlagId = c.Id
            WHERE l.LastEvaluatedAt IS NULL OR l.LastEvaluatedAt < {criteria.EvaluatedSince} OR r.FlagId IS NOT NULL
            """).ToListAsync(cancellationToken);
    }
}
