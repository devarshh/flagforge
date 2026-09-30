using System.Security.Claims;
using FlagForge.Application.Auth;
using FlagForge.Application.Common;
using FlagForge.Application.Users;
using FlagForge.ManagementApi.Authentication;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;

namespace FlagForge.ManagementApi.Features.Auth;

/// <summary>
/// Sign-in with a short-lived access token in the response body and a rotating refresh token in an httpOnly,
/// SameSite=Strict cookie scoped to <c>/api/v1/auth</c> (ADR 0008).
/// </summary>
internal static class AuthEndpoints
{
    public const string RefreshCookieName = "ff_refresh";
    public const string RefreshCookiePath = "/api/v1/auth";

    public static RouteGroupBuilder MapAuthEndpoints(this RouteGroupBuilder api)
    {
        var auth = api.MapGroup("/auth").WithTags("Auth");
        auth.MapPost("/login", LoginAsync)
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitingSetup.LoginPolicy)
            .WithName("Login")
            .WithSummary("Sign in; sets the refresh cookie")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);
        auth.MapPost("/refresh", RefreshAsync)
            .AllowAnonymous()
            .WithName("RefreshSession")
            .WithSummary("Rotate the refresh cookie and get a new access token");
        auth.MapPost("/logout", LogoutAsync)
            .AllowAnonymous()
            .WithName("Logout")
            .WithSummary("Revoke the refresh cookie's session and clear the cookie");
        auth.MapGet("/me", GetCurrentUserAsync)
            .RequireAuthorization(Policies.CanRead)
            .WithName("GetCurrentUser")
            .WithSummary("The signed-in user");
        auth.MapPost("/change-password", ChangePasswordAsync)
            .RequireAuthorization(Policies.CanRead)
            .WithName("ChangePassword")
            .WithSummary("Change your password; ends your other sessions")
            .ProducesValidationProblem();
        return api;
    }

    private static async Task<Ok<LoginResponse>> LoginAsync(
        LoginRequest request, AuthService auth, HttpContext http, IOptions<AuthOptions> options, CancellationToken cancellationToken)
    {
        var session = await auth.LoginAsync(request, cancellationToken);
        SetRefreshCookie(http, session, options.Value);
        return TypedResults.Ok(session.Response);
    }

    private static async Task<Results<Ok<LoginResponse>, ProblemHttpResult>> RefreshAsync(
        AuthService auth, HttpContext http, IOptions<AuthOptions> options, CancellationToken cancellationToken)
    {
        try
        {
            var session = await auth.RefreshAsync(http.Request.Cookies[RefreshCookieName], cancellationToken);
            SetRefreshCookie(http, session, options.Value);
            return TypedResults.Ok(session.Response);
        }
        catch (UnauthorizedException ex)
        {
            // Returned rather than thrown so the cookie deletion survives (the exception handler clears headers).
            DeleteRefreshCookie(http, options.Value);
            return TypedResults.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Sign-in required.", detail: ex.Message);
        }
    }

    private static async Task<NoContent> LogoutAsync(AuthService auth, HttpContext http, IOptions<AuthOptions> options, CancellationToken cancellationToken)
    {
        await auth.LogoutAsync(http.Request.Cookies[RefreshCookieName], cancellationToken);
        DeleteRefreshCookie(http, options.Value);
        return TypedResults.NoContent();
    }

    private static async Task<Ok<UserResponse>> GetCurrentUserAsync(AuthService auth, ClaimsPrincipal user, CancellationToken cancellationToken) =>
        TypedResults.Ok(await auth.GetCurrentUserAsync(user.GetUserId(), cancellationToken));

    private static async Task<Ok<UserResponse>> ChangePasswordAsync(
        ChangePasswordRequest request, AuthService auth, ClaimsPrincipal user, HttpContext http, CancellationToken cancellationToken) =>
        TypedResults.Ok(await auth.ChangePasswordAsync(user.GetUserId(), request, http.Request.Cookies[RefreshCookieName], cancellationToken));

    private static void SetRefreshCookie(HttpContext http, AuthSession session, AuthOptions options) =>
        http.Response.Cookies.Append(RefreshCookieName, session.RefreshToken, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Strict,
            Secure = options.RefreshCookieSecure,
            Path = RefreshCookiePath,
            Expires = session.RefreshTokenExpiresAt,
            IsEssential = true,
        });

    private static void DeleteRefreshCookie(HttpContext http, AuthOptions options) =>
        http.Response.Cookies.Delete(RefreshCookieName, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Strict,
            Secure = options.RefreshCookieSecure,
            Path = RefreshCookiePath,
        });
}
