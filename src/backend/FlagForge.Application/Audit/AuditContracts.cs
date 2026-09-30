using System.Text.Json;
using FlagForge.Domain;

namespace FlagForge.Application.Audit;

/// <summary>Filters for the audit log. Flag and environment filters apply within <see cref="ProjectKey"/>.</summary>
public sealed record AuditQuery(
    string? ProjectKey = null,
    string? FlagKey = null,
    string? EnvironmentKey = null,
    Guid? ActorId = null,
    string? Action = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    int Page = 1,
    int PageSize = 25);

public sealed record AuditEntryResponse(
    long Id,
    DateTimeOffset OccurredAt,
    ActorType ActorType,
    Guid? ActorId,
    string ActorName,
    string Action,
    Guid? ProjectId,
    Guid? EnvironmentId,
    Guid? FlagId,
    string ResourceKey,
    string? Comment,
    JsonElement? Before,
    JsonElement? After)
{
    public static AuditEntryResponse From(AuditEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return new AuditEntryResponse(
            entry.Id,
            entry.OccurredAt,
            entry.ActorType,
            entry.ActorId,
            entry.ActorName,
            entry.Action,
            entry.ProjectId,
            entry.EnvironmentId,
            entry.FlagId,
            entry.ResourceKey,
            entry.Comment,
            entry.Before,
            entry.After);
    }
}
