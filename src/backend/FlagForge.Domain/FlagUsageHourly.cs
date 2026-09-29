namespace FlagForge.Domain;

/// <summary>
/// Evaluation counts per hour. Keyed by (environment, flag, variation, hour); deliberately has no foreign keys so
/// a flush never fails for a flag deleted between evaluation and flush.
/// </summary>
public sealed class FlagUsageHourly
{
    public Guid EnvironmentId { get; init; }

    public Guid FlagId { get; init; }

    public required string VariationId { get; init; }

    public DateTimeOffset HourStart { get; init; }

    public long Count { get; set; }
}
