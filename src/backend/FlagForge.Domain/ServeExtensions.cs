using FlagForge.Evaluation;

namespace FlagForge.Domain;

public static class ServeExtensions
{
    /// <summary>True when the serve can hand out the variation, either directly or as part of a rollout.</summary>
    public static bool References(this Serve serve, string variationId)
    {
        ArgumentNullException.ThrowIfNull(serve);
        return serve.VariationId == variationId || (serve.Rollout?.Weights.Any(w => w.VariationId == variationId) ?? false);
    }
}
