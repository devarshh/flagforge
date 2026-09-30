using System.Net;
using System.Net.Http.Json;
using FlagForge.Application.Common;
using FlagForge.Application.Projects;
using FlagForge.Domain;
using FlagForge.Testing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FlagForge.ManagementApi.IntegrationTests;

public sealed class ProjectTests(FlagForgeFixture fixture) : ManagementApiTest(fixture)
{
    [Fact]
    public async Task New_projects_get_development_staging_and_a_protected_production()
    {
        var admin = await AsAsync(Role.Admin);

        var response = await admin.Client.PostJsonAsync("/api/v1/projects", new CreateProjectRequest("storefront", "Storefront", "Web shop"), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Headers.Location!.ToString().ShouldBe("/api/v1/projects/storefront");
        var project = await response.ReadJsonAsync<ProjectResponse>(Ct);
        project.Environments.Select(e => (e.Key, e.Color, e.IsProtected)).ShouldBe(
        [
            (Development, "#3A7CA5", false),
            (Staging, "#8E6CC0", false),
            (Production, "#C2362B", true),
        ]);
        project.Environments.ShouldAllBe(e => e.ConfigVersion == 1);
    }

    [Fact]
    public async Task Project_keys_are_validated_and_unique()
    {
        var admin = await AsAsync(Role.Admin);
        await admin.CreateProjectAsync("taken", Ct);

        var duplicate = await admin.Client.PostJsonAsync("/api/v1/projects", new CreateProjectRequest("taken", "Again"), Ct);
        var invalid = await admin.Client.PostJsonAsync("/api/v1/projects", new CreateProjectRequest("-Nope", ""), Ct);

        duplicate.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var errors = (await invalid.Content.ReadFromJsonAsync<ValidationProblemDetails>(Ct))!.Errors;
        errors.Keys.ShouldBe(["key", "name"], ignoreOrder: true);
    }

    [Fact]
    public async Task Deleting_a_project_requires_the_typed_key_and_removes_everything()
    {
        var admin = await AsAsync(Role.Admin);
        var project = await admin.CreateProjectAsync(cancellationToken: Ct);
        var flag = await admin.CreateFlagAsync(project.Key, cancellationToken: Ct);
        await admin.CreateSdkKeyAsync(project.Key, Development, Ct);

        var wrongKey = await admin.Client.DeleteJsonAsync($"/api/v1/projects/{project.Key}", new ConfirmKeyRequest("nope"), Ct);
        var deleted = await admin.Client.DeleteJsonAsync($"/api/v1/projects/{project.Key}", new ConfirmKeyRequest(project.Key), Ct);

        wrongKey.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await using var db = Fixture.CreateDbContext();
        (await db.Projects.AnyAsync(Ct)).ShouldBeFalse();
        (await db.Environments.AnyAsync(Ct)).ShouldBeFalse();
        (await db.Flags.AnyAsync(Ct)).ShouldBeFalse();
        (await db.FlagEnvironmentConfigs.AnyAsync(Ct)).ShouldBeFalse();
        (await db.SdkKeys.AnyAsync(Ct)).ShouldBeFalse();
        (await db.AuditEntries.AnyAsync(a => a.Action == AuditActions.ProjectDeleted && a.ProjectId == project.Id, Ct)).ShouldBeTrue();
        (await db.AuditEntries.AnyAsync(a => a.FlagId == flag.Id, Ct)).ShouldBeTrue();
    }

    [Fact]
    public async Task Environments_can_be_updated_and_deleted_but_not_the_last_one()
    {
        var admin = await AsAsync(Role.Admin);
        var project = await admin.CreateProjectAsync(cancellationToken: Ct);
        var environments = $"/api/v1/projects/{project.Key}/environments";

        var renamed = await (await admin.Client.PatchJsonAsync($"{environments}/{Staging}", new Application.Environments.UpdateEnvironmentRequest("QA", "#b86e00", true, 5), Ct))
            .ReadJsonAsync<Application.Environments.EnvironmentResponse>(Ct);
        var deleteStaging = await admin.Client.DeleteJsonAsync($"{environments}/{Staging}", new ConfirmKeyRequest(Staging), Ct);
        var deleteDevelopment = await admin.Client.DeleteJsonAsync($"{environments}/{Development}", new ConfirmKeyRequest(Development), Ct);
        var deleteLast = await admin.Client.DeleteJsonAsync($"{environments}/{Production}", new ConfirmKeyRequest(Production), Ct);

        renamed.Name.ShouldBe("QA");
        renamed.Color.ShouldBe("#B86E00");
        renamed.IsProtected.ShouldBeTrue();
        deleteStaging.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        deleteDevelopment.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        deleteLast.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }
}
