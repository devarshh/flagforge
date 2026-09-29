using System.Text.Json;

namespace FlagForge.Domain;

/// <summary>
/// An immutable record of a change. It has no foreign keys, so history survives when the project, environment, or
/// flag it describes is deleted.
/// </summary>
public sealed class AuditEntry
{
    public const int MaxCommentLength = 1000;

    public long Id { get; init; }

    public DateTimeOffset OccurredAt { get; init; }

    public ActorType ActorType { get; init; }

    public Guid? ActorId { get; init; }

    public required string ActorName { get; init; }

    public required string Action { get; init; }

    public Guid? ProjectId { get; init; }

    public Guid? EnvironmentId { get; init; }

    public Guid? FlagId { get; init; }

    /// <summary>Readable resource, for example <c>acme-coffee/new-product-layout@production</c>.</summary>
    public required string ResourceKey { get; init; }

    public string? Comment { get; init; }

    public JsonElement? Before { get; init; }

    public JsonElement? After { get; init; }

    public static string ResourceKeyFor(string projectKey, string? flagKey = null, string? environmentKey = null) =>
        (flagKey, environmentKey) switch
        {
            (null, null) => projectKey,
            (null, _) => $"{projectKey}@{environmentKey}",
            (_, null) => $"{projectKey}/{flagKey}",
            _ => $"{projectKey}/{flagKey}@{environmentKey}",
        };
}
