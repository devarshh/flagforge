namespace FlagForge.Evaluation;

/// <summary>
/// A flag and one environment's targeting, compiled for fast repeated evaluation: variations are indexed by id,
/// target lists become one hash lookup, and clause values are parsed once.
/// </summary>
public sealed class CompiledFlag
{
    private readonly FlagVariation[] _variations;
    private readonly bool _enabled;
    private readonly int _offVariationIndex;
    private readonly Dictionary<string, int> _targetVariationByKey;
    private readonly CompiledRule[] _rules;
    private readonly CompiledServe _fallthrough;

    private CompiledFlag(
        string key,
        string salt,
        bool isArchived,
        FlagVariation[] variations,
        bool enabled,
        int offVariationIndex,
        Dictionary<string, int> targetVariationByKey,
        CompiledRule[] rules,
        CompiledServe fallthrough)
    {
        Key = key;
        Salt = salt;
        IsArchived = isArchived;
        _variations = variations;
        _enabled = enabled;
        _offVariationIndex = offVariationIndex;
        _targetVariationByKey = targetVariationByKey;
        _rules = rules;
        _fallthrough = fallthrough;
    }

    public string Key { get; }

    public string Salt { get; }

    public bool IsArchived { get; }

    public IReadOnlyList<FlagVariation> Variations => _variations;

    public static CompiledFlag Compile(
        string key,
        string salt,
        IReadOnlyList<FlagVariation> variations,
        TargetingConfig config,
        bool isArchived = false)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNull(salt);
        ArgumentNullException.ThrowIfNull(variations);
        ArgumentNullException.ThrowIfNull(config);

        var variationArray = variations.ToArray();
        var indexById = new Dictionary<string, int>(variationArray.Length, StringComparer.Ordinal);
        for (var i = 0; i < variationArray.Length; i++)
        {
            indexById.TryAdd(variationArray[i].Id, i);
        }

        int IndexOf(string? variationId) =>
            variationId is not null && indexById.TryGetValue(variationId, out var index) ? index : -1;

        // Targets are checked in order, so the first list that contains a key wins.
        var targets = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var target in config.Targets)
        {
            var index = IndexOf(target.VariationId);
            foreach (var contextKey in target.ContextKeys)
            {
                targets.TryAdd(contextKey, index);
            }
        }

        var rules = config.Rules
            .Select(rule => new CompiledRule(
                rule.Id,
                [.. rule.Clauses.Select(clause => new CompiledClause(clause))],
                CompiledServe.Compile(rule.Serve, IndexOf)))
            .ToArray();

        return new CompiledFlag(
            key,
            salt,
            isArchived,
            variationArray,
            config.Enabled,
            IndexOf(config.OffVariationId),
            targets,
            rules,
            CompiledServe.Compile(config.Fallthrough, IndexOf));
    }

    internal EvaluationResult Evaluate(string flagKey, EvaluationContext context)
    {
        if (!_enabled)
        {
            return Serve(flagKey, _offVariationIndex, EvaluationReason.Off);
        }

        if (_targetVariationByKey.TryGetValue(context.Key, out var targetIndex))
        {
            return Serve(flagKey, targetIndex, EvaluationReason.TargetMatch);
        }

        for (var i = 0; i < _rules.Length; i++)
        {
            var rule = _rules[i];
            if (rule.Matches(context))
            {
                var ruleIndex = rule.Serve.Resolve(Key, Salt, context, out var ruleInRollout);
                return Serve(flagKey, ruleIndex, EvaluationReason.RuleMatch(rule.Id, i, ruleInRollout));
            }
        }

        var index = _fallthrough.Resolve(Key, Salt, context, out var inRollout);
        return Serve(flagKey, index, EvaluationReason.Fallthrough(inRollout));
    }

    private EvaluationResult Serve(string flagKey, int variationIndex, EvaluationReason reason)
    {
        if (variationIndex < 0)
        {
            return EvaluationResult.Error(flagKey);
        }

        var variation = _variations[variationIndex];
        return new EvaluationResult(flagKey, variation.Id, variation.Value, reason);
    }
}
