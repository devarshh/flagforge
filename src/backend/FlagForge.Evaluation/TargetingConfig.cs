namespace FlagForge.Evaluation;

/// <summary>The targeting configuration of one flag in one environment.</summary>
public sealed record TargetingConfig
{
    public required bool Enabled { get; init; }

    /// <summary>The variation served while the flag is off.</summary>
    public required string OffVariationId { get; init; }

    public IReadOnlyList<Target> Targets { get; init; } = [];

    public IReadOnlyList<Rule> Rules { get; init; } = [];

    /// <summary>The default rule, served when no target or rule matches.</summary>
    public required Serve Fallthrough { get; init; }
}
