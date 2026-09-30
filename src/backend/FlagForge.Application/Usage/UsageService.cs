using FlagForge.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace FlagForge.Application.Usage;

/// <summary>Evaluation counts per variation, bucketed by hour or day (UTC).</summary>
public sealed class UsageService(IFlagForgeDbContext db, TimeProvider timeProvider)
{
    public static readonly TimeSpan MaxRange = TimeSpan.FromDays(90);

    public async Task<IReadOnlyList<UsageBucket>> GetAsync(
        string projectKey, string flagKey, string environmentKey, UsageQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var daily = query.Granularity switch
        {
            UsageQuery.Hour => false,
            UsageQuery.Day => true,
            _ => throw RequestValidationException.For("granularity", "Use hour or day."),
        };
        var to = query.To ?? timeProvider.GetUtcNow();
        var from = query.From ?? to - (daily ? TimeSpan.FromDays(30) : TimeSpan.FromHours(24));
        if (from >= to)
        {
            throw RequestValidationException.For("from", "Choose a start time before the end time.");
        }

        if (to - from > MaxRange)
        {
            throw RequestValidationException.For("from", "Usage can be queried for at most 90 days at a time.");
        }

        var project = await db.GetProjectAsync(projectKey, cancellationToken);
        var flag = await db.GetFlagAsync(project, flagKey, cancellationToken);
        var environment = await db.GetEnvironmentAsync(project, environmentKey, cancellationToken);
        var start = daily ? StartOfDay(from) : StartOfHour(from);
        var rows = await db.FlagUsageHourly.AsNoTracking()
            .Where(u => u.FlagId == flag.Id && u.EnvironmentId == environment.Id && u.HourStart >= start && u.HourStart < to)
            .Select(u => new { u.HourStart, u.VariationId, u.Count })
            .ToListAsync(cancellationToken);

        // Hourly rows are already buckets; a 90-day range is at most 2,160 hours per variation, so days group in memory.
        return
        [
            .. rows
                .GroupBy(r => (BucketStart: daily ? StartOfDay(r.HourStart) : r.HourStart, r.VariationId))
                .Select(g => new UsageBucket(g.Key.BucketStart, g.Key.VariationId, g.Sum(r => r.Count)))
                .OrderBy(b => b.BucketStart)
                .ThenBy(b => b.VariationId, StringComparer.Ordinal),
        ];
    }

    public static DateTimeOffset StartOfHour(DateTimeOffset value)
    {
        var utc = value.ToUniversalTime();
        return new DateTimeOffset(utc.Year, utc.Month, utc.Day, utc.Hour, 0, 0, TimeSpan.Zero);
    }

    public static DateTimeOffset StartOfDay(DateTimeOffset value)
    {
        var utc = value.ToUniversalTime();
        return new DateTimeOffset(utc.Year, utc.Month, utc.Day, 0, 0, 0, TimeSpan.Zero);
    }
}
