namespace FlagForge.Evaluation;

/// <summary>A single condition of a rule, for example <c>email endsWith "@acme.com"</c>.</summary>
public sealed record Clause
{
    /// <summary>The attribute to read. <c>key</c> addresses the context key; other names are case-sensitive.</summary>
    public required string Attribute { get; init; }

    public required ClauseOperator Operator { get; init; }

    /// <summary>Clause values as strings; numeric and boolean comparisons parse them (invariant culture).</summary>
    public IReadOnlyList<string> Values { get; init; } = [];

    public bool Negate { get; init; }
}
