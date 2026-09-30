using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FlagForge.Application.Common;
using FlagForge.Application.Environments;
using FlagForge.Application.Flags;
using FlagForge.Application.Schedules;
using FlagForge.Domain;
using FlagForge.Testing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FlagForge.ManagementApi.IntegrationTests;

public sealed class FlagTests(FlagForgeFixture fixture) : ManagementApiTest(fixture)
{
    [Fact]
    public async Task Creating_a_flag_creates_a_default_config_in_every_environment()
    {
        var admin = await AsAsync(Role.Admin);
        var project = await admin.CreateProjectAsync(cancellationToken: Ct);

        var response = await admin.Client.PostJsonAsync(
            $"/api/v1/projects/{project.Key}/flags", new CreateFlagRequest("new-checkout", "New checkout", FlagType.Boolean, Tags: ["checkout"]), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Headers.Location!.ToString().ShouldBe($"/api/v1/projects/{project.Key}/flags/new-checkout");
        var flag = await response.ReadJsonAsync<FlagResponse>(Ct);
        flag.Variations.Select(v => v.Id).ShouldBe(["true", "false"]);
        flag.Environments.Select(e => e.EnvironmentKey).ShouldBe([Development, Staging, Production]);
        flag.Environments.ShouldAllBe(e => !e.Config.Enabled && e.Config.OffVariationId == "false" && e.Config.Fallthrough.VariationId == "true" && e.Config.Version == 1);
        await using var db = Fixture.CreateDbContext();
        (await db.FlagEnvironmentConfigs.CountAsync(c => c.FlagId == flag.Id, Ct)).ShouldBe(3);
    }

    [Fact]
    public async Task Creating_an_environment_creates_a_config_for_every_flag()
    {
        var admin = await AsAsync(Role.Admin);
        var project = await admin.CreateProjectAsync(cancellationToken: Ct);
        var first = await admin.CreateFlagAsync(project.Key, cancellationToken: Ct);
        var second = await admin.CreateFlagAsync(project.Key, type: FlagType.String, variations: ManagementApiDriver.StringVariations("a", "b", "c"), cancellationToken: Ct);

        var environment = await (await admin.Client.PostJsonAsync(
                $"/api/v1/projects/{project.Key}/environments", new CreateEnvironmentRequest("qa", "QA", "#2f8f83"), Ct))
            .ReadJsonAsync<EnvironmentResponse>(Ct);

        environment.Color.ShouldBe("#2F8F83");
        environment.SortOrder.ShouldBe(3);
        await using var db = Fixture.CreateDbContext();
        var configs = await db.FlagEnvironmentConfigs.Where(c => c.EnvironmentId == environment.Id).ToListAsync(Ct);
        configs.Select(c => c.FlagId).ShouldBe([first.Id, second.Id], ignoreOrder: true);
        var stringConfig = configs.Single(c => c.FlagId == second.Id);
        stringConfig.OffVariationId.ShouldBe(second.Variations[^1].Id);
        stringConfig.Fallthrough.VariationId.ShouldBe(second.Variations[0].Id);
    }

    [Fact]
    public async Task Non_boolean_variations_get_generated_ids_and_are_validated()
    {
        var admin = await AsAsync(Role.Admin);
        var project = await admin.CreateProjectAsync(cancellationToken: Ct);
        var url = $"/api/v1/projects/{project.Key}/flags";

        var flag = await admin.CreateFlagAsync(project.Key, type: FlagType.Number, variations:
        [
            new VariationInput("Three", JsonSerializer.SerializeToElement(3)),
            new VariationInput("Five", JsonSerializer.SerializeToElement(5)),
        ], cancellationToken: Ct);
        var booleanWithVariations = await admin.Client.PostJsonAsync(url, new CreateFlagRequest("b1", "B", FlagType.Boolean, Variations: ManagementApiDriver.StringVariations("x", "y")), Ct);
        var stringWithoutVariations = await admin.Client.PostJsonAsync(url, new CreateFlagRequest("s1", "S", FlagType.String), Ct);
        var wrongType = await admin.Client.PostJsonAsync(url, new CreateFlagRequest("n1", "N", FlagType.Number, Variations:
        [
            new VariationInput("One", JsonSerializer.SerializeToElement(1)),
            new VariationInput("Text", JsonSerializer.SerializeToElement("two")),
        ]), Ct);
        var jsonPrimitive = await admin.Client.PostJsonAsync(url, new CreateFlagRequest("j1", "J", FlagType.Json, Variations:
        [
            new VariationInput("Object", JsonSerializer.SerializeToElement(new { a = 1 })),
            new VariationInput("Number", JsonSerializer.SerializeToElement(2)),
        ]), Ct);
        var badKey = await admin.Client.PostJsonAsync(url, new CreateFlagRequest("Bad Key", "K", FlagType.Boolean), Ct);

        flag.Variations.ShouldAllBe(v => v.Id.StartsWith("v_") && v.Id.Length == 8);
        (await Errors(booleanWithVariations)).ShouldContainKey("variations");
        (await Errors(stringWithoutVariations)).ShouldContainKey("variations");
        (await Errors(wrongType)).ShouldContainKey("variations[1].value");
        (await Errors(jsonPrimitive)).ShouldContainKey("variations[1].value");
        (await Errors(badKey)).ShouldContainKey("key");
    }

    [Fact]
    public async Task Duplicate_flag_keys_conflict()
    {
        var admin = await AsAsync(Role.Admin);
        var project = await admin.CreateProjectAsync(cancellationToken: Ct);
        await admin.CreateFlagAsync(project.Key, "dup", cancellationToken: Ct);

        var duplicate = await admin.Client.PostJsonAsync($"/api/v1/projects/{project.Key}/flags", new CreateFlagRequest("dup", "Again", FlagType.Boolean), Ct);

        duplicate.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Flag_list_filters_by_search_tag_and_archived_and_pages()
    {
        var admin = await AsAsync(Role.Admin);
        var project = await admin.CreateProjectAsync(cancellationToken: Ct);
        await admin.CreateFlagAsync(project.Key, "checkout-v2", tags: ["checkout"], cancellationToken: Ct);
        await admin.CreateFlagAsync(project.Key, "search-v2", tags: ["search"], cancellationToken: Ct);
        await admin.CreateFlagAsync(project.Key, "old-banner", tags: ["checkout"], cancellationToken: Ct);
        await admin.ArchiveFlagAsync(project.Key, "old-banner", Ct);

        var all = await List(admin, project.Key, "");
        var archivedToo = await List(admin, project.Key, "includeArchived=true");
        var bySearch = await List(admin, project.Key, "search=V2");
        var byTag = await List(admin, project.Key, "tag=checkout&includeArchived=true");
        var paged = await List(admin, project.Key, "pageSize=1&page=2");

        all.TotalCount.ShouldBe(2);
        archivedToo.TotalCount.ShouldBe(3);
        bySearch.Items.Select(f => f.Key).ShouldBe(["checkout-v2", "search-v2"], ignoreOrder: true);
        byTag.Items.Select(f => f.Key).ShouldBe(["checkout-v2", "old-banner"], ignoreOrder: true);
        paged.Items.Count.ShouldBe(1);
        paged.TotalCount.ShouldBe(2);
        all.Items[0].Environments.Select(e => e.EnvironmentKey).ShouldBe([Development, Staging, Production]);
    }

    [Fact]
    public async Task Replacing_variations_keeps_ids_and_bumps_versions_only_when_values_change()
    {
        var admin = await AsAsync(Role.Admin);
        var project = await admin.CreateProjectAsync(cancellationToken: Ct);
        var flag = await admin.CreateFlagAsync(project.Key, type: FlagType.String, variations: ManagementApiDriver.StringVariations("red", "green"), cancellationToken: Ct);
        var url = $"/api/v1/projects/{project.Key}/flags/{flag.Key}/variations";
        var versionsBefore = await ConfigVersions(project.Id);

        var renamed = await (await admin.Client.PutJsonAsync(url, new ReplaceVariationsRequest(
        [
            new VariationInput("Red!", JsonSerializer.SerializeToElement("red"), flag.Variations[0].Id),
            new VariationInput("Green", JsonSerializer.SerializeToElement("green"), flag.Variations[1].Id),
            new VariationInput("Blue", JsonSerializer.SerializeToElement("blue")),
        ]), Ct)).ReadJsonAsync<FlagResponse>(Ct);
        var versionsAfterRename = await ConfigVersions(project.Id);
        var revalued = await (await admin.Client.PutJsonAsync(url, new ReplaceVariationsRequest(
        [
            new VariationInput("Red!", JsonSerializer.SerializeToElement("crimson"), flag.Variations[0].Id),
            new VariationInput("Green", JsonSerializer.SerializeToElement("green"), flag.Variations[1].Id),
            new VariationInput("Blue", JsonSerializer.SerializeToElement("blue"), renamed.Variations[2].Id),
        ]), Ct)).ReadJsonAsync<FlagResponse>(Ct);
        var versionsAfterRevalue = await ConfigVersions(project.Id);

        renamed.Variations[0].Id.ShouldBe(flag.Variations[0].Id);
        renamed.Variations[2].Id.ShouldStartWith("v_");
        versionsAfterRename.ShouldBe(versionsBefore);
        revalued.Variations[0].Value.GetString().ShouldBe("crimson");
        versionsAfterRevalue.ShouldBe([.. versionsBefore.Select(v => v + 1)]);
    }

    [Fact]
    public async Task A_variation_still_served_somewhere_cannot_be_removed()
    {
        var admin = await AsAsync(Role.Admin);
        var project = await admin.CreateProjectAsync(cancellationToken: Ct);
        var flag = await admin.CreateFlagAsync(project.Key, type: FlagType.String, variations: ManagementApiDriver.StringVariations("a", "b", "c"), cancellationToken: Ct);

        // Every environment defaults to serving the first variation and falling back to the last one.
        var response = await admin.Client.PutJsonAsync($"/api/v1/projects/{project.Key}/flags/{flag.Key}/variations", new ReplaceVariationsRequest(
        [
            new VariationInput("b", JsonSerializer.SerializeToElement("b"), flag.Variations[1].Id),
            new VariationInput("c", JsonSerializer.SerializeToElement("c"), flag.Variations[2].Id),
        ]), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(Ct))!.Detail!.ShouldContain("Development");
    }

    [Fact]
    public async Task Boolean_variations_cannot_be_replaced()
    {
        var admin = await AsAsync(Role.Admin);
        var project = await admin.CreateProjectAsync(cancellationToken: Ct);
        var flag = await admin.CreateFlagAsync(project.Key, cancellationToken: Ct);

        var response = await admin.Client.PutJsonAsync($"/api/v1/projects/{project.Key}/flags/{flag.Key}/variations",
            new ReplaceVariationsRequest(ManagementApiDriver.StringVariations("x", "y")), Ct);

        (await Errors(response)).ShouldContainKey("variations");
    }

    [Fact]
    public async Task Archiving_cancels_pending_schedules_and_restoring_brings_the_flag_back()
    {
        var admin = await AsAsync(Role.Admin);
        var project = await admin.CreateProjectAsync(cancellationToken: Ct);
        var flag = await admin.CreateFlagAsync(project.Key, cancellationToken: Ct);
        await admin.Client.PostJsonAsync($"{ManagementApiDriver.TargetingUrl(project.Key, flag.Key, Development)}/scheduled-changes",
            new CreateScheduledChangeRequest(Fixture.Time.GetUtcNow().AddDays(1), ScheduledChangeAction.TurnOn), Ct);
        var versionsBefore = await ConfigVersions(project.Id);

        var archived = await admin.ArchiveFlagAsync(project.Key, flag.Key, Ct);
        var restored = await (await admin.Client.PostAsync($"/api/v1/projects/{project.Key}/flags/{flag.Key}/restore", null, Ct)).ReadJsonAsync<FlagResponse>(Ct);

        archived.IsArchived.ShouldBeTrue();
        archived.ArchivedAt.ShouldNotBeNull();
        restored.IsArchived.ShouldBeFalse();
        await using var db = Fixture.CreateDbContext();
        (await db.ScheduledChanges.SingleAsync(s => s.FlagId == flag.Id, Ct)).Status.ShouldBe(ScheduledChangeStatus.Cancelled);
        (await ConfigVersions(project.Id)).ShouldBe([.. versionsBefore.Select(v => v + 2)]);
    }

    [Fact]
    public async Task Deleting_requires_an_archived_flag_and_the_typed_key()
    {
        var admin = await AsAsync(Role.Admin);
        var project = await admin.CreateProjectAsync(cancellationToken: Ct);
        var flag = await admin.CreateFlagAsync(project.Key, cancellationToken: Ct);
        var url = $"/api/v1/projects/{project.Key}/flags/{flag.Key}";

        var notArchived = await admin.Client.DeleteJsonAsync(url, new ConfirmKeyRequest(flag.Key), Ct);
        await admin.ArchiveFlagAsync(project.Key, flag.Key, Ct);
        var wrongKey = await admin.Client.DeleteJsonAsync(url, new ConfirmKeyRequest("something-else"), Ct);
        var deleted = await admin.Client.DeleteJsonAsync(url, new ConfirmKeyRequest(flag.Key), Ct);

        notArchived.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await Errors(wrongKey)).ShouldContainKey("confirmKey");
        deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await admin.Client.GetAsync(url, Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        await using var db = Fixture.CreateDbContext();
        (await db.FlagEnvironmentConfigs.AnyAsync(c => c.FlagId == flag.Id, Ct)).ShouldBeFalse();
        (await db.AuditEntries.AnyAsync(a => a.FlagId == flag.Id && a.Action == AuditActions.FlagDeleted, Ct)).ShouldBeTrue();
    }

    [Fact]
    public async Task Updating_a_flag_changes_metadata_without_touching_evaluation()
    {
        var admin = await AsAsync(Role.Admin);
        var project = await admin.CreateProjectAsync(cancellationToken: Ct);
        var flag = await admin.CreateFlagAsync(project.Key, cancellationToken: Ct);
        var versionsBefore = await ConfigVersions(project.Id);

        var updated = await (await admin.Client.PatchJsonAsync($"/api/v1/projects/{project.Key}/flags/{flag.Key}",
            new UpdateFlagRequest("Renamed", "Now with a description", ["a", "a", " b "], IsPermanent: true), Ct)).ReadJsonAsync<FlagResponse>(Ct);

        updated.Name.ShouldBe("Renamed");
        updated.Description.ShouldBe("Now with a description");
        updated.Tags.ShouldBe(["a", "b"]);
        updated.IsPermanent.ShouldBeTrue();
        (await ConfigVersions(project.Id)).ShouldBe(versionsBefore);
    }

    private static async Task<PagedResult<FlagSummaryResponse>> List(ManagementApiDriver driver, string projectKey, string query) =>
        await (await driver.Client.GetAsync($"/api/v1/projects/{projectKey}/flags?{query}", Ct)).ReadJsonAsync<PagedResult<FlagSummaryResponse>>(Ct);

    private async Task<List<long>> ConfigVersions(Guid projectId)
    {
        await using var db = Fixture.CreateDbContext();
        return await db.Environments.Where(e => e.ProjectId == projectId).OrderBy(e => e.SortOrder).Select(e => e.ConfigVersion).ToListAsync(Ct);
    }

    private static async Task<IDictionary<string, string[]>> Errors(HttpResponseMessage response)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync(Ct));
        return (await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(Ct))!.Errors;
    }
}
