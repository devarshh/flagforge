using System.Text.Json;

namespace FlagForge.Evaluation;

/// <summary>
/// The outcome of evaluating one flag. <see cref="VariationId"/> and <see cref="Value"/> are null for
/// <see cref="EvaluationReasonKind.FlagNotFound"/> and <see cref="EvaluationReasonKind.Error"/>, in which case
/// SDKs return the caller's default value.
/// </summary>
public readonly record struct EvaluationResult(
    string FlagKey,
    string? VariationId,
    JsonElement? Value,
    EvaluationReason Reason)
{
    public static EvaluationResult FlagNotFound(string flagKey) => new(flagKey, null, null, EvaluationReason.FlagNotFound);

    public static EvaluationResult Error(string flagKey) => new(flagKey, null, null, EvaluationReason.Error);
}
