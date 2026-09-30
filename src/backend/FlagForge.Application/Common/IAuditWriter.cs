namespace FlagForge.Application.Common;

/// <summary>Adds an audit entry to the current unit of work, so it commits in the same transaction as the change.</summary>
public interface IAuditWriter
{
    void Record(Actor actor, string action, AuditTarget target, string? comment = null, object? before = null, object? after = null);
}
