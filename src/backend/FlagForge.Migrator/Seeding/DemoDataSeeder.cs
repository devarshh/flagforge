using System.Text.Json;
using FlagForge.Application.Common;
using FlagForge.Domain;
using FlagForge.Evaluation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FlagForge.Migrator.Seeding;

/// <summary>
/// Seeds the "Acme Coffee" demo project used by the demo store. Runs only when <c>FF_SEED_DEMO_DATA=true</c> and the
/// project does not exist, so running the migrator again changes nothing.
/// </summary>
internal sealed partial class DemoDataSeeder(
    IFlagForgeDbContext db,
    IPasswordHasher<User> passwordHasher,
    IOptions<SeedOptions> options,
    TimeProvider timeProvider,
    ILogger<DemoDataSeeder> logger)
{
    public const string ProjectKey = "acme-coffee";

    private const string Development = "development";
    private const string Staging = "staging";
    private const string Production = "production";
    private const int UsageDays = 7;

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        var seed = options.Value;
        if (!seed.DemoData)
        {
            LogDisabled(logger);
            return;
        }

        if (await db.Projects.AnyAsync(p => p.Key == ProjectKey, cancellationToken))
        {
            LogSkipped(logger, ProjectKey);
            return;
        }

        var admin = await db.Users
            .Where(u => u.Role == Role.Admin && u.IsActive)
            .OrderBy(u => u.CreatedAt)
            .FirstAsync(cancellationToken);

        await db.ExecuteInTransactionAsync(
            async token =>
            {
                await SeedAsync(seed, admin, token);
                await db.SaveChangesAsync(token);
                return true;
            },
            cancellationToken);
        LogSeeded(logger, ProjectKey);
    }

    private async Task SeedAsync(SeedOptions seed, User admin, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        await AddUserIfMissingAsync("editor@flagforge.local", "Evan Editor", Role.Editor, seed.DemoUserPassword!, now, cancellationToken);
        await AddUserIfMissingAsync("viewer@flagforge.local", "Vera Viewer", Role.Viewer, seed.DemoUserPassword!, now, cancellationToken);

        var createdAt = now.AddDays(-21);
        var project = new Project
        {
            Id = Guid.CreateVersion7(createdAt),
            Key = ProjectKey,
            Name = "Acme Coffee",
            Description = "Sample storefront that the demo app uses to show live flag changes.",
            CreatedAt = createdAt,
            CreatedByUserId = admin.Id,
        };
        db.Projects.Add(project);
        Audit(admin, AuditActions.ProjectCreated, createdAt, project, after: new { project.Key, project.Name, project.Description });

        var environments = EnvironmentDefaults.Initial
            .Select((env, index) => new ProjectEnvironment
            {
                Id = Guid.CreateVersion7(createdAt),
                ProjectId = project.Id,
                Key = env.Key,
                Name = env.Name,
                Color = env.Color,
                IsProtected = env.IsProtected,
                SortOrder = index,
                CreatedAt = createdAt,
            })
            .ToDictionary(e => e.Key);
        db.Environments.AddRange(environments.Values);

        var flagsCreatedAt = now.AddDays(-14);
        var flags = new List<Flag>();
        foreach (var definition in DemoFlags(now, flagsCreatedAt))
        {
            var flag = new Flag
            {
                Id = Guid.CreateVersion7(definition.CreatedAt),
                ProjectId = project.Id,
                Key = definition.Key,
                Name = definition.Name,
                Description = definition.Description,
                Type = definition.Type,
                Variations = definition.Variations,
                Tags = definition.Tags,
                Salt = Generate.Salt(),
                IsPermanent = definition.IsPermanent,
                IsArchived = definition.ArchivedAt is not null,
                ArchivedAt = definition.ArchivedAt,
                CreatedAt = definition.CreatedAt,
                CreatedByUserId = admin.Id,
                UpdatedAt = definition.ArchivedAt ?? definition.CreatedAt,
            };
            db.Flags.Add(flag);
            flags.Add(flag);
            Audit(admin, AuditActions.FlagCreated, flag.CreatedAt, project, flag: flag, after: new
            {
                flag.Key,
                flag.Name,
                flag.Type,
                Variations = flag.Variations.Select(v => v.Id),
                flag.Tags,
            });

            foreach (var environment in environments.Values)
            {
                var config = FlagEnvironmentConfig.CreateDefault(Guid.CreateVersion7(flag.CreatedAt), flag, environment.Id, flag.CreatedAt);
                if (definition.Targeting.TryGetValue(environment.Key, out var targeting))
                {
                    EnsureValid(flag, environment.Key, targeting);
                    config.Apply(targeting, flag.CreatedAt, admin.Id);
                }

                db.FlagEnvironmentConfigs.Add(config);
            }

            if (definition.ArchivedAt is { } archivedAt)
            {
                Audit(admin, AuditActions.FlagArchived, archivedAt, project, flag: flag, comment: "Replaced by the new search service.");
            }
        }

        var development = environments[Development];
        var sdkKey = new SdkKey
        {
            Id = Guid.CreateVersion7(createdAt),
            EnvironmentId = development.Id,
            Name = "Demo store",
            KeyPrefix = SdkKey.DisplayPrefixOf(seed.DemoSdkKey!),
            KeyHash = Hashing.Sha256Hex(seed.DemoSdkKey!),
            CreatedAt = createdAt,
            CreatedByUserId = admin.Id,
        };
        db.SdkKeys.Add(sdkKey);
        Audit(admin, AuditActions.SdkKeyCreated, createdAt, project, environment: development,
            after: new { sdkKey.Name, sdkKey.KeyPrefix });

        AddHistory(admin, project, environments[Production], flags, flagsCreatedAt);
        AddUsage(development, flags, now);
    }

    private async Task AddUserIfMissingAsync(
        string email, string displayName, Role role, string password, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (await db.Users.AnyAsync(u => u.Email == email, cancellationToken))
        {
            return;
        }

        var user = new User { Id = Guid.CreateVersion7(now), Email = email, DisplayName = displayName, Role = role, CreatedAt = now };
        user.PasswordHash = passwordHasher.HashPassword(user, password);
        db.Users.Add(user);
    }

    /// <summary>A few realistic changes after creation, so History and the audit log have something to show.</summary>
    private void AddHistory(User admin, Project project, ProjectEnvironment production, List<Flag> flags, DateTimeOffset flagsCreatedAt)
    {
        var layout = flags.Single(f => f.Key == "new-product-layout");
        var before = FlagEnvironmentConfig.CreateDefault(Guid.Empty, layout, production.Id, flagsCreatedAt).ToTargetingConfig();
        var after = db.FlagEnvironmentConfigs.Local.Single(c => c.FlagId == layout.Id && c.EnvironmentId == production.Id).ToTargetingConfig();
        Audit(admin, AuditActions.FlagTargetingUpdated, flagsCreatedAt.AddDays(2), project, production, layout,
            "Early access customers first, then 10% of everyone else.", before, after);

        var banner = flags.Single(f => f.Key == "promo-banner");
        Audit(admin, AuditActions.FlagToggled, flagsCreatedAt.AddDays(3), project, production, banner,
            "Launch the autumn promotion for paid plans.", new { enabled = false }, new { enabled = true });
    }

    /// <summary>Hourly evaluation counts for the development environment with a day-night traffic pattern.</summary>
    private void AddUsage(ProjectEnvironment development, List<Flag> flags, DateTimeOffset now)
    {
        var currentHour = new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, 0, 0, TimeSpan.Zero);
        var shares = new Dictionary<string, (int PeakPerHour, (string VariationId, double Share)[] Split)>
        {
            ["promo-banner"] = (240, [("true", 1.0)]),
            ["promo-banner-text"] = (240, [("v_autumn", 0.8), ("v_frship", 0.2)]),
            ["new-product-layout"] = (220, [("true", 0.5), ("false", 0.5)]),
            ["checkout-button-color"] = (90, [("v_primry", 0.85), ("v_succes", 0.15)]),
            ["max-cart-items"] = (90, [("v_items5", 0.5), ("v_items3", 0.3), ("v_item10", 0.2)]),
            ["store-theme"] = (240, [("v_matcha", 0.85), ("v_caraml", 0.15)]),
        };

        for (var flagIndex = 0; flagIndex < flags.Count; flagIndex++)
        {
            var flag = flags[flagIndex];
            if (!shares.TryGetValue(flag.Key, out var usage))
            {
                continue;
            }

            for (var hoursAgo = UsageDays * 24; hoursAgo >= 1; hoursAgo--)
            {
                var hourStart = currentHour.AddHours(-hoursAgo);
                var total = (int)Math.Max(1, Math.Round(usage.PeakPerHour * DayNightFactor(hourStart.Hour) * Noise(hoursAgo, flagIndex)));
                var remaining = total;
                for (var i = 0; i < usage.Split.Length; i++)
                {
                    var (variationId, share) = usage.Split[i];
                    var count = i == usage.Split.Length - 1 ? remaining : (int)Math.Round(total * share);
                    remaining -= count;
                    if (count > 0)
                    {
                        db.FlagUsageHourly.Add(new FlagUsageHourly
                        {
                            EnvironmentId = development.Id,
                            FlagId = flag.Id,
                            VariationId = variationId,
                            HourStart = hourStart,
                            Count = count,
                        });
                    }
                }
            }
        }
    }

    /// <summary>Quiet nights, busy afternoons (UTC): 15% of peak overnight, 100% around 14:30.</summary>
    private static double DayNightFactor(int hourOfDay) =>
        0.15 + (0.85 * Math.Max(0, Math.Sin(Math.PI * (hourOfDay - 7) / 15.0)));

    /// <summary>Deterministic ±10% jitter so the chart looks organic without a random number generator.</summary>
    private static double Noise(int hoursAgo, int flagIndex)
    {
        var x = Math.Sin((hoursAgo * 12.9898) + (flagIndex * 78.233)) * 43758.5453;
        return 0.9 + (0.2 * (x - Math.Floor(x)));
    }

    private void Audit(
        User admin,
        string action,
        DateTimeOffset occurredAt,
        Project project,
        ProjectEnvironment? environment = null,
        Flag? flag = null,
        string? comment = null,
        object? before = null,
        object? after = null)
    {
        db.AuditEntries.Add(new AuditEntry
        {
            OccurredAt = occurredAt,
            ActorType = ActorType.User,
            ActorId = admin.Id,
            ActorName = admin.DisplayName,
            Action = action,
            ProjectId = project.Id,
            EnvironmentId = environment?.Id,
            FlagId = flag?.Id,
            ResourceKey = AuditEntry.ResourceKeyFor(project.Key, flag?.Key, environment?.Key),
            Comment = comment,
            Before = before is null ? null : JsonSerializer.SerializeToElement(before, JsonDefaults.Options),
            After = after is null ? null : JsonSerializer.SerializeToElement(after, JsonDefaults.Options),
        });
    }

    private static void EnsureValid(Flag flag, string environmentKey, TargetingConfig targeting)
    {
        var errors = TargetingValidator.Validate(targeting, flag.VariationIds);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                $"Seeded targeting for {flag.Key}@{environmentKey} is invalid: {string.Join("; ", errors.Select(e => $"{e.Path}: {e.Message}"))}");
        }
    }

    private static IEnumerable<DemoFlag> DemoFlags(DateTimeOffset now, DateTimeOffset createdAt)
    {
        var paidPlans = Clause("plan", ClauseOperator.In, "premium", "enterprise");

        yield return new DemoFlag(
            "promo-banner",
            "Promo banner",
            "Shows the promotional banner at the top of the store.",
            FlagType.Boolean,
            Variation.ForBoolean(),
            ["marketing"],
            createdAt,
            new()
            {
                [Development] = On("false", Serve.Variation("true")),
                [Staging] = On("false", Serve.Variation("true")),
                [Production] = On("false", Serve.Variation("false"), rules: [Rule("r_paidpl", "Paid plans", Serve.Variation("true"), paidPlans)]),
            });

        yield return new DemoFlag(
            "promo-banner-text",
            "Promo banner text",
            "The message shown in the promotional banner.",
            FlagType.String,
            [
                VariationOf("v_welcom", "Welcome", "Welcome to Acme Coffee"),
                VariationOf("v_autumn", "Autumn sale", "Autumn sale: 20% off all beans"),
                VariationOf("v_frship", "Free shipping", "Free shipping on every order this week"),
            ],
            ["marketing"],
            createdAt,
            new()
            {
                [Development] = On("v_welcom", Serve.Variation("v_autumn"),
                    rules: [Rule("r_entrpr", "Enterprise customers", Serve.Variation("v_frship"), Clause("plan", ClauseOperator.In, "enterprise"))]),
                [Staging] = On("v_welcom", Serve.Variation("v_autumn")),
                [Production] = On("v_welcom", Serve.Variation("v_welcom"),
                    rules: [Rule("r_entrpr", "Enterprise customers", Serve.Variation("v_frship"), Clause("plan", ClauseOperator.In, "enterprise"))]),
            });

        yield return new DemoFlag(
            "new-product-layout",
            "New product layout",
            "Shows products in a grid instead of a list.",
            FlagType.Boolean,
            Variation.ForBoolean(),
            ["storefront", "experiment"],
            createdAt,
            new()
            {
                [Development] = On("false", Rollout(("true", 50_000), ("false", 50_000))),
                [Staging] = On("false", Serve.Variation("true")),
                [Production] = On(
                    "false",
                    Rollout(("true", 10_000), ("false", 90_000)),
                    targets: [new Target { VariationId = "true", ContextKeys = ["alice"] }],
                    rules: [Rule("r_earlya", "Early access customers", Serve.Variation("true"), Clause("groups", ClauseOperator.In, "early-access"))]),
            });

        yield return new DemoFlag(
            "checkout-button-color",
            "Checkout button color",
            "Color of the checkout button.",
            FlagType.String,
            [
                VariationOf("v_primry", "Primary", "primary"),
                VariationOf("v_secndy", "Secondary", "secondary"),
                VariationOf("v_succes", "Success", "success"),
            ],
            ["checkout", "experiment"],
            createdAt,
            new()
            {
                [Development] = On("v_primry", Serve.Variation("v_primry"),
                    rules: [Rule("r_staff1", "Internal staff", Serve.Variation("v_succes"), Clause("email", ClauseOperator.EndsWith, "@acme.com"))]),
                [Staging] = On("v_primry", Rollout(("v_primry", 34_000), ("v_secndy", 33_000), ("v_succes", 33_000))),
                [Production] = On("v_primry", Serve.Variation("v_primry"),
                    rules: [Rule("r_staff1", "Internal staff", Serve.Variation("v_succes"), Clause("email", ClauseOperator.EndsWith, "@acme.com"))]),
            });

        yield return new DemoFlag(
            "max-cart-items",
            "Maximum cart items",
            "How many items a cart can hold.",
            FlagType.Number,
            [
                VariationOf("v_items3", "Three", 3),
                VariationOf("v_items5", "Five", 5),
                VariationOf("v_item10", "Ten", 10),
            ],
            ["checkout"],
            createdAt,
            new()
            {
                [Development] = On("v_items5", Serve.Variation("v_items5"), rules:
                [
                    Rule("r_entrpr", "Enterprise customers", Serve.Variation("v_item10"), Clause("plan", ClauseOperator.In, "enterprise")),
                    Rule("r_freepl", "Free plan", Serve.Variation("v_items3"), Clause("plan", ClauseOperator.In, "free")),
                ]),
                [Staging] = On("v_items5", Serve.Variation("v_items5")),
                [Production] = On("v_items5", Serve.Variation("v_items5"), rules:
                [
                    Rule("r_paidpl", "Paid plans", Serve.Variation("v_item10"), paidPlans),
                    Rule("r_freepl", "Free plan", Serve.Variation("v_items3"), Clause("plan", ClauseOperator.In, "free")),
                ]),
            },
            IsPermanent: true);

        yield return new DemoFlag(
            "store-theme",
            "Store theme",
            "Accent color and corner style of the storefront.",
            FlagType.Json,
            [
                VariationOf("v_matcha", "Matcha", new { accent = "#5E7F4F", rounded = true }),
                VariationOf("v_caraml", "Caramel", new { accent = "#A0522D", rounded = false }),
            ],
            ["storefront"],
            createdAt,
            new()
            {
                [Development] = On("v_matcha", Serve.Variation("v_matcha"),
                    rules: [Rule("r_betaus", "Beta testers", Serve.Variation("v_caraml"), Clause("beta", ClauseOperator.In, "true"))]),
                [Staging] = On("v_matcha", Serve.Variation("v_matcha")),
                [Production] = On("v_matcha", Serve.Variation("v_matcha")),
            });

        yield return new DemoFlag(
            "legacy-search",
            "Legacy search",
            "Routes search to the old search service.",
            FlagType.Boolean,
            Variation.ForBoolean(),
            ["search"],
            now.AddDays(-120),
            [],
            ArchivedAt: now.AddDays(-30));

        yield return new DemoFlag(
            "dark-mode-beta",
            "Dark mode beta",
            "Offers the dark theme to beta testers. Never rolled out, so it shows up as stale.",
            FlagType.Boolean,
            Variation.ForBoolean(),
            ["storefront"],
            now.AddDays(-60),
            []);
    }

    private static TargetingConfig On(string offVariationId, Serve fallthrough, IReadOnlyList<Target>? targets = null, IReadOnlyList<Rule>? rules = null) =>
        new()
        {
            Enabled = true,
            OffVariationId = offVariationId,
            Fallthrough = fallthrough,
            Targets = targets ?? [],
            Rules = rules ?? [],
        };

    private static Rule Rule(string id, string description, Serve serve, params Clause[] clauses) =>
        new() { Id = id, Description = description, Serve = serve, Clauses = clauses };

    private static Clause Clause(string attribute, ClauseOperator op, params string[] values) =>
        new() { Attribute = attribute, Operator = op, Values = values };

    private static Serve Rollout(params (string VariationId, int Weight)[] weights) =>
        Serve.PercentageRollout(new Rollout
        {
            Weights = [.. weights.Select(w => new WeightedVariation { VariationId = w.VariationId, Weight = w.Weight })],
        });

    private static Variation VariationOf(string id, string name, object value) =>
        new() { Id = id, Name = name, Value = JsonSerializer.SerializeToElement(value, JsonDefaults.Options) };

    private sealed record DemoFlag(
        string Key,
        string Name,
        string Description,
        FlagType Type,
        IReadOnlyList<Variation> Variations,
        IReadOnlyList<string> Tags,
        DateTimeOffset CreatedAt,
        Dictionary<string, TargetingConfig> Targeting,
        bool IsPermanent = false,
        DateTimeOffset? ArchivedAt = null);

    [LoggerMessage(Level = LogLevel.Information, Message = "FF_SEED_DEMO_DATA is not true; skipping demo data")]
    private static partial void LogDisabled(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Project {ProjectKey} already exists; skipping demo data")]
    private static partial void LogSkipped(ILogger logger, string projectKey);

    [LoggerMessage(Level = LogLevel.Information, Message = "Seeded demo project {ProjectKey}")]
    private static partial void LogSeeded(ILogger logger, string projectKey);
}
