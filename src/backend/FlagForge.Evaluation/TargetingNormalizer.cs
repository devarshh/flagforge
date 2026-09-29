namespace FlagForge.Evaluation;

/// <summary>
/// Puts a valid targeting config into canonical form before it is saved: rollout weights follow the flag's
/// variation order (which makes ramp-ups monotonic), duplicate context keys are removed, and empty target lists
/// are dropped.
/// </summary>
public static class TargetingNormalizer
{
    public static TargetingConfig Normalize(TargetingConfig config, IReadOnlyList<string> variationIds)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(variationIds);

        var order = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < variationIds.Count; i++)
        {
            order.TryAdd(variationIds[i], i);
        }

        return config with
        {
            Targets = [.. config.Targets
                .Select(target => target with { ContextKeys = [.. target.ContextKeys.Distinct(StringComparer.Ordinal)] })
                .Where(target => target.ContextKeys.Count > 0)],
            Rules = [.. config.Rules.Select(rule => rule with { Serve = NormalizeServe(rule.Serve, order) })],
            Fallthrough = NormalizeServe(config.Fallthrough, order),
        };
    }

    private static Serve NormalizeServe(Serve serve, Dictionary<string, int> order)
    {
        if (serve.Rollout is null)
        {
            return serve;
        }

        var weights = serve.Rollout.Weights
            .OrderBy(weight => order.GetValueOrDefault(weight.VariationId, int.MaxValue))
            .ToArray();
        return serve with { Rollout = serve.Rollout with { Weights = weights } };
    }
}
