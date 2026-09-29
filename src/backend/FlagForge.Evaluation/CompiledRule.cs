namespace FlagForge.Evaluation;

internal sealed class CompiledRule(string id, CompiledClause[] clauses, CompiledServe serve)
{
    public string Id { get; } = id;

    public CompiledServe Serve { get; } = serve;

    public bool Matches(EvaluationContext context)
    {
        foreach (var clause in clauses)
        {
            if (!clause.Matches(context))
            {
                return false;
            }
        }

        return true;
    }
}
