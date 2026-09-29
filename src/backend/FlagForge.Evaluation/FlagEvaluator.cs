namespace FlagForge.Evaluation;

/// <summary>Evaluates flags. Pure and deterministic: the same inputs always produce the same result.</summary>
public static class FlagEvaluator
{
    /// <summary>
    /// Evaluates <paramref name="flag"/> for <paramref name="context"/>. A missing (null) or archived flag returns
    /// <see cref="EvaluationReasonKind.FlagNotFound"/>; a reference to a variation that does not exist returns
    /// <see cref="EvaluationReasonKind.Error"/>. Neither carries a value, so SDKs fall back to the caller's default.
    /// </summary>
    public static EvaluationResult Evaluate(string flagKey, CompiledFlag? flag, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(flagKey);
        ArgumentNullException.ThrowIfNull(context);

        if (flag is null || flag.IsArchived)
        {
            return EvaluationResult.FlagNotFound(flagKey);
        }

        return flag.Evaluate(flagKey, context);
    }
}
