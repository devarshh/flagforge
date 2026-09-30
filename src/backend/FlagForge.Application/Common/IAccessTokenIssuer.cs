using FlagForge.Domain;

namespace FlagForge.Application.Common;

/// <summary>Issues short-lived access tokens (JWTs) for dashboard users.</summary>
public interface IAccessTokenIssuer
{
    AccessToken Issue(User user);
}
