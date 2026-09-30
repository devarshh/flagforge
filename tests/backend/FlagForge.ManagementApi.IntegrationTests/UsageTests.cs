using System.Globalization;
using System.Net;
using FlagForge.Application.Usage;
using FlagForge.Domain;
using FlagForge.Testing;
using Microsoft.EntityFrameworkCore;

namespace FlagForge.ManagementApi.IntegrationTests;

public sealed class UsageTests(FlagForgeFixture fixture) : ManagementApiTest(fixture)
{
    [Fact]
    public async Task Usage_is_aggregated_by_hour_and_by_day()
    {
        var admin = await AsAsync(Role.Admin);
        var project = await admin.CreateProjectAsync(cancellationToken: Ct);
        var flag = await admin.CreateFlagAsync(project.Key, cancellationToken: Ct);
        // Three days back, so the default "last 24 hours" window is empty whatever the time of day.
        var day = UsageService.StartOfDay(Fixture.Time.GetUtcNow().AddDays(-3));
        await using (var db = Fixture.CreateDbContext())
        {
            var environmentId = await db.Environments.Where(e => e.ProjectId == project.Id && e.Key == Development).Select(e => e.Id).SingleAsync(Ct);
            db.FlagUsageHourly.AddRange(
                Row(environmentId, flag.Id, "true", day.AddHours(10), 5),
                Row(environmentId, flag.Id, "false", day.AddHours(10), 1),
                Row(environmentId, flag.Id, "true", day.AddHours(11), 7),
                Row(environmentId, flag.Id, "true", day.AddDays(1).AddHours(9), 4));
            await db.SaveChangesAsync(Ct);
        }

        var range = $"from={Format(day)}&to={Format(day.AddDays(2))}";
        var url = $"{ManagementApiDriver.TargetingUrl(project.Key, flag.Key, Development)}/usage";
        var hourly = await (await admin.Client.GetAsync($"{url}?{range}&granularity=hour", Ct)).ReadJsonAsync<IReadOnlyList<UsageBucket>>(Ct);
        var daily = await (await admin.Client.GetAsync($"{url}?{range}&granularity=day", Ct)).ReadJsonAsync<IReadOnlyList<UsageBucket>>(Ct);
        var lastDay = await (await admin.Client.GetAsync(url, Ct)).ReadJsonAsync<IReadOnlyList<UsageBucket>>(Ct);

        hourly.ShouldBe(
        [
            new UsageBucket(day.AddHours(10), "false", 1),
            new UsageBucket(day.AddHours(10), "true", 5),
            new UsageBucket(day.AddHours(11), "true", 7),
            new UsageBucket(day.AddDays(1).AddHours(9), "true", 4),
        ]);
        daily.ShouldBe(
        [
            new UsageBucket(day, "false", 1),
            new UsageBucket(day, "true", 12),
            new UsageBucket(day.AddDays(1), "true", 4),
        ]);
        lastDay.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("granularity=week")]
    [InlineData("from=2026-01-01T00:00:00Z&to=2026-06-01T00:00:00Z")]
    [InlineData("from=2026-02-01T00:00:00Z&to=2026-01-01T00:00:00Z")]
    public async Task Usage_query_is_validated(string query)
    {
        var admin = await AsAsync(Role.Admin);
        var project = await admin.CreateProjectAsync(cancellationToken: Ct);
        var flag = await admin.CreateFlagAsync(project.Key, cancellationToken: Ct);

        var response = await admin.Client.GetAsync($"{ManagementApiDriver.TargetingUrl(project.Key, flag.Key, Development)}/usage?{query}", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    private static FlagUsageHourly Row(Guid environmentId, Guid flagId, string variationId, DateTimeOffset hour, long count) =>
        new() { EnvironmentId = environmentId, FlagId = flagId, VariationId = variationId, HourStart = hour, Count = count };

    private static string Format(DateTimeOffset value) => Uri.EscapeDataString(value.ToString("O", CultureInfo.InvariantCulture));
}
