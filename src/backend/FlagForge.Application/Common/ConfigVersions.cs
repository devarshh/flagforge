using Microsoft.EntityFrameworkCore;

namespace FlagForge.Application.Common;

public static class ConfigVersions
{
    /// <summary>
    /// Bumps <c>ConfigVersion</c> of every environment in a project, in id order so concurrent callers take row locks
    /// in the same sequence and cannot deadlock.
    /// </summary>
    public static async Task<List<ConfigChangedMessage>> IncrementProjectAsync(
        this IFlagForgeDbContext db, Guid projectId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
        var environmentIds = await db.Environments
            .Where(e => e.ProjectId == projectId)
            .OrderBy(e => e.Id)
            .Select(e => e.Id)
            .ToListAsync(cancellationToken);
        var changes = new List<ConfigChangedMessage>(environmentIds.Count);
        foreach (var environmentId in environmentIds)
        {
            changes.Add(new ConfigChangedMessage(environmentId, await db.IncrementConfigVersionAsync(environmentId, cancellationToken)));
        }

        return changes;
    }
}
