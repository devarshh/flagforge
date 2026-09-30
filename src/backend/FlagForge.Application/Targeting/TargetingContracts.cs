using System.Text.Json;
using FlagForge.Application.Common;
using FlagForge.Domain;
using FlagForge.Evaluation;

namespace FlagForge.Application.Targeting;

/// <summary>A flag's targeting in one environment, with the version to send back as <c>expectedVersion</c>.</summary>
public sealed record TargetingResponse(
    string EnvironmentKey,
    bool Enabled,
    string OffVariationId,
    IReadOnlyList<Target> Targets,
    IReadOnlyList<Rule> Rules,
    Serve Fallthrough,
    int Version,
    DateTimeOffset UpdatedAt,
    UserRef? UpdatedBy)
{
    public static TargetingResponse From(FlagEnvironmentConfig config, string environmentKey, UserRef? updatedBy)
    {
        ArgumentNullException.ThrowIfNull(config);
        return new TargetingResponse(
            environmentKey, config.Enabled, config.OffVariationId, config.Targets, config.Rules, config.Fallthrough, config.Version, config.UpdatedAt, updatedBy);
    }
}

public sealed record UpdateTargetingRequest(TargetingConfig Config, int ExpectedVersion, string? Comment = null);

public sealed record ToggleRequest(bool Enabled, int? ExpectedVersion = null, string? Comment = null);

/// <summary>Evaluates <see cref="Context"/> against <see cref="DraftConfig"/> when given, otherwise the saved config.</summary>
public sealed record PreviewRequest(JsonElement Context, TargetingConfig? DraftConfig = null);

/// <summary>The resource a scheduled change touched, and the notification to publish after commit (if any).</summary>
public sealed record AppliedScheduledChange(AuditTarget Target, ConfigChangedMessage? Change);
