namespace FlagForge.ManagementApi.Authentication;

/// <summary>JWT claim names; inbound claim mapping is off, so these are also the names on the ClaimsPrincipal.</summary>
internal static class TokenClaims
{
    public const string Subject = "sub";
    public const string Email = "email";
    public const string Name = "name";
    public const string Role = "role";
}
