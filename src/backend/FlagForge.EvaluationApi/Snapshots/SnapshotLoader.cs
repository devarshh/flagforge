using System.Diagnostics;
using FlagForge.Evaluation;
using FlagForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FlagForge.EvaluationApi.Snapshots;

/// <summary>Loads an environment's snapshot in one round trip (no tracking, projected columns only).</summary>
public sealed class SnapshotLoader(FlagForgeDbContext db, TimeProvider timeProvider, EvaluationMetrics metrics)
{
    public async Task<EnvironmentSnapshot?> LoadAsync(Guid environmentId, CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        var loadedAt = timeProvider.GetUtcNow();
        var row = await db.Environments.AsNoTracking()
            .Where(e => e.Id == environmentId)
            .Select(e => new
            {
                e.ConfigVersion,
                Flags = db.FlagEnvironmentConfigs
                    .Where(c => c.EnvironmentId == e.Id && !c.Flag.IsArchived)
                    .Select(c => new
                    {
                        c.FlagId,
                        c.Flag.Key,
                        c.Flag.Salt,
                        c.Flag.Variations,
                        c.Enabled,
                        c.OffVariationId,
                        c.Targets,
                        c.Rules,
                        c.Fallthrough,
                    })
                    .ToList(),
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (row is null)
        {
            return null;
        }

        var flags = new Dictionary<string, SnapshotFlag>(row.Flags.Count, StringComparer.Ordinal);
        foreach (var flag in row.Flags)
        {
            var config = new TargetingConfig
            {
                Enabled = flag.Enabled,
                OffVariationId = flag.OffVariationId,
                Targets = flag.Targets,
                Rules = flag.Rules,
                Fallthrough = flag.Fallthrough,
            };
            var variations = flag.Variations.Select(v => new FlagVariation(v.Id, v.Value)).ToList();
            flags[flag.Key] = new SnapshotFlag(flag.FlagId, CompiledFlag.Compile(flag.Key, flag.Salt, variations, config));
        }

        metrics.RecordSnapshotLoad(Stopwatch.GetElapsedTime(started));
        return new EnvironmentSnapshot(environmentId, row.ConfigVersion, flags, loadedAt);
    }
}
