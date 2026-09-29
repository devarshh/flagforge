using FlagForge.Evaluation;

namespace FlagForge.Domain;

/// <summary>The targeting of one flag in one environment. A row exists for every (flag, environment) pair.</summary>
public sealed class FlagEnvironmentConfig
{
    public Guid Id { get; init; }

    public Guid FlagId { get; init; }

    public Guid EnvironmentId { get; init; }

    public bool Enabled { get; set; }

    public required string OffVariationId { get; set; }

    public IReadOnlyList<Target> Targets { get; set; } = [];

    public IReadOnlyList<Rule> Rules { get; set; } = [];

    public required Serve Fallthrough { get; set; }

    /// <summary>Optimistic concurrency token, incremented in code on every change.</summary>
    public int Version { get; set; } = 1;

    public DateTimeOffset UpdatedAt { get; set; }

    public Guid? UpdatedByUserId { get; set; }

    public Flag Flag { get; init; } = null!;

    public ProjectEnvironment Environment { get; init; } = null!;

    /// <summary>
    /// Defaults: disabled. Boolean flags serve <c>false</c> when off and <c>true</c> by default; other types serve
    /// the last variation when off and the first by default.
    /// </summary>
    public static FlagEnvironmentConfig CreateDefault(Guid id, Flag flag, Guid environmentId, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(flag);
        var (offId, fallthroughId) = flag.Type == FlagType.Boolean
            ? (Variation.FalseId, Variation.TrueId)
            : (flag.Variations[^1].Id, flag.Variations[0].Id);

        return new FlagEnvironmentConfig
        {
            Id = id,
            FlagId = flag.Id,
            EnvironmentId = environmentId,
            Enabled = false,
            OffVariationId = offId,
            Fallthrough = Serve.Variation(fallthroughId),
            UpdatedAt = now,
        };
    }

    public TargetingConfig ToTargetingConfig() => new()
    {
        Enabled = Enabled,
        OffVariationId = OffVariationId,
        Targets = Targets,
        Rules = Rules,
        Fallthrough = Fallthrough,
    };

    /// <summary>Replaces the targeting and bumps <see cref="Version"/>.</summary>
    public void Apply(TargetingConfig config, DateTimeOffset now, Guid? updatedByUserId)
    {
        ArgumentNullException.ThrowIfNull(config);
        Enabled = config.Enabled;
        OffVariationId = config.OffVariationId;
        Targets = config.Targets;
        Rules = config.Rules;
        Fallthrough = config.Fallthrough;
        Version++;
        UpdatedAt = now;
        UpdatedByUserId = updatedByUserId;
    }

    /// <summary>True when any serve in this config references the variation.</summary>
    public bool References(string variationId) =>
        OffVariationId == variationId
        || Targets.Any(t => t.VariationId == variationId)
        || Rules.Any(r => ServeReferences(r.Serve, variationId))
        || ServeReferences(Fallthrough, variationId);

    private static bool ServeReferences(Serve serve, string variationId) =>
        serve.VariationId == variationId || (serve.Rollout?.Weights.Any(w => w.VariationId == variationId) ?? false);
}
