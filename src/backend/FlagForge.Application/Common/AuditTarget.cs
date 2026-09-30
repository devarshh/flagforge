using FlagForge.Domain;

namespace FlagForge.Application.Common;

/// <summary>The resource an audit entry describes.</summary>
public sealed record AuditTarget(string ResourceKey, Guid? ProjectId = null, Guid? EnvironmentId = null, Guid? FlagId = null)
{
    public static AuditTarget For(Project project) => new(project.Key, project.Id);

    public static AuditTarget For(Project project, ProjectEnvironment environment) =>
        new(AuditEntry.ResourceKeyFor(project.Key, environmentKey: environment.Key), project.Id, environment.Id);

    public static AuditTarget For(Project project, Flag flag) =>
        new(AuditEntry.ResourceKeyFor(project.Key, flag.Key), project.Id, FlagId: flag.Id);

    public static AuditTarget For(Project project, Flag flag, ProjectEnvironment environment) =>
        new(AuditEntry.ResourceKeyFor(project.Key, flag.Key, environment.Key), project.Id, environment.Id, flag.Id);

    public static AuditTarget For(User user) => new($"users/{user.Email}");
}
