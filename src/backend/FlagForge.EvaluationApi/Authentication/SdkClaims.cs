using System.Security.Claims;

namespace FlagForge.EvaluationApi.Authentication;

public static class SdkClaims
{
    public const string SdkKeyId = "sdkKeyId";
    public const string EnvironmentId = "environmentId";
    public const string ProjectId = "projectId";

    public static Guid GetEnvironmentId(this ClaimsPrincipal principal) => Read(principal, EnvironmentId);

    public static Guid GetSdkKeyId(this ClaimsPrincipal principal) => Read(principal, SdkKeyId);

    private static Guid Read(ClaimsPrincipal principal, string type)
    {
        ArgumentNullException.ThrowIfNull(principal);
        return Guid.Parse(principal.FindFirstValue(type) ?? throw new InvalidOperationException($"The principal has no {type} claim."));
    }
}
