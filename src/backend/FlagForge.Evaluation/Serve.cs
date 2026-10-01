namespace FlagForge.Evaluation;

/// <summary>
/// What a rule or the fallthrough serves: exactly one of a fixed <see cref="VariationId"/> or a percentage
/// <see cref="Rollout"/>. <see cref="TargetingValidator"/> enforces the "exactly one" rule.
/// </summary>
public sealed record Serve
{
    public string? VariationId { get; init; }

    public Rollout? Rollout { get; init; }

    public static Serve Variation(string variationId) => new() { VariationId = variationId };

    public static Serve PercentageRollout(Rollout rollout) => new() { Rollout = rollout };
}
