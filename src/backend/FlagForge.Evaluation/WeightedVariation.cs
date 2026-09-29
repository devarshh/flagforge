namespace FlagForge.Evaluation;

public sealed record WeightedVariation
{
    public required string VariationId { get; init; }

    /// <summary>Thousandths of a percent, from 0 to 100000.</summary>
    public required int Weight { get; init; }
}
