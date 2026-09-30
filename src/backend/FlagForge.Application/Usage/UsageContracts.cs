namespace FlagForge.Application.Usage;

/// <summary>
/// <see cref="Granularity"/> is <c>hour</c> or <c>day</c>. Defaults: the last 24 hours for hourly buckets, the last
/// 30 days for daily buckets. The range can span at most 90 days.
/// </summary>
public sealed record UsageQuery(DateTimeOffset? From = null, DateTimeOffset? To = null, string Granularity = UsageQuery.Hour)
{
    public const string Hour = "hour";
    public const string Day = "day";
}

public sealed record UsageBucket(DateTimeOffset BucketStart, string VariationId, long Count);
