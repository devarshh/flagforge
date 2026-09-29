namespace FlagForge.Evaluation;

/// <summary>Limits on evaluation contexts, enforced by <see cref="EvaluationContextParser"/>.</summary>
public static class ContextLimits
{
    public const int MaxKeyLength = 256;
    public const int MaxAttributes = 50;
    public const int MaxStringLength = 1024;
    public const int MaxArrayItems = 100;
}
