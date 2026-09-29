namespace FlagForge.Evaluation;

/// <summary>A targeting rule: when every clause matches, the rule's serve is used.</summary>
public sealed record Rule
{
    public required string Id { get; init; }

    public string? Description { get; init; }

    public required IReadOnlyList<Clause> Clauses { get; init; }

    public required Serve Serve { get; init; }
}
