using System.Net;
using FlagForge.Application.Schedules;
using FlagForge.Application.Targeting;
using FlagForge.Domain;
using FlagForge.Evaluation;
using FlagForge.Testing;
using FlagForge.Worker.Jobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FlagForge.Worker.IntegrationTests;

public sealed class WorkerTests(FlagForgeFixture fixture) : IntegrationTest(fixture)
{
    private const string Development = "development";

    private ManagementApiDriver _editor = null!;
    private string _project = string.Empty;

    private WorkerFactory Worker => Fixture.Worker;

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        var admin = await ManagementApiDriver.CreateAsync(Fixture, Role.Admin, Ct);
        _project = (await admin.CreateProjectAsync(cancellationToken: Ct)).Key;
        _editor = await ManagementApiDriver.CreateAsync(Fixture, Role.Editor, Ct);
    }

    [Fact]
    public async Task Two_processors_in_parallel_execute_each_due_change_exactly_once()
    {
        const int Changes = 30;
        var flags = new List<string>();
        for (var i = 0; i < Changes; i++)
        {
            var flag = await _editor.CreateFlagAsync(_project, $"flag-{i:00}", cancellationToken: Ct);
            await ScheduleAsync(flag.Key, ScheduledChangeAction.TurnOn, Fixture.Time.GetUtcNow().AddMinutes(1));
            flags.Add(flag.Key);
        }

        var versionBefore = await DevelopmentConfigVersionAsync();
        Fixture.Time.Advance(TimeSpan.FromMinutes(2));
        var first = Worker.CreateProcessor();
        var second = Worker.CreateProcessor();

        var summaries = new List<ProcessingSummary>();
        while (true)
        {
            var round = await Task.WhenAll(first.ProcessDueChangesAsync(Ct), second.ProcessDueChangesAsync(Ct));
            summaries.AddRange(round);
            if (round.All(s => s.Claimed == 0))
            {
                break;
            }
        }

        summaries.Sum(s => s.Executed).ShouldBe(Changes);
        summaries.Sum(s => s.Failed).ShouldBe(0);
        await using var db = Fixture.CreateDbContext();
        (await db.ScheduledChanges.CountAsync(s => s.Status == ScheduledChangeStatus.Completed && s.AttemptCount == 1, Ct)).ShouldBe(Changes);
        var executed = await db.AuditEntries.Where(a => a.Action == AuditActions.ScheduleExecuted).ToListAsync(Ct);
        executed.Select(a => a.After!.Value.GetProperty("id").GetGuid()).Distinct().Count().ShouldBe(Changes);
        executed.Count.ShouldBe(Changes);
        (await db.AuditEntries.CountAsync(a => a.Action == AuditActions.FlagToggled, Ct)).ShouldBe(Changes);
        (await DevelopmentConfigVersionAsync()).ShouldBe(versionBefore + Changes);
        foreach (var flag in flags)
        {
            (await _editor.GetTargetingAsync(_project, flag, Development, Ct)).Enabled.ShouldBeTrue();
        }
    }

    [Fact]
    public async Task Executed_changes_are_audited_as_the_scheduler_with_the_author_in_the_comment()
    {
        var flag = await _editor.CreateFlagAsync(_project, cancellationToken: Ct);
        await ScheduleAsync(flag.Key, ScheduledChangeAction.TurnOn, Fixture.Time.GetUtcNow().AddMinutes(1));
        Fixture.Time.Advance(TimeSpan.FromMinutes(2));

        (await Worker.CreateProcessor().ProcessDueChangesAsync(Ct)).Executed.ShouldBe(1);

        await using var db = Fixture.CreateDbContext();
        var toggled = await db.AuditEntries.SingleAsync(a => a.Action == AuditActions.FlagToggled, Ct);
        toggled.ActorType.ShouldBe(ActorType.System);
        toggled.ActorName.ShouldBe("scheduler");
        toggled.ActorId.ShouldBeNull();
        toggled.Comment.ShouldStartWith($"Scheduled by {_editor.User.DisplayName} on ");
        toggled.Comment.ShouldEndWith(" UTC");
        var change = await db.ScheduledChanges.SingleAsync(Ct);
        change.ExecutedAt.ShouldBe(Fixture.Time.GetUtcNow());
    }

    [Fact]
    public async Task Release_plan_steps_apply_in_order_even_when_all_are_due_at_once()
    {
        var flag = await _editor.CreateFlagAsync(_project, cancellationToken: Ct);
        await _editor.ToggleAsync(_project, flag.Key, Development, true, cancellationToken: Ct);
        var now = Fixture.Time.GetUtcNow();
        var plan = new CreateReleasePlanRequest(
        [
            new ReleasePlanStep(now.AddMinutes(1), Weights(5)),
            new ReleasePlanStep(now.AddMinutes(2), Weights(50)),
            new ReleasePlanStep(now.AddMinutes(3), Weights(100)),
        ]);
        (await _editor.Client.PostJsonAsync($"{ManagementApiDriver.TargetingUrl(_project, flag.Key, Development)}/release-plan", plan, Ct))
            .StatusCode.ShouldBe(HttpStatusCode.Created);
        Fixture.Time.Advance(TimeSpan.FromMinutes(10));

        var first = Worker.CreateProcessor();
        var second = Worker.CreateProcessor();
        for (var round = 0; round < 10; round++)
        {
            var summaries = await Task.WhenAll(first.ProcessDueChangesAsync(Ct), second.ProcessDueChangesAsync(Ct));
            if (summaries.All(s => s.Claimed == 0))
            {
                break;
            }
        }

        var targeting = await _editor.GetTargetingAsync(_project, flag.Key, Development, Ct);
        targeting.Fallthrough.Rollout!.Weights.Select(w => w.Weight).ShouldBe([100_000, 0]);
        await using var db = Fixture.CreateDbContext();
        var applied = await db.AuditEntries
            .Where(a => a.Action == AuditActions.FlagTargetingUpdated)
            .OrderBy(a => a.Id)
            .Select(a => a.After)
            .ToListAsync(Ct);
        applied.Select(a => a!.Value.GetProperty("fallthrough").GetProperty("rollout").GetProperty("weights")[0].GetProperty("weight").GetInt32())
            .ShouldBe([5_000, 50_000, 100_000]);
    }

    [Fact]
    public async Task Failing_changes_are_retried_and_then_marked_failed_with_an_audit_entry()
    {
        var flag = await _editor.CreateFlagAsync(_project, cancellationToken: Ct);
        Guid changeId;
        await using (var db = Fixture.CreateDbContext())
        {
            // Validation stops this at the API; inserting it directly simulates a change that became invalid.
            var environmentId = await db.Environments.Where(e => e.Project.Key == _project && e.Key == Development).Select(e => e.Id).SingleAsync(Ct);
            var change = new ScheduledChange
            {
                Id = Guid.CreateVersion7(Fixture.Time.GetUtcNow()),
                FlagId = flag.Id,
                EnvironmentId = environmentId,
                ExecuteAt = Fixture.Time.GetUtcNow(),
                Action = ScheduledChangeAction.SetFallthrough,
                Payload = Serve.Variation("variation-that-does-not-exist"),
                CreatedAt = Fixture.Time.GetUtcNow(),
                CreatedByUserId = _editor.User.Id,
            };
            db.ScheduledChanges.Add(change);
            await db.SaveChangesAsync(Ct);
            changeId = change.Id;
        }

        var processor = Worker.CreateProcessor();
        var attempts = new List<ProcessingSummary>();
        for (var i = 0; i < ScheduledChange.MaxAttempts; i++)
        {
            attempts.Add(await processor.ProcessDueChangesAsync(Ct));
        }

        var afterFailure = await processor.ProcessDueChangesAsync(Ct);

        attempts.ShouldAllBe(s => s.Failed == 1);
        afterFailure.Claimed.ShouldBe(0);
        await using var verify = Fixture.CreateDbContext();
        var failed = await verify.ScheduledChanges.SingleAsync(s => s.Id == changeId, Ct);
        failed.Status.ShouldBe(ScheduledChangeStatus.Failed);
        failed.AttemptCount.ShouldBe(ScheduledChange.MaxAttempts);
        failed.Error!.ShouldContain("variation-that-does-not-exist");
        var audit = await verify.AuditEntries.SingleAsync(a => a.Action == AuditActions.ScheduleFailed, Ct);
        audit.ActorName.ShouldBe("scheduler");
        audit.FlagId.ShouldBe(flag.Id);
        audit.Comment!.ShouldContain("Gave up after 3 attempts");
        (await _editor.GetTargetingAsync(_project, flag.Key, Development, Ct)).Version.ShouldBe(1);
    }

    [Fact]
    public async Task Claims_left_by_a_crashed_worker_are_reclaimed_after_they_expire()
    {
        var flag = await _editor.CreateFlagAsync(_project, cancellationToken: Ct);
        await ScheduleAsync(flag.Key, ScheduledChangeAction.TurnOn, Fixture.Time.GetUtcNow().AddMinutes(1));
        Fixture.Time.Advance(TimeSpan.FromMinutes(2));
        await using (var scope = Worker.Services.CreateAsyncScope())
        {
            // A worker claims the change and dies before executing it.
            var now = Fixture.Time.GetUtcNow();
            (await scope.ServiceProvider.GetRequiredService<IScheduledChangeClaimer>().ClaimDueAsync(20, now, now + ScheduledChangeProcessor.ClaimDuration, Ct)).Count.ShouldBe(1);
        }

        var whileClaimed = await Worker.CreateProcessor().ProcessDueChangesAsync(Ct);
        Fixture.Time.Advance(ScheduledChangeProcessor.ClaimDuration + TimeSpan.FromSeconds(1));
        var afterExpiry = await Worker.CreateProcessor().ProcessDueChangesAsync(Ct);

        whileClaimed.Claimed.ShouldBe(0);
        afterExpiry.Executed.ShouldBe(1);
        await using var db = Fixture.CreateDbContext();
        var change = await db.ScheduledChanges.SingleAsync(Ct);
        change.Status.ShouldBe(ScheduledChangeStatus.Completed);
        change.AttemptCount.ShouldBe(2);
    }

    [Fact]
    public async Task The_running_worker_executes_due_changes_on_its_timer()
    {
        await using var worker = Worker.WithJobs();
        _ = worker.Server;
        var flag = await _editor.CreateFlagAsync(_project, cancellationToken: Ct);
        await ScheduleAsync(flag.Key, ScheduledChangeAction.TurnOn, Fixture.Time.GetUtcNow().AddSeconds(10));

        Fixture.Time.Advance(TimeSpan.FromSeconds(16));

        await Eventually.AssertAsync(
            async () => (await _editor.GetTargetingAsync(_project, flag.Key, Development, Ct)).Enabled,
            TimeSpan.FromSeconds(10),
            Ct,
            "the worker to turn the flag on");
    }

    [Fact]
    public async Task Retention_deletes_only_rows_past_their_retention()
    {
        var now = Fixture.Time.GetUtcNow();
        var flag = await _editor.CreateFlagAsync(_project, cancellationToken: Ct);
        await using (var db = Fixture.CreateDbContext())
        {
            var environmentId = await db.Environments.Where(e => e.Project.Key == _project && e.Key == Development).Select(e => e.Id).SingleAsync(Ct);
            var oldHour = Hour(now.AddDays(-91));
            db.FlagUsageHourly.AddRange(Enumerable.Range(0, RetentionCleanup.BatchSize + 5).Select(i => new FlagUsageHourly
            {
                EnvironmentId = environmentId,
                FlagId = flag.Id,
                VariationId = "true",
                HourStart = oldHour.AddHours(-i),
                Count = 1,
            }));
            db.FlagUsageHourly.Add(new FlagUsageHourly { EnvironmentId = environmentId, FlagId = flag.Id, VariationId = "true", HourStart = Hour(now.AddDays(-89)), Count = 1 });
            db.AuditEntries.AddRange(
                Audit("old", now.AddDays(-366)),
                Audit("recent", now.AddDays(-364)));
            db.RefreshTokens.AddRange(
                Token(_editor.User.Id, expiresAt: now.AddDays(-8)),
                Token(_editor.User.Id, expiresAt: now.AddDays(-6)),
                Token(_editor.User.Id, expiresAt: now.AddDays(3)));
            await db.SaveChangesAsync(Ct);
        }

        RetentionSummary summary;
        await using (var scope = Worker.Services.CreateAsyncScope())
        {
            summary = await scope.ServiceProvider.GetRequiredService<RetentionCleanup>().CleanUpAsync(Ct);
        }

        summary.ShouldBe(new RetentionSummary(RetentionCleanup.BatchSize + 5, 1, 1));
        await using var verify = Fixture.CreateDbContext();
        (await verify.FlagUsageHourly.CountAsync(Ct)).ShouldBe(1);
        (await verify.AuditEntries.Where(a => a.ResourceKey.StartsWith("retention/")).Select(a => a.ResourceKey).ToListAsync(Ct)).ShouldBe(["retention/recent"]);
        (await verify.RefreshTokens.CountAsync(t => t.UserId == _editor.User.Id, Ct)).ShouldBe(2);
    }

    [Fact]
    public async Task Worker_exposes_only_health_endpoints()
    {
        using var client = Worker.CreateClient();

        (await client.GetAsync("/health/live", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.GetAsync("/health/ready", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.GetAsync("/api/v1/meta", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private async Task ScheduleAsync(string flagKey, ScheduledChangeAction action, DateTimeOffset executeAt)
    {
        var response = await _editor.Client.PostJsonAsync(
            $"{ManagementApiDriver.TargetingUrl(_project, flagKey, Development)}/scheduled-changes", new CreateScheduledChangeRequest(executeAt, action), Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));
    }

    private async Task<long> DevelopmentConfigVersionAsync()
    {
        await using var db = Fixture.CreateDbContext();
        return await db.Environments.Where(e => e.Project.Key == _project && e.Key == Development).Select(e => e.ConfigVersion).SingleAsync(Ct);
    }

    private static WeightedVariation[] Weights(int truePercent) =>
    [
        new() { VariationId = "true", Weight = truePercent * 1000 },
        new() { VariationId = "false", Weight = (100 - truePercent) * 1000 },
    ];

    private static DateTimeOffset Hour(DateTimeOffset value) => new(value.Year, value.Month, value.Day, value.Hour, 0, 0, TimeSpan.Zero);

    private static AuditEntry Audit(string name, DateTimeOffset occurredAt) => new()
    {
        OccurredAt = occurredAt,
        ActorType = ActorType.System,
        ActorName = "test",
        Action = AuditActions.ProjectUpdated,
        ResourceKey = $"retention/{name}",
    };

    private RefreshToken Token(Guid userId, DateTimeOffset expiresAt) => new()
    {
        Id = Guid.CreateVersion7(Fixture.Time.GetUtcNow()),
        UserId = userId,
        TokenHash = Hashing.Sha256Hex(Guid.NewGuid().ToString()),
        ExpiresAt = expiresAt,
        CreatedAt = expiresAt.AddDays(-7),
    };
}
