using System.Text.Json;
using FlagForge.Application.Targeting;
using FlagForge.Domain;

namespace FlagForge.Application.Flags;

/// <summary>A variation as sent by clients. Omit <see cref="Id"/> for new variations; the server assigns <c>v_xxxxxx</c>.</summary>
public sealed record VariationInput(string Name, JsonElement Value, string? Id = null, string? Description = null);

/// <summary><see cref="Variations"/> is required for non-boolean flags and must be omitted for boolean flags.</summary>
public sealed record CreateFlagRequest(
    string Key,
    string Name,
    FlagType Type,
    string? Description = null,
    IReadOnlyList<VariationInput>? Variations = null,
    IReadOnlyList<string>? Tags = null,
    bool IsPermanent = false);

/// <summary>Partial update: null fields are left unchanged; an empty description clears it.</summary>
public sealed record UpdateFlagRequest(string? Name = null, string? Description = null, IReadOnlyList<string>? Tags = null, bool? IsPermanent = null);

public sealed record ReplaceVariationsRequest(IReadOnlyList<VariationInput> Variations);

public sealed record FlagListQuery(string? Search = null, string? Tag = null, bool IncludeArchived = false, int Page = 1, int PageSize = 25);

public sealed record FlagEnvironmentSummary(string EnvironmentKey, bool Enabled, int Version, DateTimeOffset? LastEvaluatedAt);

public sealed record FlagSummaryResponse(
    Guid Id,
    string Key,
    string Name,
    string? Description,
    FlagType Type,
    IReadOnlyList<string> Tags,
    bool IsPermanent,
    bool IsArchived,
    bool IsStale,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<FlagEnvironmentSummary> Environments);

public sealed record FlagEnvironmentResponse(string EnvironmentKey, DateTimeOffset? LastEvaluatedAt, TargetingResponse Config);

public sealed record FlagResponse(
    Guid Id,
    string Key,
    string Name,
    string? Description,
    FlagType Type,
    IReadOnlyList<Variation> Variations,
    IReadOnlyList<string> Tags,
    bool IsPermanent,
    bool IsArchived,
    DateTimeOffset? ArchivedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<FlagEnvironmentResponse> Environments);
