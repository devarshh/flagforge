using System.Security.Claims;
using FlagForge.Application.Common;
using FlagForge.Domain;

namespace FlagForge.ManagementApi.Authentication;

internal static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(TokenClaims.Subject), out var id)
            ? id
            : throw new UnauthorizedException("Your session is not valid. Sign in again.");

    public static Actor ToActor(this ClaimsPrincipal principal) =>
        Actor.ForUser(
            principal.GetUserId(),
            principal.FindFirstValue(TokenClaims.Name) ?? principal.FindFirstValue(TokenClaims.Email) ?? "unknown",
            Enum.TryParse<Role>(principal.FindFirstValue(TokenClaims.Role), out var role) ? role : Role.Viewer);
}
