using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FlagForge.Application.Evaluations;
using FlagForge.Domain;
using FlagForge.Evaluation;
using FlagForge.EvaluationApi.Endpoints;
using FlagForge.EvaluationApi.Hubs;
using FlagForge.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FlagForge.EvaluationApi.IntegrationTests;

public sealed class EvaluationApiTests(FlagForgeFixture fixture) : IntegrationTest(fixture)
{
    private const string Development = "development";
    private static readonly TimeSpan PropagationTimeout = TimeSpan.FromSeconds(5);

    private ManagementApiDriver _admin = null!;
    private string _project = string.Empty;
    private string _sdkKey = string.Empty;

    private EvaluationApiFactory Api => Fixture.EvaluationApi;

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        _admin = await ManagementApiDriver.CreateAsync(Fixture, Role.Admin, Ct);
        _project = (await _admin.CreateProjectAsync(cancellationToken: Ct)).Key;
        _sdkKey = (await _admin.CreateSdkKeyAsync(_project, Development, Ct)).PlaintextKey;
    }

    [Fact]
    public async Task Missing_unknown_and_malformed_keys_are_unauthorized()
    {
        using var anonymous = Api.CreateClient();
        using var unknown = Api.CreateSdkClient("ffk_" + new string('x', 43));
        using var malformed = Api.CreateSdkClient("not-an-sdk-key");

        (await EvaluateAsync(anonymous)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await EvaluateAsync(unknown)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await EvaluateAsync(malformed)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Revoked_keys_stop_working_once_the_revocation_arrives()
    {
        var created = await _admin.CreateSdkKeyAsync(_project, Development, Ct);
        using var client = Api.CreateSdkClient(created.PlaintextKey);
        (await EvaluateAsync(client)).StatusCode.ShouldBe(HttpStatusCode.OK);

        await _admin.Client.DeleteAsync($"/api/v1/projects/{_project}/environments/{Development}/sdk-keys/{created.Id}", Ct);

        // The key is cached for 60 s; only the Redis revocation message can end it this quickly.
        await Eventually.AssertAsync(
            async () => (await EvaluateAsync(client)).StatusCode == HttpStatusCode.Unauthorized, PropagationTimeout, Ct, "the revoked key to be rejected");
    }

    [Fact]
    public async Task Evaluate_returns_every_non_archived_flag_and_no_configuration()
    {
        var targeted = await _admin.CreateFlagAsync(_project, "targeted", cancellationToken: Ct);
        await _admin.UpdateTargetingAsync(_project, targeted.Key, Development, new TargetingConfig
        {
            Enabled = true,
            OffVariationId = "false",
            Targets = [new Target { VariationId = "true", ContextKeys = ["secret-beta-tester"] }],
            Rules =
            [
                new Rule
                {
                    Id = "r_internal",
                    Description = "Confidential rule description",
                    Clauses = [new Clause { Attribute = "email", Operator = ClauseOperator.EndsWith, Values = ["@internal.example"] }],
                    Serve = Serve.Variation("true"),
                },
            ],
            Fallthrough = Serve.Variation("false"),
        }, cancellationToken: Ct);
        await _admin.CreateFlagAsync(_project, "banner-text", FlagType.String, ManagementApiDriver.StringVariations("Hello", "Welcome"), cancellationToken: Ct);
        await _admin.CreateFlagAsync(_project, "retired", cancellationToken: Ct);
        await _admin.ArchiveFlagAsync(_project, "retired", Ct);
        using var client = Api.CreateSdkClient(_sdkKey);

        var response = await EvaluateAsync(client, new { key = "someone", attributes = new { email = "sam@internal.example" } });

        var json = await response.Content.ReadAsStringAsync(Ct);
        var body = JsonSerializer.Deserialize<EvaluateAllResponse>(json, Application.Common.JsonDefaults.Options)!;
        body.Flags.Keys.ShouldBe(["targeted", "banner-text"], ignoreOrder: true);
        body.Flags["targeted"].Value!.Value.GetBoolean().ShouldBeTrue();
        body.Flags["targeted"].Reason.ShouldBe(new ReasonResponse(EvaluationReasonKind.RuleMatch, "r_internal", 0, false));
        body.Flags["banner-text"].Reason.Kind.ShouldBe(EvaluationReasonKind.Off);
        foreach (var secret in new[] { "\"rules\"", "\"targets\"", "clauses", "contextKeys", "fallthrough", "secret-beta-tester", "@internal.example", "Confidential", "salt" })
        {
            json.ShouldNotContain(secret, Case.Insensitive);
        }
    }

    [Fact]
    public async Task Single_flag_evaluation_returns_one_result_or_404()
    {
        await _admin.CreateFlagAsync(_project, "one", cancellationToken: Ct);
        await _admin.CreateFlagAsync(_project, "gone", cancellationToken: Ct);
        await _admin.ArchiveFlagAsync(_project, "gone", Ct);
        using var client = Api.CreateSdkClient(_sdkKey);

        var one = await (await EvaluateAsync(client, url: "/sdk/v1/evaluate/one")).ReadJsonAsync<EvaluationResultResponse>(Ct);
        var archived = await EvaluateAsync(client, url: "/sdk/v1/evaluate/gone");
        var missing = await EvaluateAsync(client, url: "/sdk/v1/evaluate/never-existed");

        one.FlagKey.ShouldBe("one");
        one.VariationId.ShouldBe("false");
        one.Reason.Kind.ShouldBe(EvaluationReasonKind.Off);
        archived.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        missing.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await missing.Content.ReadFromJsonAsync<ProblemDetails>(Ct))!.Title.ShouldBe("Not found.");
    }

    [Fact]
    public async Task A_config_change_reaches_the_next_evaluation_through_redis()
    {
        await _admin.CreateFlagAsync(_project, "new-checkout", cancellationToken: Ct);
        using var client = Api.CreateSdkClient(_sdkKey);
        var before = await (await EvaluateAsync(client)).ReadJsonAsync<EvaluateAllResponse>(Ct);
        before.Flags["new-checkout"].Value!.Value.GetBoolean().ShouldBeFalse();

        await _admin.ToggleAsync(_project, "new-checkout", Development, enabled: true, cancellationToken: Ct);

        // The fake clock is frozen, so the snapshot TTL cannot expire: only the change message invalidates it.
        var after = await Eventually.GetAsync(
            async () => await (await EvaluateAsync(client)).ReadJsonAsync<EvaluateAllResponse>(Ct),
            response => response.Flags["new-checkout"].Value!.Value.GetBoolean(),
            PropagationTimeout,
            Ct,
            "new-checkout to be served as true");
        after.EnvironmentVersion.ShouldBeGreaterThan(before.EnvironmentVersion);
        after.Flags["new-checkout"].Reason.Kind.ShouldBe(EvaluationReasonKind.Fallthrough);
    }

    [Fact]
    public async Task Hub_clients_receive_flags_changed_after_a_change()
    {
        await _admin.CreateFlagAsync(_project, "live-flag", cancellationToken: Ct);
        await using var connection = Api.CreateHubConnection(_sdkKey);
        var received = new List<FlagsChangedMessage>();
        connection.On<FlagsChangedMessage>(nameof(IFlagsClient.FlagsChanged), message =>
        {
            lock (received)
            {
                received.Add(message);
            }
        });
        await connection.StartAsync(Ct);

        await _admin.ToggleAsync(_project, "live-flag", Development, enabled: true, cancellationToken: Ct);

        var expectedVersion = await EnvironmentConfigVersionAsync(Development);
        await Eventually.AssertAsync(
            () =>
            {
                lock (received)
                {
                    return Task.FromResult(received.Any(m => m.EnvironmentVersion == expectedVersion));
                }
            },
            PropagationTimeout,
            Ct,
            $"FlagsChanged with version {expectedVersion}");
    }

    [Fact]
    public async Task Hub_clients_in_other_environments_are_not_notified()
    {
        await _admin.CreateFlagAsync(_project, "scoped-flag", cancellationToken: Ct);
        var stagingKey = (await _admin.CreateSdkKeyAsync(_project, "staging", Ct)).PlaintextKey;
        await using var staging = Api.CreateHubConnection(stagingKey);
        await using var development = Api.CreateHubConnection(_sdkKey);
        var stagingMessages = 0;
        var developmentMessages = 0;
        staging.On<FlagsChangedMessage>(nameof(IFlagsClient.FlagsChanged), _ => Interlocked.Increment(ref stagingMessages));
        development.On<FlagsChangedMessage>(nameof(IFlagsClient.FlagsChanged), _ => Interlocked.Increment(ref developmentMessages));
        await staging.StartAsync(Ct);
        await development.StartAsync(Ct);

        await _admin.ToggleAsync(_project, "scoped-flag", Development, enabled: true, cancellationToken: Ct);

        await Eventually.AssertAsync(() => Task.FromResult(Volatile.Read(ref developmentMessages) > 0), PropagationTimeout, Ct, "the development notification");
        Volatile.Read(ref stagingMessages).ShouldBe(0);
    }

    [Fact]
    public async Task Hub_rejects_unknown_keys()
    {
        await using var connection = Api.CreateHubConnection("ffk_" + new string('z', 43));

        await Should.ThrowAsync<Exception>(() => connection.StartAsync(Ct));
    }

    [Fact]
    public async Task Usage_flush_writes_and_accumulates_hourly_rows()
    {
        var flag = await _admin.CreateFlagAsync(_project, "counted", cancellationToken: Ct);
        using var client = Api.CreateSdkClient(_sdkKey);
        for (var i = 0; i < 3; i++)
        {
            (await EvaluateAsync(client, new { key = $"user-{i}" })).StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        var written = await Api.UsageFlush.FlushAsync(Ct);
        (await EvaluateAsync(client, url: "/sdk/v1/evaluate/counted")).StatusCode.ShouldBe(HttpStatusCode.OK);
        await Api.UsageFlush.FlushAsync(Ct);

        written.ShouldBeGreaterThanOrEqualTo(1);
        await using var db = Fixture.CreateDbContext();
        var row = await db.FlagUsageHourly.SingleAsync(u => u.FlagId == flag.Id, Ct);
        row.VariationId.ShouldBe("false");
        row.Count.ShouldBe(4);
        row.HourStart.ShouldBe(Application.Usage.UsageService.StartOfHour(Fixture.Time.GetUtcNow()));
    }

    [Fact]
    public async Task Snapshots_expire_after_their_ttl_even_without_a_message()
    {
        var flag = await _admin.CreateFlagAsync(_project, "quiet-change", cancellationToken: Ct);
        using var client = Api.CreateSdkClient(_sdkKey);
        (await (await EvaluateAsync(client)).ReadJsonAsync<EvaluateAllResponse>(Ct)).Flags["quiet-change"].Value!.Value.GetBoolean().ShouldBeFalse();

        // Change the database behind the API's back: no Redis message is published.
        await using (var db = Fixture.CreateDbContext())
        {
            await db.FlagEnvironmentConfigs
                .Where(c => c.FlagId == flag.Id && c.Environment.Key == Development)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.Enabled, true), Ct);
        }

        (await (await EvaluateAsync(client)).ReadJsonAsync<EvaluateAllResponse>(Ct)).Flags["quiet-change"].Value!.Value.GetBoolean().ShouldBeFalse();
        Fixture.Time.Advance(TimeSpan.FromSeconds(61));
        (await (await EvaluateAsync(client)).ReadJsonAsync<EvaluateAllResponse>(Ct)).Flags["quiet-change"].Value!.Value.GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task Invalid_contexts_return_400_with_field_paths()
    {
        using var client = Api.CreateSdkClient(_sdkKey);

        var response = await EvaluateAsync(client, new { attributes = new { plan = new { nested = true } } });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var errors = (await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(Ct))!.Errors;
        errors.Keys.ShouldBe(["context.key", "context.attributes.plan"], ignoreOrder: true);
    }

    [Fact]
    public async Task Bodies_over_32_KB_are_rejected()
    {
        // The limit is Kestrel's, which the in-memory test server does not apply, so this test runs a real server.
        await using var kestrel = Api.WithWebHostBuilder(_ => { });
        kestrel.UseKestrel(0);
        kestrel.StartServer();
        using var client = kestrel.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _sdkKey);
        using var content = new StringContent(
            JsonSerializer.Serialize(new { context = new { key = "k", attributes = new { padding = new string('p', 40_000) } } }),
            Encoding.UTF8,
            "application/json");

        var response = await client.PostAsync("/sdk/v1/evaluate", content, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge);
    }

    [Fact]
    public async Task Rate_limiting_returns_429_with_retry_after()
    {
        await using var limited = Api.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration(configuration =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimiting:SdkBurst"] = "2",
                ["RateLimiting:SdkPermitsPerSecond"] = "1",
            })));
        using var client = limited.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _sdkKey);

        var statuses = new List<HttpResponseMessage>();
        for (var i = 0; i < 3; i++)
        {
            statuses.Add(await EvaluateAsync(client));
        }

        statuses.Take(2).ShouldAllBe(r => r.StatusCode == HttpStatusCode.OK);
        statuses[2].StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        statuses[2].Headers.RetryAfter.ShouldNotBeNull();
    }

    [Fact]
    public async Task Cors_allows_only_configured_origins_without_credentials()
    {
        await using var withCors = Api.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration(configuration =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Cors:AllowedOrigins"] = "https://shop.example, https://other.example" })));
        using var client = withCors.CreateClient();

        var allowed = await PreflightAsync(client, "https://shop.example");
        var denied = await PreflightAsync(client, "https://evil.example");

        allowed.Headers.GetValues("Access-Control-Allow-Origin").ShouldBe(["https://shop.example"]);
        allowed.Headers.Contains("Access-Control-Allow-Credentials").ShouldBeFalse();
        denied.Headers.Contains("Access-Control-Allow-Origin").ShouldBeFalse();
    }

    [Fact]
    public async Task Sdk_keys_never_appear_in_logs_even_at_trace_level()
    {
        var logs = new CapturingLoggerProvider();
        await using var verbose = Api.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Logging:LogLevel:Default"] = "Trace",
                ["Logging:LogLevel:Microsoft.AspNetCore"] = "Trace",
                ["Logging:LogLevel:Microsoft.AspNetCore.Hosting.Diagnostics"] = "Trace",
            }));
            builder.ConfigureLogging(logging => logging.AddProvider(logs));
        });
        await _admin.CreateFlagAsync(_project, "logged", cancellationToken: Ct);
        using var client = verbose.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _sdkKey);

        (await EvaluateAsync(client)).StatusCode.ShouldBe(HttpStatusCode.OK);
        await using (var connection = EvaluationApiFactory.CreateHubConnection(verbose.Server, _sdkKey))
        {
            await connection.StartAsync(Ct);
        }

        logs.Lines.ShouldNotBeEmpty();
        logs.Lines.ShouldAllBe(line => !line.Contains(_sdkKey, StringComparison.Ordinal));
    }

    private static async Task<HttpResponseMessage> PreflightAsync(HttpClient client, string origin)
    {
        using var request = new HttpRequestMessage(HttpMethod.Options, "/sdk/v1/evaluate");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "authorization,content-type");
        return await client.SendAsync(request, Ct);
    }

    private static Task<HttpResponseMessage> EvaluateAsync(HttpClient client, object? context = null, string url = "/sdk/v1/evaluate") =>
        client.PostJsonAsync(url, new { context = context ?? new { key = "user-1" } }, Ct);

    private async Task<long> EnvironmentConfigVersionAsync(string environmentKey)
    {
        await using var db = Fixture.CreateDbContext();
        return await db.Environments.Where(e => e.Project.Key == _project && e.Key == environmentKey).Select(e => e.ConfigVersion).SingleAsync(Ct);
    }
}
