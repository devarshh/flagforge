using System.Security.Claims;
using System.Text.Encodings.Web;
using FlagForge.Application.Common;
using FlagForge.Domain;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FlagForge.EvaluationApi.Authentication;

/// <summary>
/// Authenticates <c>Authorization: Bearer ffk_...</c>, or the <c>access_token</c> query parameter for hub requests
/// only (browsers cannot set headers on WebSockets). Keys are looked up by SHA-256 hash, cache first. Plaintext keys
/// are never logged.
/// </summary>
public sealed class SdkKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, SdkKeyCache cache)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "SdkKey";
    public const string HubPathPrefix = "/sdk/hubs";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var key = ReadKey();
        if (key is null)
        {
            return AuthenticateResult.NoResult();
        }

        if (!SdkKeyFormat.LooksLikeSdkKey(key))
        {
            return AuthenticateResult.Fail("The credential is not an SDK key.");
        }

        var keyHash = Hashing.Sha256Hex(key);
        if (!cache.TryGet(keyHash, out var identity))
        {
            var db = Context.RequestServices.GetRequiredService<IFlagForgeDbContext>();
            var found = await db.SdkKeys.AsNoTracking()
                .Where(k => k.KeyHash == keyHash && k.RevokedAt == null)
                .Select(k => new SdkKeyIdentity(k.Id, k.EnvironmentId, k.Environment.ProjectId))
                .FirstOrDefaultAsync(Context.RequestAborted);
            if (found is null)
            {
                return AuthenticateResult.Fail("Unknown or revoked SDK key.");
            }

            cache.Set(keyHash, found);
            identity = found;
        }

        Claim[] claims =
        [
            new(SdkClaims.SdkKeyId, identity.SdkKeyId.ToString()),
            new(SdkClaims.EnvironmentId, identity.EnvironmentId.ToString()),
            new(SdkClaims.ProjectId, identity.ProjectId.ToString()),
        ];
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName)), SchemeName));
    }

    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        await Context.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new ProblemDetailsContext
        {
            HttpContext = Context,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "SDK key required.",
                Detail = "Send a valid SDK key as 'Authorization: Bearer ffk_...'. Revoked keys stop working within seconds.",
            },
        });
    }

    private string? ReadKey()
    {
        var header = Request.Headers.Authorization.ToString();
        if (header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return header["Bearer ".Length..].Trim();
        }

        if (Request.Path.StartsWithSegments(HubPathPrefix))
        {
            var token = Request.Query["access_token"].ToString();
            return token.Length > 0 ? token : null;
        }

        return null;
    }
}
