using System.ComponentModel.DataAnnotations;

namespace FlagForge.EvaluationApi;

/// <summary>Bound from the <c>Evaluation</c> configuration section.</summary>
public sealed class EvaluationOptions
{
    public const string SectionName = "Evaluation";

    /// <summary>How long a compiled snapshot is served before it is reloaded, even without a change message.</summary>
    [Range(1, 3600)]
    public int SnapshotTtlSeconds { get; set; } = 60;

    /// <summary>How often buffered usage counts are written to the database.</summary>
    [Range(1, 3600)]
    public int UsageFlushSeconds { get; set; } = 30;
}

/// <summary>Per-SDK-key token bucket, bound from the <c>RateLimiting</c> section.</summary>
public sealed class SdkRateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    [Range(1, 100_000)]
    public int SdkPermitsPerSecond { get; set; } = 20;

    [Range(1, 1_000_000)]
    public int SdkBurst { get; set; } = 100;
}

/// <summary>Browser origins allowed to call <c>/sdk</c>, from <c>Cors__AllowedOrigins</c> (comma-separated, <c>*</c> for any).</summary>
public sealed class SdkCorsOptions
{
    public const string SectionName = "Cors";

    public string AllowedOrigins { get; set; } = string.Empty;

    public IReadOnlyList<string> Origins =>
        [.. AllowedOrigins.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
}
