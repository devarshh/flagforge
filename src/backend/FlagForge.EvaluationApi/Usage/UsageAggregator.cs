using System.Collections.Concurrent;
using FlagForge.Application.Usage;
using FlagForge.Domain;

namespace FlagForge.EvaluationApi.Usage;

/// <summary>
/// Counts served results in memory per (environment, flag, variation, hour) until the next flush. Draining removes
/// keys one by one, so increments that race with a flush land in the next one instead of being lost.
/// </summary>
public sealed class UsageAggregator(TimeProvider timeProvider)
{
    public const int MaxKeys = 100_000;

    private readonly ConcurrentDictionary<UsageKey, long> _counts = new();

    public int KeyCount => _counts.Count;

    public void Record(Guid environmentId, Guid flagId, string? variationId)
    {
        // FLAG_NOT_FOUND and ERROR results carry no variation and are not counted.
        if (variationId is null)
        {
            return;
        }

        var key = new UsageKey(environmentId, flagId, variationId, UsageService.StartOfHour(timeProvider.GetUtcNow()));
        _counts.AddOrUpdate(key, 1, static (_, count) => count + 1);
    }

    public IReadOnlyList<FlagUsageHourly> Drain()
    {
        var rows = new List<FlagUsageHourly>(_counts.Count);
        foreach (var key in _counts.Keys)
        {
            if (_counts.TryRemove(key, out var count))
            {
                rows.Add(new FlagUsageHourly
                {
                    EnvironmentId = key.EnvironmentId,
                    FlagId = key.FlagId,
                    VariationId = key.VariationId,
                    HourStart = key.HourStart,
                    Count = count,
                });
            }
        }

        return rows;
    }

    /// <summary>Puts counts back after a failed flush so the next flush retries them.</summary>
    public void Restore(IEnumerable<FlagUsageHourly> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        foreach (var row in rows)
        {
            _counts.AddOrUpdate(new UsageKey(row.EnvironmentId, row.FlagId, row.VariationId, row.HourStart), row.Count, (_, count) => count + row.Count);
        }
    }

    /// <summary>When the database has been unreachable for a long time, drops the oldest hours to bound memory.</summary>
    public int TrimOldestHours(int maxKeys = MaxKeys)
    {
        var dropped = 0;
        while (_counts.Count > maxKeys)
        {
            var oldest = _counts.Keys.Min(k => k.HourStart);
            foreach (var key in _counts.Keys.Where(k => k.HourStart == oldest))
            {
                if (_counts.TryRemove(key, out _))
                {
                    dropped++;
                }
            }
        }

        return dropped;
    }

    private readonly record struct UsageKey(Guid EnvironmentId, Guid FlagId, string VariationId, DateTimeOffset HourStart);
}
