using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FlagForge.Application.Environments;
using FlagForge.Application.Flags;
using FlagForge.Application.Projects;
using FlagForge.Application.Schedules;
using FlagForge.Application.SdkKeys;
using FlagForge.Application.Targeting;
using FlagForge.Application.Users;
using FlagForge.Domain;
using FlagForge.Evaluation;
using FlagForge.Testing;
using Microsoft.AspNetCore.Mvc;

namespace FlagForge.ManagementApi.IntegrationTests;

/// <summary>The RBAC matrix: Viewer reads, Editor edits non-protected environments, Admin does everything.</summary>
public sealed class AuthorizationTests(FlagForgeFixture fixture) : ManagementApiTest(fixture)
{
    private string _project = string.Empty;
    private string _flag = string.Empty;

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        var admin = await AsAsync(Role.Admin);
        _project = (await admin.CreateProjectAsync(cancellationToken: Ct)).Key;
        _flag = (await admin.CreateFlagAsync(_project, cancellationToken: Ct)).Key;
    }

    public static TheoryData<string> Mutations => new()
    {
        "create project",
        "create environment",
        "create sdk key",
        "create flag",
        "update flag",
        "update targeting",
        "toggle",
        "schedule change",
        "archive flag",
        "create user",
    };

    [Theory]
    [MemberData(nameof(Mutations))]
    public async Task Viewer_is_forbidden_to_change_anything(string mutation)
    {
        var viewer = await AsAsync(Role.Viewer);

        var response = await SendAsync(viewer.Client, mutation, Development);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Viewer_can_read_and_preview()
    {
        var viewer = await AsAsync(Role.Viewer);
        var targeting = ManagementApiDriver.TargetingUrl(_project, _flag, Production);

        (await viewer.Client.GetAsync("/api/v1/projects", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await viewer.Client.GetAsync($"/api/v1/projects/{_project}/flags", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await viewer.Client.GetAsync(targeting, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await viewer.Client.GetAsync("/api/v1/audit", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        var preview = await viewer.Client.PostJsonAsync($"{targeting}/evaluate-preview", new PreviewRequest(JsonSerializer.SerializeToElement(new { key = "u1" })), Ct);
        preview.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("update targeting")]
    [InlineData("toggle")]
    [InlineData("schedule change")]
    public async Task Editor_is_forbidden_to_change_a_protected_environment(string mutation)
    {
        var editor = await AsAsync(Role.Editor);

        var response = await SendAsync(editor.Client, mutation, Production, comment: "Editors cannot do this even with a comment");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(Ct))!.Detail!.ShouldContain("protected");
    }

    [Theory]
    [InlineData("update targeting", Development)]
    [InlineData("toggle", Staging)]
    [InlineData("schedule change", Development)]
    [InlineData("create flag", Development)]
    [InlineData("update flag", Development)]
    [InlineData("archive flag", Development)]
    public async Task Editor_can_change_flags_in_unprotected_environments(string mutation, string environment)
    {
        var editor = await AsAsync(Role.Editor);

        var response = await SendAsync(editor.Client, mutation, environment);

        response.IsSuccessStatusCode.ShouldBeTrue(await response.Content.ReadAsStringAsync(Ct));
    }

    [Theory]
    [InlineData("create project")]
    [InlineData("create environment")]
    [InlineData("create sdk key")]
    [InlineData("create user")]
    public async Task Editor_is_forbidden_to_use_admin_endpoints(string mutation)
    {
        var editor = await AsAsync(Role.Editor);

        (await SendAsync(editor.Client, mutation, Development)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("update targeting")]
    [InlineData("toggle")]
    [InlineData("schedule change")]
    public async Task Admin_can_change_a_protected_environment_with_a_comment(string mutation)
    {
        var admin = await AsAsync(Role.Admin);

        var response = await SendAsync(admin.Client, mutation, Production, comment: "Approved in the release review");

        response.IsSuccessStatusCode.ShouldBeTrue(await response.Content.ReadAsStringAsync(Ct));
    }

    [Theory]
    [InlineData("update targeting")]
    [InlineData("toggle")]
    public async Task Protected_environment_changes_require_a_comment(string mutation)
    {
        var admin = await AsAsync(Role.Admin);

        var response = await SendAsync(admin.Client, mutation, Production);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(Ct))!.Errors.ShouldContainKey("comment");
    }

    [Fact]
    public async Task Anonymous_requests_are_unauthorized_except_meta()
    {
        using var client = AnonymousClient();

        (await client.GetAsync("/api/v1/projects", Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await client.GetAsync("/api/v1/audit", Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var meta = await client.GetAsync("/api/v1/meta", Ct);
        meta.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await meta.Content.ReadAsStringAsync(Ct)).ShouldContain("\"version\"");
    }

    [Fact]
    public async Task Unauthorized_responses_are_problem_details()
    {
        using var client = AnonymousClient();

        var response = await client.GetAsync("/api/v1/projects", Ct);

        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
    }

    private async Task<HttpResponseMessage> SendAsync(HttpClient client, string mutation, string environment, string? comment = null)
    {
        var targeting = ManagementApiDriver.TargetingUrl(_project, _flag, environment);
        return mutation switch
        {
            "create project" => await client.PostJsonAsync("/api/v1/projects", new CreateProjectRequest(ManagementApiDriver.UniqueKey("p"), "P"), Ct),
            "create environment" => await client.PostJsonAsync($"/api/v1/projects/{_project}/environments", new CreateEnvironmentRequest("qa", "QA", "#2F8F83"), Ct),
            "create sdk key" => await client.PostJsonAsync($"/api/v1/projects/{_project}/environments/{environment}/sdk-keys", new CreateSdkKeyRequest("App"), Ct),
            "create flag" => await client.PostJsonAsync($"/api/v1/projects/{_project}/flags", new CreateFlagRequest(ManagementApiDriver.UniqueKey("f"), "F", FlagType.Boolean), Ct),
            "update flag" => await client.PatchJsonAsync($"/api/v1/projects/{_project}/flags/{_flag}", new UpdateFlagRequest(Name: "Renamed"), Ct),
            "update targeting" => await client.PutJsonAsync(targeting, new UpdateTargetingRequest(OnConfig, 1, comment), Ct),
            "toggle" => await client.PostJsonAsync($"{targeting}/toggle", new ToggleRequest(true, Comment: comment), Ct),
            "schedule change" => await client.PostJsonAsync(
                $"{targeting}/scheduled-changes", new CreateScheduledChangeRequest(Fixture.Time.GetUtcNow().AddHours(1), ScheduledChangeAction.TurnOn), Ct),
            "archive flag" => await client.PostAsync($"/api/v1/projects/{_project}/flags/{_flag}/archive", null, Ct),
            "create user" => await client.PostJsonAsync("/api/v1/users", new CreateUserRequest($"{Guid.NewGuid():N}@test.local", "Someone", Role.Viewer), Ct),
            _ => throw new ArgumentOutOfRangeException(nameof(mutation), mutation, "Unknown mutation."),
        };
    }

    private static TargetingConfig OnConfig => new()
    {
        Enabled = true,
        OffVariationId = Variation.FalseId,
        Fallthrough = Serve.Variation(Variation.TrueId),
    };
}
