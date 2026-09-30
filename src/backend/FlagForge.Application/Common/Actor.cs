using FlagForge.Domain;

namespace FlagForge.Application.Common;

/// <summary>Who is performing an operation: a signed-in user, or the system (for example the scheduler).</summary>
public sealed record Actor(ActorType Type, Guid? UserId, string Name, Role? Role)
{
    public static Actor ForUser(Guid userId, string name, Role role) => new(ActorType.User, userId, name, role);

    public static Actor System(string name) => new(ActorType.System, null, name, null);

    /// <summary>Admins and the system may change protected environments; editors may not.</summary>
    public bool CanChangeProtectedEnvironments => Type == ActorType.System || Role == Domain.Role.Admin;
}
