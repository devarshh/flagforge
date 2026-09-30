using FlagForge.Application.Common;
using FlagForge.Application.Flags;
using FlagForge.Application.Stale;
using FlagForge.Domain;
using FlagForge.Testing;
using Microsoft.EntityFrameworkCore;

namespace FlagForge.ManagementApi.IntegrationTests;

public sealed class StaleFlagTests(FlagForgeFixture fixture) : ManagementApiTest(fixture)
{
    private ManagementApiDriver _admin = null!;
    private string _project = string.Empty;

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        _admin = await AsAsync(Role.Admin);
        _project = (await _admin.CreateProjectAsync(cancellationToken: Ct)).Key;
    }

    [Fact]
    public async Task Old_flag_without_evaluations_for_30_days_is_stale()
    {
        var flag = await OldFlagAsync("forgotten", ageInDays: 31);
        await AddUsageAsync(flag, Development, "true", daysAgo: 31);

        var stale = await StaleAsync();

        var entry = stale.ShouldHaveSingleItem();
        entry.FlagKey.ShouldBe("forgotten");
        entry.Reason.ShouldBe(StaleReason.NoRecentEvaluations);
        entry.LastEvaluatedAt.ShouldNotBeNull();
        entry.ServedVariationId.ShouldBeNull();
        (await ListAsync()).Items.Single(f => f.Key == "forgotten").IsStale.ShouldBeTrue();
    }

    [Fact]
    public async Task Flag_serving_one_variation_in_every_evaluating_environment_is_fully_rolled_out()
    {
        var same = await OldFlagAsync("same-everywhere", ageInDays: 40);
        await AddUsageAsync(same, Development, "true", daysAgo: 5);
        await AddUsageAsync(same, Production, "true", daysAgo: 1);
        var different = await OldFlagAsync("different-per-environment", ageInDays: 40);
        await AddUsageAsync(different, Development, "true", daysAgo: 3);
        await AddUsageAsync(different, Production, "false", daysAgo: 3);

        var stale = (await StaleAsync()).ToDictionary(s => s.FlagKey);

        stale["same-everywhere"].Reason.ShouldBe(StaleReason.FullyRolledOut);
        stale["same-everywhere"].ServedVariationId.ShouldBe("true");
        stale["different-per-environment"].Reason.ShouldBe(StaleReason.FullyRolledOut);
        stale["different-per-environment"].ServedVariationId.ShouldBeNull();
    }

    [Fact]
    public async Task Young_permanent_archived_varied_and_recently_quiet_flags_are_not_stale()
    {
        await OldFlagAsync("young", ageInDays: 5);
        var permanent = await OldFlagAsync("permanent", ageInDays: 60);
        await _admin.Client.PatchJsonAsync($"/api/v1/projects/{_project}/flags/{permanent.Key}", new UpdateFlagRequest(IsPermanent: true), Ct);
        var archived = await OldFlagAsync("archived", ageInDays: 60);
        await _admin.ArchiveFlagAsync(_project, archived.Key, Ct);
        var varied = await OldFlagAsync("varied", ageInDays: 60);
        await AddUsageAsync(varied, Development, "true", daysAgo: 2);
        await AddUsageAsync(varied, Development, "false", daysAgo: 2);
        var quiet = await OldFlagAsync("quiet-lately", ageInDays: 60);
        await AddUsageAsync(quiet, Development, "true", daysAgo: 20);

        (await StaleAsync()).ShouldBeEmpty();
    }

    private async Task<FlagResponse> OldFlagAsync(string key, int ageInDays)
    {
        var flag = await _admin.CreateFlagAsync(_project, key, cancellationToken: Ct);
        await using var db = Fixture.CreateDbContext();
        var createdAt = Fixture.Time.GetUtcNow().AddDays(-ageInDays);
        await db.Flags.Where(f => f.Id == flag.Id).ExecuteUpdateAsync(s => s.SetProperty(f => f.CreatedAt, createdAt), Ct);
        return flag;
    }

    private async Task AddUsageAsync(FlagResponse flag, string environmentKey, string variationId, int daysAgo)
    {
        await using var db = Fixture.CreateDbContext();
        var environmentId = await db.Environments.Where(e => e.Project.Key == _project && e.Key == environmentKey).Select(e => e.Id).SingleAsync(Ct);
        var now = Fixture.Time.GetUtcNow().AddDays(-daysAgo);
        db.FlagUsageHourly.Add(new FlagUsageHourly
        {
            EnvironmentId = environmentId,
            FlagId = flag.Id,
            VariationId = variationId,
            HourStart = new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, 0, 0, TimeSpan.Zero),
            Count = 42,
        });
        await db.SaveChangesAsync(Ct);
    }

    private async Task<IReadOnlyList<StaleFlagResponse>> StaleAsync() =>
        await (await _admin.Client.GetAsync($"/api/v1/projects/{_project}/stale-flags", Ct)).ReadJsonAsync<IReadOnlyList<StaleFlagResponse>>(Ct);

    private async Task<PagedResult<FlagSummaryResponse>> ListAsync() =>
        await (await _admin.Client.GetAsync($"/api/v1/projects/{_project}/flags", Ct)).ReadJsonAsync<PagedResult<FlagSummaryResponse>>(Ct);
}
