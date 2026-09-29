namespace FlagForge.Evaluation;

/// <summary>
/// The reason for an evaluation result. <see cref="RuleId"/> and <see cref="RuleIndex"/> are set for
/// <see cref="EvaluationReasonKind.RuleMatch"/>; <see cref="InRollout"/> is set for rule matches and fallthrough.
/// </summary>
public readonly record struct EvaluationReason(
    EvaluationReasonKind Kind,
    string? RuleId = null,
    int? RuleIndex = null,
    bool? InRollout = null)
{
    public static EvaluationReason Off { get; } = new(EvaluationReasonKind.Off);

    public static EvaluationReason TargetMatch { get; } = new(EvaluationReasonKind.TargetMatch);

    public static EvaluationReason FlagNotFound { get; } = new(EvaluationReasonKind.FlagNotFound);

    public static EvaluationReason Error { get; } = new(EvaluationReasonKind.Error);

    public static EvaluationReason RuleMatch(string ruleId, int ruleIndex, bool inRollout) =>
        new(EvaluationReasonKind.RuleMatch, ruleId, ruleIndex, inRollout);

    public static EvaluationReason Fallthrough(bool inRollout) =>
        new(EvaluationReasonKind.Fallthrough, InRollout: inRollout);
}
