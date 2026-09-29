using FlagForge.Evaluation;

namespace FlagForge.Domain;

public sealed class Flag
{
    public const int MaxNameLength = 100;
    public const int MaxDescriptionLength = 1000;
    public const int MaxTags = 10;
    public const int MaxTagLength = 32;

    public Guid Id { get; init; }

    public Guid ProjectId { get; init; }

    /// <summary>Unique per project; immutable after creation.</summary>
    public required string Key { get; init; }

    public required string Name { get; set; }

    public string? Description { get; set; }

    public FlagType Type { get; init; }

    public required IReadOnlyList<Variation> Variations { get; set; }

    public IReadOnlyList<string> Tags { get; set; } = [];

    /// <summary>16 random hex characters mixed into bucketing so rollouts are independent across flags.</summary>
    public required string Salt { get; init; }

    /// <summary>Permanent flags (kill switches, config) are excluded from stale detection.</summary>
    public bool IsPermanent { get; set; }

    public bool IsArchived { get; set; }

    public DateTimeOffset? ArchivedAt { get; set; }

    public DateTimeOffset CreatedAt { get; init; }

    public Guid CreatedByUserId { get; init; }

    public DateTimeOffset UpdatedAt { get; set; }

    public Project Project { get; init; } = null!;

    public ICollection<FlagEnvironmentConfig> Configs { get; init; } = [];

    public IReadOnlyList<string> VariationIds => [.. Variations.Select(v => v.Id)];

    public IReadOnlyList<FlagVariation> ToFlagVariations() => [.. Variations.Select(v => new FlagVariation(v.Id, v.Value))];
}
