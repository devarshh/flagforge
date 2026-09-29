namespace FlagForge.Evaluation;

/// <summary>Individual targeting: context keys that always receive a specific variation.</summary>
public sealed record Target
{
    public required string VariationId { get; init; }

    public required IReadOnlyList<string> ContextKeys { get; init; }
}
