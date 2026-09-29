namespace FlagForge.Evaluation;

/// <summary>
/// Clause operators. "Is not one of" is expressed as <see cref="In"/> with <see cref="Clause.Negate"/> set;
/// regular-expression operators are intentionally not supported (ReDoS risk).
/// </summary>
public enum ClauseOperator
{
    In,
    Contains,
    StartsWith,
    EndsWith,
    Lt,
    Lte,
    Gt,
    Gte,
    Exists,
}
