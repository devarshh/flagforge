using System.Text.Json;

namespace FlagForge.Evaluation.Tests;

/// <summary>Concise builders for flags, configs, and contexts used across the evaluation tests.</summary>
internal static class Build
{
    public const string FlagKey = "test-flag";
    public const string Salt = "0123456789abcdef";

    public static readonly FlagVariation[] BooleanVariations =
    [
        new("true", Json("true")),
        new("false", Json("false")),
    ];

    public static readonly string[] BooleanVariationIds = ["true", "false"];

    public static JsonElement Json(string raw)
    {
        using var document = JsonDocument.Parse(raw);
        return document.RootElement.Clone();
    }

    public static TargetingConfig Config(
        bool enabled = true,
        string offVariationId = "false",
        Serve? fallthrough = null,
        IReadOnlyList<Target>? targets = null,
        IReadOnlyList<Rule>? rules = null) => new()
        {
            Enabled = enabled,
            OffVariationId = offVariationId,
            Fallthrough = fallthrough ?? Serve.Variation("false"),
            Targets = targets ?? [],
            Rules = rules ?? [],
        };

    public static Target Target(string variationId, params string[] contextKeys) =>
        new() { VariationId = variationId, ContextKeys = contextKeys };

    public static Rule Rule(string id, Serve serve, params Clause[] clauses) =>
        new() { Id = id, Serve = serve, Clauses = clauses };

    public static Clause Clause(string attribute, ClauseOperator op, params string[] values) =>
        new() { Attribute = attribute, Operator = op, Values = values };

    public static Clause Not(Clause clause) => clause with { Negate = true };

    public static Serve Rollout(params (string VariationId, int Weight)[] weights) =>
        Serve.PercentageRollout(new Rollout
        {
            Weights = [.. weights.Select(w => new WeightedVariation { VariationId = w.VariationId, Weight = w.Weight })],
        });

    public static Serve RolloutBy(string bucketBy, params (string VariationId, int Weight)[] weights) =>
        Serve.PercentageRollout(Rollout(weights).Rollout! with { BucketBy = bucketBy });

    public static CompiledFlag BooleanFlag(TargetingConfig config, string key = FlagKey, string salt = Salt, bool isArchived = false) =>
        CompiledFlag.Compile(key, salt, BooleanVariations, config, isArchived);

    public static EvaluationContext Context(string key, params (string Name, AttributeValue Value)[] attributes) =>
        new(key, attributes.ToDictionary(a => a.Name, a => a.Value));

    /// <summary>Builds a context from a JSON literal for one attribute, exercising the real parser.</summary>
    public static EvaluationContext ContextWith(string attributeName, string attributeJson)
    {
        var json = Json($$$"""{"key":"user-1","attributes":{"{{{attributeName}}}":{{{attributeJson}}}}}""");
        if (!EvaluationContextParser.TryParse(json, out var context, out var errors))
        {
            throw new InvalidOperationException($"Invalid test context: {string.Join("; ", errors)}");
        }

        return context;
    }

    public static EvaluationResult Evaluate(TargetingConfig config, EvaluationContext context) =>
        FlagEvaluator.Evaluate(FlagKey, BooleanFlag(config), context);
}
