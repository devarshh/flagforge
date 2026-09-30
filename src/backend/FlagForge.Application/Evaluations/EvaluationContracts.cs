using System.Text.Json;
using System.Text.Json.Serialization;
using FlagForge.Evaluation;

namespace FlagForge.Application.Evaluations;

/// <summary>The wire format of a reason; optional fields are omitted when they do not apply.</summary>
public sealed record ReasonResponse(
    EvaluationReasonKind Kind,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? RuleId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? RuleIndex = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? InRollout = null)
{
    public static ReasonResponse From(EvaluationReason reason) => new(reason.Kind, reason.RuleId, reason.RuleIndex, reason.InRollout);
}

/// <summary>One flag's result: served value, variation, and reason. Never includes targeting configuration.</summary>
public sealed record FlagValueResponse(JsonElement? Value, string? VariationId, ReasonResponse Reason)
{
    public static FlagValueResponse From(EvaluationResult result) => new(result.Value, result.VariationId, ReasonResponse.From(result.Reason));
}

public sealed record EvaluationResultResponse(string FlagKey, JsonElement? Value, string? VariationId, ReasonResponse Reason)
{
    public static EvaluationResultResponse From(EvaluationResult result) =>
        new(result.FlagKey, result.Value, result.VariationId, ReasonResponse.From(result.Reason));
}
