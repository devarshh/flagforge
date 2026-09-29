namespace FlagForge.Evaluation;

/// <summary>A validation problem with a JSON-pointer-style path such as <c>rules[1].clauses[0].values</c>.</summary>
public sealed record ValidationError(string Path, string Message);
