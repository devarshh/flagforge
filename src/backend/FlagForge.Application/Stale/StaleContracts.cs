namespace FlagForge.Application.Stale;

public enum StaleReason
{
    /// <summary>No evaluations in any environment during the last 30 days.</summary>
    NoRecentEvaluations,

    /// <summary>Every environment that evaluated the flag in the last 14 days served a single variation.</summary>
    FullyRolledOut,
}

public sealed record StaleFlagResponse(
    string FlagKey,
    string Name,
    StaleReason Reason,
    DateTimeOffset? LastEvaluatedAt,
    string? ServedVariationId);

/// <summary>Thresholds for one stale-flag query; <see cref="StaleFlagService"/> derives them from the current time.</summary>
public sealed record StaleFlagCriteria(
    DateTimeOffset CreatedBefore,
    DateTimeOffset EvaluatedSince,
    DateTimeOffset SingleVariationSince);

/// <summary>A row produced by <see cref="IStaleFlagQuery"/>.</summary>
public sealed class StaleFlagRow
{
    public required string FlagKey { get; init; }

    public required string Name { get; init; }

    /// <summary><see cref="StaleReason"/> name, as computed in SQL.</summary>
    public required string Reason { get; init; }

    public DateTimeOffset? LastEvaluatedAt { get; init; }

    public string? ServedVariationId { get; init; }
}
