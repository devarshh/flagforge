using System.Net;
using FlagForge.Application.Auth;
using FlagForge.Application.Common;
using FlagForge.Application.Users;
using FlagForge.Domain;
using FlagForge.Testing;
using Microsoft.EntityFrameworkCore;

namespace FlagForge.ManagementApi.IntegrationTests;

public sealed class UserTests(FlagForgeFixture fixture) : ManagementApiTest(fixture)
{
    [Fact]
    public async Task Creating_a_user_returns_a_temporary_password_once()
    {
        var admin = await AsAsync(Role.Admin);

        var response = await admin.Client.PostJsonAsync("/api/v1/users", new CreateUserRequest("Sam@Example.com", "Sam", Role.Editor), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await response.ReadJsonAsync<CreateUserResponse>(Ct);
        created.User.Email.ShouldBe("sam@example.com");
        created.User.MustChangePassword.ShouldBeTrue();
        created.TemporaryPassword.Length.ShouldBe(16);
        response.Headers.Location!.ToString().ShouldBe($"/api/v1/users/{created.User.Id}");
        var fetched = await (await admin.Client.GetAsync($"/api/v1/users/{created.User.Id}", Ct)).Content.ReadAsStringAsync(Ct);
        fetched.ShouldNotContain(created.TemporaryPassword);
        (await admin.Client.PostJsonAsync("/api/v1/users", new CreateUserRequest("sam@example.com", "Sam again", Role.Viewer), Ct))
            .StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task The_last_active_admin_cannot_be_demoted_or_deactivated()
    {
        var admin = await AsAsync(Role.Admin);
        var url = $"/api/v1/users/{admin.User.Id}";

        var demote = await admin.Client.PatchJsonAsync(url, new UpdateUserRequest(Role: Role.Editor), Ct);
        var deactivate = await admin.Client.PatchJsonAsync(url, new UpdateUserRequest(IsActive: false), Ct);
        await Fixture.CreateAsync(Role.Admin, cancellationToken: Ct);
        var demoteWithAnotherAdmin = await admin.Client.PatchJsonAsync(url, new UpdateUserRequest(Role: Role.Editor), Ct);

        demote.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        deactivate.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        demoteWithAnotherAdmin.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Deactivated_users_cannot_sign_in_and_lose_their_sessions()
    {
        var admin = await AsAsync(Role.Admin);
        var editor = await Fixture.CreateAsync(Role.Editor, cancellationToken: Ct);
        using var client = AnonymousClient();
        var session = await client.PostJsonAsync("/api/v1/auth/login", new LoginRequest(editor.Email, TestAuth.Password), Ct);
        var refreshToken = Cookies.RefreshToken(session);

        var updated = await (await admin.Client.PatchJsonAsync($"/api/v1/users/{editor.Id}", new UpdateUserRequest(IsActive: false), Ct))
            .ReadJsonAsync<UserResponse>(Ct);

        updated.IsActive.ShouldBeFalse();
        (await client.PostJsonAsync("/api/v1/auth/login", new LoginRequest(editor.Email, TestAuth.Password), Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await client.SendAsync(Cookies.WithRefreshCookie(HttpMethod.Post, "/api/v1/auth/refresh", refreshToken), Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        await using var db = Fixture.CreateDbContext();
        (await db.AuditEntries.SingleAsync(a => a.Action == AuditActions.UserDeactivated, Ct)).ResourceKey.ShouldBe($"users/{editor.Email}");
    }

    [Fact]
    public async Task Resetting_a_password_issues_a_new_temporary_password()
    {
        var admin = await AsAsync(Role.Admin);
        var viewer = await Fixture.CreateAsync(Role.Viewer, cancellationToken: Ct);
        using var client = AnonymousClient();

        var reset = await (await admin.Client.PostAsync($"/api/v1/users/{viewer.Id}/reset-password", null, Ct)).ReadJsonAsync<ResetPasswordResponse>(Ct);

        (await client.PostJsonAsync("/api/v1/auth/login", new LoginRequest(viewer.Email, TestAuth.Password), Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var login = await (await client.PostJsonAsync("/api/v1/auth/login", new LoginRequest(viewer.Email, reset.TemporaryPassword), Ct)).ReadJsonAsync<LoginResponse>(Ct);
        login.User.MustChangePassword.ShouldBeTrue();
    }

    [Fact]
    public async Task Users_are_listed_in_pages()
    {
        var admin = await AsAsync(Role.Admin);
        await Fixture.CreateAsync(Role.Viewer, cancellationToken: Ct);
        await Fixture.CreateAsync(Role.Editor, cancellationToken: Ct);

        var page = await (await admin.Client.GetAsync("/api/v1/users?page=1&pageSize=2", Ct)).ReadJsonAsync<PagedResult<UserResponse>>(Ct);
        var invalid = await admin.Client.GetAsync("/api/v1/users?pageSize=500", Ct);

        page.Items.Count.ShouldBe(2);
        page.TotalCount.ShouldBe(3);
        invalid.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
