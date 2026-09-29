namespace FlagForge.Evaluation;

/// <summary>A serve resolved to variation indexes; rollouts become cumulative bucket thresholds.</summary>
internal sealed class CompiledServe
{
    private readonly int _variationIndex;
    private readonly string? _bucketBy;
    private readonly int[] _thresholds = [];
    private readonly int[] _rolloutIndexes = [];

    private CompiledServe(int variationIndex) => _variationIndex = variationIndex;

    private CompiledServe(string bucketBy, int[] thresholds, int[] rolloutIndexes)
    {
        _variationIndex = -1;
        _bucketBy = bucketBy;
        _thresholds = thresholds;
        _rolloutIndexes = rolloutIndexes;
    }

    public static CompiledServe Compile(Serve serve, Func<string?, int> indexOf)
    {
        var hasVariation = serve.VariationId is not null;
        var rollout = serve.Rollout;
        if (hasVariation == (rollout is not null))
        {
            // Neither or both: the serve is broken, which evaluates to ERROR.
            return new CompiledServe(-1);
        }

        if (rollout is null)
        {
            return new CompiledServe(indexOf(serve.VariationId));
        }

        if (rollout.Weights.Count == 0)
        {
            return new CompiledServe(-1);
        }

        var thresholds = new int[rollout.Weights.Count];
        var indexes = new int[rollout.Weights.Count];
        var cumulative = 0;
        for (var i = 0; i < rollout.Weights.Count; i++)
        {
            cumulative += rollout.Weights[i].Weight;
            thresholds[i] = cumulative;
            indexes[i] = indexOf(rollout.Weights[i].VariationId);
        }

        return new CompiledServe(rollout.BucketBy, thresholds, indexes);
    }

    /// <summary>Returns the served variation index (-1 when the config is broken).</summary>
    public int Resolve(string flagKey, string salt, EvaluationContext context, out bool inRollout)
    {
        if (_bucketBy is null)
        {
            inRollout = false;
            return _variationIndex;
        }

        inRollout = true;
        var bucket = Bucketing.ComputeBucket(flagKey, salt, context, _bucketBy);
        for (var i = 0; i < _thresholds.Length; i++)
        {
            if (bucket < _thresholds[i])
            {
                return _rolloutIndexes[i];
            }
        }

        // Unreachable when weights sum to 100000; kept so under-allocated rollouts still serve a variation.
        return _rolloutIndexes[^1];
    }
}
