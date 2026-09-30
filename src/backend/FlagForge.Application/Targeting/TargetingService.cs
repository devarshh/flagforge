using System.Globalization;
using FlagForge.Application.Common;
using FlagForge.Application.Evaluations;
using FlagForge.Domain;
using FlagForge.Evaluation;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace FlagForge.Application.Targeting;

/// <summary>
/// Reads and changes a flag's targeting in one environment. Every change runs in one transaction: optimistic
/// concurrency on the config's <c>Version</c>, an atomic <c>ConfigVersion</c> bump, and the audit entry. The Redis
/// notification is published only after commit. The worker applies scheduled changes through the same methods.
/// </summary>
public sealed class TargetingService(
    IFlagForgeDbContext db,
    IAuditWriter audit,
    IChangeNotifier notifier,
    TimeProvider timeProvider,
    IValidator<UpdateTargetingRequest> updateValidator,
    IValidator<ToggleRequest> toggleValidator)
{
    public const string SchedulerName = "scheduler";

    public async Task<TargetingResponse> GetAsync(string projectKey, string flagKey, string environmentKey, CancellationToken cancellationToken)
    {
        var project = await db.GetProjectAsync(projectKey, cancellationToken);
        var flag = await db.GetFlagAsync(project, flagKey, cancellationToken);
        var environment = await db.GetEnvironmentAsync(project, environmentKey, cancellationToken);
        var config = await db.FlagEnvironmentConfigs.AsNoTracking()
            .FirstAsync(c => c.FlagId == flag.Id && c.EnvironmentId == environment.Id, cancellationToken);
        return TargetingResponse.From(config, environment.Key, await UserRefAsync(config.UpdatedByUserId, cancellationToken));
    }

    public async Task<TargetingResponse> UpdateAsync(
        string projectKey, string flagKey, string environmentKey, UpdateTargetingRequest request, Actor actor, CancellationToken cancellationToken)
    {
        await updateValidator.EnsureValidAsync(request, cancellationToken);
        var (response, change) = await db.ExecuteInTransactionAsync(
            async token =>
            {
                var scope = await LoadAsync(projectKey, flagKey, environmentKey, token);
                EnsureCanChange(scope, actor, request.Comment);
                var variationIds = scope.Flag.VariationIds;
                TargetingValidator.Validate(request.Config, variationIds).ThrowIfAny("config");
                EnsureExpectedVersion(scope.Config, request.ExpectedVersion);
                return await ReplaceAsync(scope, TargetingNormalizer.Normalize(request.Config, variationIds), AuditActions.FlagTargetingUpdated, actor, request.Comment, token);
            },
            cancellationToken);
        await notifier.PublishConfigChangedAsync(change is null ? [] : [change], cancellationToken);
        return response;
    }

    /// <summary>Turns the flag on or off. Asking for the current state is a no-op that returns the config.</summary>
    public async Task<TargetingResponse> ToggleAsync(
        string projectKey, string flagKey, string environmentKey, ToggleRequest request, Actor actor, CancellationToken cancellationToken)
    {
        await toggleValidator.EnsureValidAsync(request, cancellationToken);
        var (response, change) = await db.ExecuteInTransactionAsync(
            async token =>
            {
                var scope = await LoadAsync(projectKey, flagKey, environmentKey, token);
                EnsureCanChange(scope, actor, request.Comment);
                if (request.ExpectedVersion is { } expected)
                {
                    EnsureExpectedVersion(scope.Config, expected);
                }

                return await SetEnabledAsync(scope, request.Enabled, actor, request.Comment, token);
            },
            cancellationToken);
        await notifier.PublishConfigChangedAsync(change is null ? [] : [change], cancellationToken);
        return response;
    }

    /// <summary>Evaluates a context against the saved or a draft config. Preview evaluations are not counted as usage.</summary>
    public async Task<EvaluationResultResponse> PreviewAsync(
        string projectKey, string flagKey, string environmentKey, PreviewRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!EvaluationContextParser.TryParse(request.Context, out var context, out var contextErrors))
        {
            contextErrors.ThrowIfAny("context");
        }

        var project = await db.GetProjectAsync(projectKey, cancellationToken);
        var flag = await db.GetFlagAsync(project, flagKey, cancellationToken);
        var environment = await db.GetEnvironmentAsync(project, environmentKey, cancellationToken);
        TargetingConfig config;
        if (request.DraftConfig is { } draft)
        {
            TargetingValidator.Validate(draft, flag.VariationIds).ThrowIfAny("draftConfig");
            config = TargetingNormalizer.Normalize(draft, flag.VariationIds);
        }
        else
        {
            var saved = await db.FlagEnvironmentConfigs.AsNoTracking()
                .FirstAsync(c => c.FlagId == flag.Id && c.EnvironmentId == environment.Id, cancellationToken);
            config = saved.ToTargetingConfig();
        }

        var compiled = CompiledFlag.Compile(flag.Key, flag.Salt, flag.ToFlagVariations(), config, flag.IsArchived);
        return EvaluationResultResponse.From(FlagEvaluator.Evaluate(flag.Key, compiled, context!));
    }

    /// <summary>
    /// Applies a due scheduled change as the system "scheduler" actor. Runs inside the caller's transaction; the
    /// caller publishes the returned notification after commit (null when the change was already in effect).
    /// </summary>
    public async Task<AppliedScheduledChange> ApplyScheduledChangeAsync(ScheduledChange change, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(change);
        var scope = await LoadAsync(change.FlagId, change.EnvironmentId, cancellationToken);
        var actor = Actor.System(SchedulerName);
        var comment = ScheduledComment(change);
        EnsureCanChange(scope, actor, comment);

        var (_, message) = change.Action switch
        {
            ScheduledChangeAction.TurnOn => await SetEnabledAsync(scope, enabled: true, actor, comment, cancellationToken),
            ScheduledChangeAction.TurnOff => await SetEnabledAsync(scope, enabled: false, actor, comment, cancellationToken),
            ScheduledChangeAction.SetFallthrough => await SetFallthroughAsync(scope, change.Payload, actor, comment, cancellationToken),
            _ => throw new InvalidOperationException($"Unknown scheduled action {change.Action}."),
        };
        return new AppliedScheduledChange(AuditTarget.For(scope.Project, scope.Flag, scope.Environment), message);
    }

    /// <summary>The audit comment on changes the scheduler makes: "Scheduled by Sam on 2026-10-01 09:00 UTC".</summary>
    public static string ScheduledComment(ScheduledChange change)
    {
        ArgumentNullException.ThrowIfNull(change);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"Scheduled by {change.CreatedBy.DisplayName} on {change.CreatedAt.UtcDateTime:yyyy-MM-dd HH:mm} UTC");
    }

    private async Task<(TargetingResponse, ConfigChangedMessage?)> SetFallthroughAsync(
        TargetingScope scope, Serve? payload, Actor actor, string comment, CancellationToken cancellationToken)
    {
        if (payload is null)
        {
            throw RequestValidationException.For("payload", "A fallthrough change needs a serve payload.");
        }

        var variationIds = scope.Flag.VariationIds;
        var config = scope.Config.ToTargetingConfig() with { Fallthrough = payload };
        TargetingValidator.Validate(config, variationIds).ThrowIfAny("config");
        return await ReplaceAsync(scope, TargetingNormalizer.Normalize(config, variationIds), AuditActions.FlagTargetingUpdated, actor, comment, cancellationToken);
    }

    private async Task<(TargetingResponse, ConfigChangedMessage?)> SetEnabledAsync(
        TargetingScope scope, bool enabled, Actor actor, string? comment, CancellationToken cancellationToken)
    {
        if (scope.Config.Enabled == enabled)
        {
            return (TargetingResponse.From(scope.Config, scope.Environment.Key, await UserRefAsync(scope.Config.UpdatedByUserId, cancellationToken)), null);
        }

        var before = new { Enabled = !enabled, scope.Config.Version };
        scope.Config.Apply(scope.Config.ToTargetingConfig() with { Enabled = enabled }, timeProvider.GetUtcNow(), actor.UserId);
        var after = new { Enabled = enabled, scope.Config.Version };
        return await SaveAsync(scope, AuditActions.FlagToggled, before, after, actor, comment, cancellationToken);
    }

    private async Task<(TargetingResponse, ConfigChangedMessage?)> ReplaceAsync(
        TargetingScope scope, TargetingConfig config, string action, Actor actor, string? comment, CancellationToken cancellationToken)
    {
        var before = Snapshot(scope.Config);
        scope.Config.Apply(config, timeProvider.GetUtcNow(), actor.UserId);
        return await SaveAsync(scope, action, before, Snapshot(scope.Config), actor, comment, cancellationToken);
    }

    private async Task<(TargetingResponse, ConfigChangedMessage?)> SaveAsync(
        TargetingScope scope, string action, object before, object after, Actor actor, string? comment, CancellationToken cancellationToken)
    {
        audit.Record(actor, action, AuditTarget.For(scope.Project, scope.Flag, scope.Environment), comment, before, after);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            var current = await db.FlagEnvironmentConfigs.AsNoTracking()
                .Where(c => c.Id == scope.Config.Id)
                .Select(c => c.Version)
                .FirstAsync(cancellationToken);
            throw VersionConflict(current);
        }

        var configVersion = await db.IncrementConfigVersionAsync(scope.Environment.Id, cancellationToken);
        var updatedBy = actor.UserId is { } userId ? new UserRef(userId, actor.Name) : null;
        return (TargetingResponse.From(scope.Config, scope.Environment.Key, updatedBy), new ConfigChangedMessage(scope.Environment.Id, configVersion));
    }

    private static void EnsureCanChange(TargetingScope scope, Actor actor, string? comment)
    {
        if (scope.Flag.IsArchived)
        {
            throw new ConflictException("This flag is archived. Restore it before changing its targeting.");
        }

        if (!scope.Environment.IsProtected)
        {
            return;
        }

        if (!actor.CanChangeProtectedEnvironments)
        {
            throw new ForbiddenException($"{scope.Environment.Name} is a protected environment. Only admins can change it.");
        }

        if (string.IsNullOrWhiteSpace(comment))
        {
            throw RequestValidationException.For("comment", $"Add a comment. Changes to {scope.Environment.Name} need one because it is protected.");
        }
    }

    private static void EnsureExpectedVersion(FlagEnvironmentConfig config, int expectedVersion)
    {
        if (config.Version != expectedVersion)
        {
            throw VersionConflict(config.Version);
        }
    }

    private static ConflictException VersionConflict(int currentVersion) =>
        new(
            "This flag was changed by someone else while you were editing. Load the latest version and try again.",
            new Dictionary<string, object?> { ["currentVersion"] = currentVersion });

    private static object Snapshot(FlagEnvironmentConfig config) => new
    {
        config.Enabled,
        config.OffVariationId,
        config.Targets,
        config.Rules,
        config.Fallthrough,
        config.Version,
    };

    private async Task<TargetingScope> LoadAsync(string projectKey, string flagKey, string environmentKey, CancellationToken cancellationToken)
    {
        var project = await db.GetProjectAsync(projectKey, cancellationToken);
        var flag = await db.GetFlagAsync(project, flagKey, cancellationToken);
        var environment = await db.GetEnvironmentAsync(project, environmentKey, cancellationToken);
        var config = await db.FlagEnvironmentConfigs.FirstAsync(c => c.FlagId == flag.Id && c.EnvironmentId == environment.Id, cancellationToken);
        return new TargetingScope(project, flag, environment, config);
    }

    private async Task<TargetingScope> LoadAsync(Guid flagId, Guid environmentId, CancellationToken cancellationToken)
    {
        var config = await db.FlagEnvironmentConfigs
            .Include(c => c.Flag).ThenInclude(f => f.Project)
            .Include(c => c.Environment)
            .FirstOrDefaultAsync(c => c.FlagId == flagId && c.EnvironmentId == environmentId, cancellationToken)
            ?? throw new NotFoundException("The flag or environment of this scheduled change no longer exists.");
        return new TargetingScope(config.Flag.Project, config.Flag, config.Environment, config);
    }

    private async Task<UserRef?> UserRefAsync(Guid? userId, CancellationToken cancellationToken) =>
        userId is null
            ? null
            : await db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => new UserRef(u.Id, u.DisplayName)).FirstOrDefaultAsync(cancellationToken);

    private sealed record TargetingScope(Project Project, Flag Flag, ProjectEnvironment Environment, FlagEnvironmentConfig Config);
}
