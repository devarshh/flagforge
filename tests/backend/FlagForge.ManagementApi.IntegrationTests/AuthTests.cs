using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FlagForge.Application.Auth;
using FlagForge.Application.Users;
using FlagForge.Domain;
using FlagForge.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace FlagForge.ManagementApi.IntegrationTests;

public sealed class AuthTests(FlagForgeFixture fixture) : ManagementApiTest(fixture)
{
    private const string LoginUrl = "/api/v1/auth/login";
    private const string RefreshUrl = "/api/v1/auth/refresh";

    [Fact]
    public async Task Login_returns_an_access_token_and_sets_a_secure_refresh_cookie()
    {
        var user = await Fixture.CreateAsync(Role.Editor, cancellationToken: Ct);
        using var client = AnonymousClient();

        var response = await client.PostJsonAsync(LoginUrl, new LoginRequest(user.Email.ToUpperInvariant(), TestAuth.Password), Ct);

        var body = await response.ReadJsonAsync<LoginResponse>(Ct);
        body.User.Id.ShouldBe(user.Id);
        body.User.Role.ShouldBe(Role.Editor);
        body.ExpiresAt.ShouldBe(Fixture.Time.GetUtcNow().AddMinutes(15), TimeSpan.FromSeconds(5));
        var cookie = Cookies.SetCookieHeader(response)!.ToLowerInvariant();
        cookie.ShouldContain("path=/api/v1/auth");
        cookie.ShouldContain("httponly");
        cookie.ShouldContain("samesite=strict");
        cookie.ShouldContain("secure");

        using var me = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        me.Headers.Authorization = new AuthenticationHeaderValue("Bearer", body.AccessToken);
        var current = await (await client.SendAsync(me, Ct)).ReadJsonAsync<UserResponse>(Ct);
        current.Email.ShouldBe(user.Email);
    }

    [Fact]
    public async Task Unknown_email_and_wrong_password_get_the_same_generic_error()
    {
        var user = await Fixture.CreateAsync(Role.Viewer, cancellationToken: Ct);
        using var client = AnonymousClient();

        var unknown = await client.PostJsonAsync(LoginUrl, new LoginRequest("nobody@test.local", TestAuth.Password), Ct);
        var wrongPassword = await client.PostJsonAsync(LoginUrl, new LoginRequest(user.Email, "not-the-password"), Ct);

        unknown.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        wrongPassword.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var unknownProblem = await unknown.Content.ReadFromJsonAsync<ProblemDetails>(Ct);
        var wrongProblem = await wrongPassword.Content.ReadFromJsonAsync<ProblemDetails>(Ct);
        unknownProblem!.Detail.ShouldBe(wrongProblem!.Detail);
        Cookies.SetCookieHeader(wrongPassword).ShouldBeNull();
    }

    [Fact]
    public async Task Five_failures_lock_the_account_for_fifteen_minutes()
    {
        var user = await Fixture.CreateAsync(Role.Editor, cancellationToken: Ct);
        using var client = AnonymousClient();
        for (var attempt = 0; attempt < 5; attempt++)
        {
            (await client.PostJsonAsync(LoginUrl, new LoginRequest(user.Email, "wrong-password-123"), Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        var whileLocked = await client.PostJsonAsync(LoginUrl, new LoginRequest(user.Email, TestAuth.Password), Ct);
        whileLocked.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        Fixture.Time.Advance(TimeSpan.FromMinutes(15) + TimeSpan.FromSeconds(1));
        var afterLockout = await client.PostJsonAsync(LoginUrl, new LoginRequest(user.Email, TestAuth.Password), Ct);
        afterLockout.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Refresh_rotates_the_refresh_token()
    {
        using var client = AnonymousClient();
        var (first, _) = await LoginAsync(client);

        var refreshed = await client.SendAsync(Cookies.WithRefreshCookie(HttpMethod.Post, RefreshUrl, first), Ct);

        var session = await refreshed.ReadJsonAsync<LoginResponse>(Ct);
        session.AccessToken.ShouldNotBeNullOrEmpty();
        var second = Cookies.RefreshToken(refreshed);
        second.ShouldNotBeNull();
        second.ShouldNotBe(first);

        await using var db = Fixture.CreateDbContext();
        var original = await db.RefreshTokens.SingleAsync(t => t.TokenHash == Hashing.Sha256Hex(first), Ct);
        original.RevokedAt.ShouldNotBeNull();
        original.ReplacedByTokenHash.ShouldBe(Hashing.Sha256Hex(second));
        (await client.SendAsync(Cookies.WithRefreshCookie(HttpMethod.Post, RefreshUrl, second), Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Reusing_a_rotated_refresh_token_revokes_every_session_of_the_user()
    {
        using var client = AnonymousClient();
        var (first, user) = await LoginAsync(client);
        var second = Cookies.RefreshToken(await client.SendAsync(Cookies.WithRefreshCookie(HttpMethod.Post, RefreshUrl, first), Ct));

        var replay = await client.SendAsync(Cookies.WithRefreshCookie(HttpMethod.Post, RefreshUrl, first), Ct);
        var afterReplay = await client.SendAsync(Cookies.WithRefreshCookie(HttpMethod.Post, RefreshUrl, second), Ct);

        replay.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        Cookies.SetCookieHeader(replay)!.ShouldContain("expires=Thu, 01 Jan 1970", Case.Insensitive);
        afterReplay.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        await using var db = Fixture.CreateDbContext();
        (await db.RefreshTokens.CountAsync(t => t.UserId == user.Id && t.RevokedAt == null, Ct)).ShouldBe(0);
    }

    [Fact]
    public async Task Expired_refresh_token_is_rejected()
    {
        using var client = AnonymousClient();
        var (token, _) = await LoginAsync(client);

        Fixture.Time.Advance(TimeSpan.FromDays(7) + TimeSpan.FromMinutes(1));

        (await client.SendAsync(Cookies.WithRefreshCookie(HttpMethod.Post, RefreshUrl, token), Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_without_a_cookie_is_unauthorized()
    {
        using var client = AnonymousClient();

        (await client.PostAsync(RefreshUrl, null, Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_revokes_the_session_and_clears_the_cookie()
    {
        using var client = AnonymousClient();
        var (token, _) = await LoginAsync(client);

        var logout = await client.SendAsync(Cookies.WithRefreshCookie(HttpMethod.Post, "/api/v1/auth/logout", token), Ct);

        logout.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        Cookies.RefreshToken(logout).ShouldBeNull();
        (await client.SendAsync(Cookies.WithRefreshCookie(HttpMethod.Post, RefreshUrl, token), Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Changing_a_temporary_password_clears_must_change_password()
    {
        var admin = await AsAsync(Role.Admin);
        var created = await (await admin.Client.PostJsonAsync("/api/v1/users", new CreateUserRequest("new.person@test.local", "New Person", Role.Editor), Ct))
            .ReadJsonAsync<CreateUserResponse>(Ct);
        using var client = AnonymousClient();
        var login = await (await client.PostJsonAsync(LoginUrl, new LoginRequest("new.person@test.local", created.TemporaryPassword), Ct)).ReadJsonAsync<LoginResponse>(Ct);
        login.User.MustChangePassword.ShouldBeTrue();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);

        var tooShort = await client.PostJsonAsync("/api/v1/auth/change-password", new ChangePasswordRequest(created.TemporaryPassword, "short"), Ct);
        var wrongCurrent = await client.PostJsonAsync("/api/v1/auth/change-password", new ChangePasswordRequest("not-it-at-all", "a-brand-new-password"), Ct);
        var changed = await (await client.PostJsonAsync("/api/v1/auth/change-password", new ChangePasswordRequest(created.TemporaryPassword, "a-brand-new-password"), Ct))
            .ReadJsonAsync<UserResponse>(Ct);

        (await tooShort.Content.ReadFromJsonAsync<ValidationProblemDetails>(Ct))!.Errors.ShouldContainKey("newPassword");
        (await wrongCurrent.Content.ReadFromJsonAsync<ValidationProblemDetails>(Ct))!.Errors.ShouldContainKey("currentPassword");
        changed.MustChangePassword.ShouldBeFalse();
        (await client.PostJsonAsync(LoginUrl, new LoginRequest("new.person@test.local", created.TemporaryPassword), Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await client.PostJsonAsync(LoginUrl, new LoginRequest("new.person@test.local", "a-brand-new-password"), Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Login_is_rate_limited_per_client_address()
    {
        await using var limited = Api.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration(configuration =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["RateLimiting:LoginPermitsPerMinute"] = "3" })));
        using var client = limited.CreateClient();
        var attempts = new List<HttpResponseMessage>();
        for (var i = 0; i < 4; i++)
        {
            attempts.Add(await client.PostJsonAsync(LoginUrl, new LoginRequest("nobody@test.local", "whatever-password"), Ct));
        }

        attempts.Take(3).ShouldAllBe(r => r.StatusCode == HttpStatusCode.Unauthorized);
        attempts[3].StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        attempts[3].Headers.RetryAfter.ShouldNotBeNull();
    }

    private async Task<(string Token, User User)> LoginAsync(HttpClient client)
    {
        var user = await Fixture.CreateAsync(Role.Editor, cancellationToken: Ct);
        var response = await client.PostJsonAsync(LoginUrl, new LoginRequest(user.Email, TestAuth.Password), Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (Cookies.RefreshToken(response)!, user);
    }
}
