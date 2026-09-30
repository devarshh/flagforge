using System.Net;
using FlagForge.Application.Audit;
using FlagForge.Application.Common;
using FlagForge.Domain;
using FlagForge.Testing;

namespace FlagForge.ManagementApi.IntegrationTests;

public sealed class AuditTests(FlagForgeFixture fixture) : ManagementApiTest(fixture)
{
    [Fact]
    public async Task Audit_log_is_newest_first_and_filterable()
    {
        var admin = await AsAsync(Role.Admin);
        var editor = await AsAsync(Role.Editor);
        var first = await admin.CreateProjectAsync("first-project", Ct);
        await admin.CreateProjectAsync("second-project", Ct);
        await admin.CreateFlagAsync(first.Key, "checkout", cancellationToken: Ct);
        await admin.CreateFlagAsync("second-project", "search", cancellationToken: Ct);
        Fixture.Time.Advance(TimeSpan.FromSeconds(1));
        await editor.ToggleAsync(first.Key, "checkout", Development, true, cancellationToken: Ct);

        var all = await Query(admin, "");
        var byProject = await Query(admin, "projectKey=first-project");
        var byFlag = await Query(admin, "projectKey=first-project&flagKey=checkout");
        var byEnvironment = await Query(admin, "projectKey=first-project&environmentKey=development");
        var byAction = await Query(admin, "action=flag.toggled");
        var byActor = await Query(admin, $"actorId={editor.User.Id}");
        var unknownProject = await Query(admin, "projectKey=never-existed");
        var paged = await Query(admin, "pageSize=2&page=2");

        all.Items[0].Action.ShouldBe(AuditActions.FlagToggled);
        all.Items.Select(e => e.OccurredAt).ShouldBeInOrder(SortDirection.Descending);
        byProject.Items.ShouldAllBe(e => e.ProjectId == first.Id);
        byProject.Items.Select(e => e.Action).ShouldBe([AuditActions.FlagToggled, AuditActions.FlagCreated, AuditActions.ProjectCreated]);
        byFlag.Items.Select(e => e.Action).ShouldBe([AuditActions.FlagToggled, AuditActions.FlagCreated]);
        byEnvironment.Items.ShouldHaveSingleItem().ResourceKey.ShouldBe("first-project/checkout@development");
        byAction.TotalCount.ShouldBe(1);
        byActor.Items.ShouldAllBe(e => e.ActorId == editor.User.Id);
        byActor.Items[0].After!.Value.GetProperty("enabled").GetBoolean().ShouldBeTrue();
        unknownProject.TotalCount.ShouldBe(0);
        paged.Items.Count.ShouldBe(2);
        paged.TotalCount.ShouldBe(all.TotalCount);
    }

    [Theory]
    [InlineData("flagKey=checkout")]
    [InlineData("environmentKey=development")]
    [InlineData("action=flag.exploded")]
    [InlineData("from=2026-02-01T00:00:00Z&to=2026-01-01T00:00:00Z")]
    public async Task Invalid_filters_are_rejected(string query)
    {
        var admin = await AsAsync(Role.Admin);

        (await admin.Client.GetAsync($"/api/v1/audit?{query}", Ct)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    private static async Task<PagedResult<AuditEntryResponse>> Query(ManagementApiDriver driver, string query) =>
        await (await driver.Client.GetAsync($"/api/v1/audit?{query}", Ct)).ReadJsonAsync<PagedResult<AuditEntryResponse>>(Ct);
}
