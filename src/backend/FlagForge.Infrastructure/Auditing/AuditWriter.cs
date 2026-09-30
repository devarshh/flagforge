using System.Text.Json;
using FlagForge.Application.Common;
using FlagForge.Domain;

namespace FlagForge.Infrastructure.Auditing;

/// <summary>Adds audit entries to the DbContext; the caller's SaveChanges commits them with the change itself.</summary>
internal sealed class AuditWriter(IFlagForgeDbContext db, TimeProvider timeProvider) : IAuditWriter
{
    public void Record(Actor actor, string action, AuditTarget target, string? comment = null, object? before = null, object? after = null)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(target);
        db.AuditEntries.Add(new AuditEntry
        {
            OccurredAt = timeProvider.GetUtcNow(),
            ActorType = actor.Type,
            ActorId = actor.UserId,
            ActorName = actor.Name,
            Action = action,
            ProjectId = target.ProjectId,
            EnvironmentId = target.EnvironmentId,
            FlagId = target.FlagId,
            ResourceKey = target.ResourceKey,
            Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim(),
            Before = ToJson(before),
            After = ToJson(after),
        });
    }

    private static JsonElement? ToJson(object? value) =>
        value is null ? null : JsonSerializer.SerializeToElement(value, value.GetType(), JsonDefaults.Options);
}
