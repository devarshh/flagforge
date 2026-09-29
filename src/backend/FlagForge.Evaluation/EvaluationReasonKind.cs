namespace FlagForge.Evaluation;

/// <summary>Why a value was served. Serialized as OFF, TARGET_MATCH, RULE_MATCH, FALLTHROUGH, FLAG_NOT_FOUND, ERROR.</summary>
public enum EvaluationReasonKind
{
    Off,
    TargetMatch,
    RuleMatch,
    Fallthrough,
    FlagNotFound,
    Error,
}
