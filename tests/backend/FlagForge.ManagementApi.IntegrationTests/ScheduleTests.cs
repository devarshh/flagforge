using System.Net;
using System.Net.Http.Json;
using FlagForge.Application.Schedules;
using FlagForge.Domain;
using FlagForge.Evaluation;
using FlagForge.Testing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FlagForge.ManagementApi.IntegrationTests;

public sealed class ScheduleTests(FlagForgeFixture fixture) : ManagementApiTest(fixture)
{
    private ManagementApiDriver _editor = null!;
    private string _project = string.Empty;
    private string _flag = string.Empty;

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        var admin = await AsAsync(Role.Admin);
        _project = (await admin.CreateProjectAsync(cancellationToken: Ct)).Key;
        _flag = (await admin.CreateFlagAsync(_project, cancellationToken: Ct)).Key;
        _editor = await AsAsync(Role.Editor);
    }

    private string Url => ManagementApiDriver.TargetingUrl(_project, _flag, Development);

    [Fact]
    public async Task Release_plan_creates_one_rollout_change_per_step()
    {
        var now = Fixture.Time.GetUtcNow();
        int[] percents = [5, 25, 50, 100];
        TimeSpan[] offsets = [TimeSpan.FromHours(1), TimeSpan.FromDays(1), TimeSpan.FromDays(2), TimeSpan.FromDays(3)];
        var steps = percents.Select((percent, i) => new ReleasePlanStep(now + offsets[i],
        [
            // Deliberately listed false-first: the server stores weights in variation order.
            new WeightedVariation { VariationId = "false", Weight = (100 - percent) * 1000 },
            new WeightedVariation { VariationId = "true", Weight = percent * 1000 },
        ])).ToList();

        var response = await _editor.Client.PostJsonAsync($"{Url}/release-plan", new CreateReleasePlanRequest(steps), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await response.ReadJsonAsync<IReadOnlyList<ScheduledChangeResponse>>(Ct);
        created.Count.ShouldBe(4);
        created.Select(c => c.ReleasePlanId).Distinct().Count().ShouldBe(1);
        created.ShouldAllBe(c => c.Action == ScheduledChangeAction.SetFallthrough && c.Status == ScheduledChangeStatus.Pending);
        created.Select(c => c.ExecuteAt).ShouldBe(steps.Select(s => s.ExecuteAt));
        created.Select(c => c.Payload!.Rollout!.Weights[0]).ShouldAllBe(w => w.VariationId == "true");
        created.Select(c => c.Payload!.Rollout!.Weights[0].Weight).ShouldBe([5_000, 25_000, 50_000, 100_000]);
        await using var db = Fixture.CreateDbContext();
        (await db.ScheduledChanges.CountAsync(s => s.ReleasePlanId == created[0].ReleasePlanId, Ct)).ShouldBe(4);
    }

    [Fact]
    public async Task Release_plan_steps_must_be_ascending_valid_and_in_the_future()
    {
        var now = Fixture.Time.GetUtcNow();
        WeightedVariation[] half = [new() { VariationId = "true", Weight = 50_000 }, new() { VariationId = "false", Weight = 50_000 }];
        WeightedVariation[] short90 = [new() { VariationId = "true", Weight = 40_000 }, new() { VariationId = "false", Weight = 50_000 }];

        var notAscending = await _editor.Client.PostJsonAsync($"{Url}/release-plan",
            new CreateReleasePlanRequest([new(now.AddDays(2), half), new(now.AddDays(1), half)]), Ct);
        var badWeights = await _editor.Client.PostJsonAsync($"{Url}/release-plan",
            new CreateReleasePlanRequest([new(now.AddDays(1), short90)]), Ct);
        var past = await _editor.Client.PostJsonAsync($"{Url}/release-plan",
            new CreateReleasePlanRequest([new(now.AddMinutes(-5), half)]), Ct);
        var tooMany = await _editor.Client.PostJsonAsync($"{Url}/release-plan",
            new CreateReleasePlanRequest([.. Enumerable.Range(1, 11).Select(i => new ReleasePlanStep(now.AddDays(i), half))]), Ct);

        (await Errors(notAscending)).ShouldContainKey("steps[1].executeAt");
        (await Errors(badWeights))["steps[0].weights"].ShouldBe(["Weights add up to 90%. Make them add up to 100%."]);
        (await Errors(past)).ShouldContainKey("steps[0].executeAt");
        (await Errors(tooMany)).ShouldContainKey("steps");
    }

    [Fact]
    public async Task Scheduled_change_validates_time_and_payload()
    {
        var now = Fixture.Time.GetUtcNow();

        var past = await _editor.Client.PostJsonAsync($"{Url}/scheduled-changes", new CreateScheduledChangeRequest(now.AddMinutes(-1), ScheduledChangeAction.TurnOn), Ct);
        var withinTolerance = await _editor.Client.PostJsonAsync($"{Url}/scheduled-changes", new CreateScheduledChangeRequest(now.AddSeconds(-20), ScheduledChangeAction.TurnOn), Ct);
        var missingPayload = await _editor.Client.PostJsonAsync($"{Url}/scheduled-changes", new CreateScheduledChangeRequest(now.AddHours(1), ScheduledChangeAction.SetFallthrough), Ct);
        var unexpectedPayload = await _editor.Client.PostJsonAsync($"{Url}/scheduled-changes",
            new CreateScheduledChangeRequest(now.AddHours(1), ScheduledChangeAction.TurnOff, Serve.Variation("true")), Ct);
        var badVariation = await _editor.Client.PostJsonAsync($"{Url}/scheduled-changes",
            new CreateScheduledChangeRequest(now.AddHours(1), ScheduledChangeAction.SetFallthrough, Serve.Variation("nope")), Ct);

        (await Errors(past)).ShouldContainKey("executeAt");
        withinTolerance.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await Errors(missingPayload)).ShouldContainKey("payload");
        (await Errors(unexpectedPayload)).ShouldContainKey("payload");
        (await Errors(badVariation)).ShouldContainKey("payload.variationId");
    }

    [Fact]
    public async Task Pending_changes_can_be_cancelled_once()
    {
        var created = await (await _editor.Client.PostJsonAsync($"{Url}/scheduled-changes",
            new CreateScheduledChangeRequest(Fixture.Time.GetUtcNow().AddHours(1), ScheduledChangeAction.TurnOn), Ct)).ReadJsonAsync<ScheduledChangeResponse>(Ct);

        var cancel = await _editor.Client.DeleteAsync($"{Url}/scheduled-changes/{created.Id}", Ct);
        var again = await _editor.Client.DeleteAsync($"{Url}/scheduled-changes/{created.Id}", Ct);
        var listed = await (await _editor.Client.GetAsync($"{Url}/scheduled-changes", Ct)).ReadJsonAsync<IReadOnlyList<ScheduledChangeResponse>>(Ct);

        cancel.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        again.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        listed.Single().Status.ShouldBe(ScheduledChangeStatus.Cancelled);
        await using var db = Fixture.CreateDbContext();
        (await db.AuditEntries.CountAsync(a => a.Action == AuditActions.ScheduleCancelled, Ct)).ShouldBe(1);
    }

    private static async Task<IDictionary<string, string[]>> Errors(HttpResponseMessage response)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync(Ct));
        return (await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(Ct))!.Errors;
    }
}
