using System.Text.Json.Serialization;

namespace FlagForge.Evaluation;

/// <summary>
/// What a rule or the fallthrough serves: exactly one of a fixed <see cref="VariationId"/> or a percentage
/// <see cref="Rollout"/>. <see cref="TargetingValidator"/> enforces the "exactly one" rule. Only the one in use is
/// written to JSON, so API responses and audit diffs show the documented shape without a null alternative.
/// </summary>
public sealed record Serve
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? VariationId { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Rollout? Rollout { get; init; }

    public static Serve Variation(string variationId) => new() { VariationId = variationId };

    public static Serve PercentageRollout(Rollout rollout) => new() { Rollout = rollout };
}
