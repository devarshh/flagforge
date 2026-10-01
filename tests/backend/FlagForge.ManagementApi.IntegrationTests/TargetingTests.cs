using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FlagForge.Application.Common;
using FlagForge.Application.Evaluations;
using FlagForge.Application.Targeting;
using FlagForge.Domain;
using FlagForge.Evaluation;
using FlagForge.Testing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FlagForge.ManagementApi.IntegrationTests;

public sealed class TargetingTests(FlagForgeFixture fixture) : ManagementApiTest(fixture)
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
    public async Task Invalid_targeting_returns_400_with_field_paths()
    {
        var config = new TargetingConfig
        {
            Enabled = true,
            OffVariationId = "false",
            Rules =
            [
                new Rule
                {
                    Id = "r1",
                    Clauses = [new Clause { Attribute = "age", Operator = ClauseOperator.Gt, Values = ["eighteen"] }],
                    Serve = Serve.Variation("missing"),
                },
            ],
            Fallthrough = Serve.PercentageRollout(new Rollout
            {
                Weights = [new WeightedVariation { VariationId = "true", Weight = 40_000 }, new WeightedVariation { VariationId = "false", Weight = 50_000 }],
            }),
        };

        var response = await _editor.Client.PutJsonAsync(Url, new UpdateTargetingRequest(config, 1), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var errors = (await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(Ct))!.Errors;
        errors.Keys.ShouldBe(["config.rules[0].clauses[0].values[0]", "config.rules[0].serve.variationId", "config.fallthrough.rollout.weights"], ignoreOrder: true);
        errors["config.fallthrough.rollout.weights"].ShouldBe(["Weights add up to 90%. Make them add up to 100%."]);
    }

    [Fact]
    public async Task Unknown_json_values_return_400_with_the_field_path()
    {
        using var body = new StringContent(
            """{"expectedVersion":1,"config":{"enabled":true,"offVariationId":"false","fallthrough":{"variationId":"true"},"rules":[{"id":"r1","clauses":[{"attribute":"a","operator":"matches","values":["x"]}],"serve":{"variationId":"true"}}]}}""",
            System.Text.Encoding.UTF8,
            "application/json");

        var response = await _editor.Client.PutAsync(Url, body, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(Ct))!.Errors.ShouldContainKey("config.rules[0].clauses[0].operator");
    }

    [Fact]
    public async Task Stale_expected_version_returns_409_with_the_current_version()
    {
        var current = await _editor.GetTargetingAsync(_project, _flag, Development, Ct);
        await _editor.ToggleAsync(_project, _flag, Development, true, cancellationToken: Ct);

        var response = await _editor.Client.PutJsonAsync(Url, new UpdateTargetingRequest(On(), current.Version), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        problem.GetProperty("currentVersion").GetInt32().ShouldBe(current.Version + 1);
        problem.GetProperty("detail").GetString()!.ShouldContain("changed by someone else");
    }

    [Fact]
    public async Task Saving_bumps_the_config_version_and_the_environment_config_version()
    {
        var before = await _editor.GetTargetingAsync(_project, _flag, Development, Ct);
        var environmentVersionBefore = await EnvironmentConfigVersion(Development);

        var saved = await _editor.UpdateTargetingAsync(_project, _flag, Development, On(), cancellationToken: Ct);

        saved.Version.ShouldBe(before.Version + 1);
        saved.Enabled.ShouldBeTrue();
        saved.UpdatedBy!.Id.ShouldBe(_editor.User.Id);
        (await EnvironmentConfigVersion(Development)).ShouldBe(environmentVersionBefore + 1);
        (await EnvironmentConfigVersion(Staging)).ShouldBe(environmentVersionBefore);
    }

    [Fact]
    public async Task Saving_writes_an_audit_entry_with_before_and_after()
    {
        await _editor.UpdateTargetingAsync(_project, _flag, Development, On(), comment: "Ship it", cancellationToken: Ct);

        await using var db = Fixture.CreateDbContext();
        var entry = await db.AuditEntries.SingleAsync(a => a.Action == AuditActions.FlagTargetingUpdated, Ct);
        entry.ActorId.ShouldBe(_editor.User.Id);
        entry.ActorType.ShouldBe(ActorType.User);
        entry.ResourceKey.ShouldBe($"{_project}/{_flag}@{Development}");
        entry.Comment.ShouldBe("Ship it");
        entry.Before!.Value.GetProperty("enabled").GetBoolean().ShouldBeFalse();
        entry.After!.Value.GetProperty("enabled").GetBoolean().ShouldBeTrue();
        entry.After.Value.GetProperty("version").GetInt32().ShouldBe(entry.Before.Value.GetProperty("version").GetInt32() + 1);
    }

    [Fact]
    public async Task Saving_publishes_a_config_changed_message_after_commit()
    {
        await using var recorder = await RedisRecorder.StartAsync<ConfigChangedMessage>(Fixture.RedisConnectionString, Channels.ConfigChanged);
        var environmentId = await EnvironmentId(Development);

        await _editor.UpdateTargetingAsync(_project, _flag, Development, On(), cancellationToken: Ct);

        var expectedVersion = await EnvironmentConfigVersion(Development);
        var messages = await recorder.WaitForAsync(m => m.EnvironmentId == environmentId && m.ConfigVersion == expectedVersion, TimeSpan.FromSeconds(5), Ct);
        messages.ShouldContain(m => m.EnvironmentId == environmentId);
    }

    [Fact]
    public async Task Rollout_weights_are_saved_in_variation_order()
    {
        var config = On() with
        {
            Fallthrough = Serve.PercentageRollout(new Rollout
            {
                Weights = [new WeightedVariation { VariationId = "false", Weight = 75_000 }, new WeightedVariation { VariationId = "true", Weight = 25_000 }],
            }),
        };

        var saved = await _editor.UpdateTargetingAsync(_project, _flag, Development, config, cancellationToken: Ct);

        saved.Fallthrough.Rollout!.Weights.Select(w => w.VariationId).ShouldBe(["true", "false"]);
    }

    [Fact]
    public async Task Toggling_to_the_current_state_changes_nothing()
    {
        var before = await _editor.GetTargetingAsync(_project, _flag, Development, Ct);

        var toggled = await _editor.ToggleAsync(_project, _flag, Development, enabled: false, cancellationToken: Ct);

        toggled.Version.ShouldBe(before.Version);
        await using var db = Fixture.CreateDbContext();
        (await db.AuditEntries.AnyAsync(a => a.Action == AuditActions.FlagToggled, Ct)).ShouldBeFalse();
    }

    [Fact]
    public async Task Toggling_records_flag_toggled()
    {
        var toggled = await _editor.ToggleAsync(_project, _flag, Development, enabled: true, cancellationToken: Ct);

        toggled.Enabled.ShouldBeTrue();
        await using var db = Fixture.CreateDbContext();
        var entry = await db.AuditEntries.SingleAsync(a => a.Action == AuditActions.FlagToggled, Ct);
        entry.After!.Value.GetProperty("enabled").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task Archived_flags_cannot_be_retargeted()
    {
        await _editor.ArchiveFlagAsync(_project, _flag, Ct);

        var response = await _editor.Client.PostJsonAsync($"{Url}/toggle", new ToggleRequest(true), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Preview_evaluates_a_draft_without_saving_it()
    {
        var draft = On() with
        {
            Rules =
            [
                new Rule
                {
                    Id = "r_staff",
                    Clauses = [new Clause { Attribute = "email", Operator = ClauseOperator.EndsWith, Values = ["@acme.com"] }],
                    Serve = Serve.Variation("false"),
                },
            ],
        };
        var context = JsonSerializer.SerializeToElement(new { key = "sam", attributes = new { email = "sam@acme.com" } });

        var draftResult = await (await _editor.Client.PostJsonAsync($"{Url}/evaluate-preview", new PreviewRequest(context, draft), Ct))
            .ReadJsonAsync<EvaluationResultResponse>(Ct);
        var savedResult = await (await _editor.Client.PostJsonAsync($"{Url}/evaluate-preview", new PreviewRequest(context), Ct))
            .ReadJsonAsync<EvaluationResultResponse>(Ct);

        draftResult.VariationId.ShouldBe("false");
        draftResult.Reason.Kind.ShouldBe(EvaluationReasonKind.RuleMatch);
        draftResult.Reason.RuleId.ShouldBe("r_staff");
        draftResult.Reason.RuleIndex.ShouldBe(0);
        savedResult.Reason.Kind.ShouldBe(EvaluationReasonKind.Off);
        savedResult.Value!.Value.GetBoolean().ShouldBeFalse();
        (await _editor.GetTargetingAsync(_project, _flag, Development, Ct)).Enabled.ShouldBeFalse();
    }

    [Fact]
    public async Task Preview_reports_invalid_contexts_and_drafts_with_paths()
    {
        var badContext = await _editor.Client.PostJsonAsync($"{Url}/evaluate-preview",
            new PreviewRequest(JsonSerializer.SerializeToElement(new { attributes = new { plan = "free" } })), Ct);
        var badDraft = await _editor.Client.PostJsonAsync($"{Url}/evaluate-preview",
            new PreviewRequest(JsonSerializer.SerializeToElement(new { key = "u" }), On() with { OffVariationId = "nope" }), Ct);

        (await badContext.Content.ReadFromJsonAsync<ValidationProblemDetails>(Ct))!.Errors.ShouldContainKey("context.key");
        (await badDraft.Content.ReadFromJsonAsync<ValidationProblemDetails>(Ct))!.Errors.ShouldContainKey("draftConfig.offVariationId");
    }

    [Fact]
    public async Task Preview_json_uses_the_sdk_reason_format()
    {
        var response = await _editor.Client.PostJsonAsync($"{Url}/evaluate-preview", new PreviewRequest(JsonSerializer.SerializeToElement(new { key = "u" })), Ct);

        var json = await response.Content.ReadAsStringAsync(Ct);
        json.ShouldContain("\"kind\":\"OFF\"");
        json.ShouldNotContain("ruleId");
    }

    [Fact]
    public async Task Serves_are_written_with_only_the_alternative_in_use()
    {
        var config = On() with
        {
            Fallthrough = Serve.PercentageRollout(new Rollout
            {
                Weights = [new WeightedVariation { VariationId = "true", Weight = 25_000 }, new WeightedVariation { VariationId = "false", Weight = 75_000 }],
            }),
        };

        await _editor.UpdateTargetingAsync(_project, _flag, Development, config, cancellationToken: Ct);

        using var response = JsonDocument.Parse(await _editor.Client.GetStringAsync(Url, Ct));
        PropertyNames(response.RootElement.GetProperty("fallthrough")).ShouldBe(["rollout"]);
        await using var db = Fixture.CreateDbContext();
        var entry = await db.AuditEntries.SingleAsync(a => a.Action == AuditActions.FlagTargetingUpdated, Ct);
        PropertyNames(entry.Before!.Value.GetProperty("fallthrough")).ShouldBe(["variationId"]);
        PropertyNames(entry.After!.Value.GetProperty("fallthrough")).ShouldBe(["rollout"]);
    }

    private static string[] PropertyNames(JsonElement element) => [.. element.EnumerateObject().Select(property => property.Name)];

    private static TargetingConfig On() => new()
    {
        Enabled = true,
        OffVariationId = "false",
        Fallthrough = Serve.Variation("true"),
    };

    private async Task<long> EnvironmentConfigVersion(string environmentKey)
    {
        await using var db = Fixture.CreateDbContext();
        return await db.Environments.Where(e => e.Project.Key == _project && e.Key == environmentKey).Select(e => e.ConfigVersion).SingleAsync(Ct);
    }

    private async Task<Guid> EnvironmentId(string environmentKey)
    {
        await using var db = Fixture.CreateDbContext();
        return await db.Environments.Where(e => e.Project.Key == _project && e.Key == environmentKey).Select(e => e.Id).SingleAsync(Ct);
    }
}
