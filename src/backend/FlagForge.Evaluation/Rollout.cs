namespace FlagForge.Evaluation;

/// <summary>A percentage rollout. Weights are thousandths of a percent: 100000 is 100%.</summary>
public sealed record Rollout
{
    /// <summary>The attribute whose value is hashed to pick a bucket. Defaults to the context key.</summary>
    public string BucketBy { get; init; } = AttributeNames.Key;

    /// <summary>Weights, stored in the flag's variation order so that ramping a variation up is monotonic.</summary>
    public required IReadOnlyList<WeightedVariation> Weights { get; init; }
}
