using FlagForge.Application.Common;
using FlagForge.Application.Targeting;
using FlagForge.Domain;
using FlagForge.Evaluation;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace FlagForge.Application.Schedules;

public enum ScheduledChangeOutcome
{
    /// <summary>Applied and marked completed.</summary>
    Executed,

    /// <summary>Another worker owns the claim now, or the change was cancelled; nothing was done.</summary>
    Skipped,

    /// <summary>An earlier change for the same flag and environment is still open; retried on the next poll.</summary>
    Deferred,
}

/// <summary>
/// Scheduled changes and release plans. The worker executes due changes through <see cref="ExecuteAsync"/>, which
/// applies them with <see cref="TargetingService"/> so versions, audit entries, and notifications match the API.
/// </summary>
public sealed class ScheduleService(
    IFlagForgeDbContext db,
    IAuditWriter audit,
    IChangeNotifier notifier,
    TargetingService targeting,
    TimeProvider timeProvider,
    IValidator<CreateScheduledChangeRequest> createValidator,
    IValidator<CreateReleasePlanRequest> releasePlanValidator)
{
    /// <summary>Clients may send a time up to 30 seconds in the past (clock skew, slow submits).</summary>
    public static readonly TimeSpan PastTolerance = TimeSpan.FromSeconds(30);

    public async Task<IReadOnlyList<ScheduledChangeResponse>> ListAsync(
        string projectKey, string flagKey, string environmentKey, CancellationToken cancellationToken)
    {
        var project = await db.GetProjectAsync(projectKey, cancellationToken);
        var flag = await db.GetFlagAsync(project, flagKey, cancellationToken);
        var environment = await db.GetEnvironmentAsync(project, environmentKey, cancellationToken);
        var changes = await db.ScheduledChanges.AsNoTracking()
            .Include(s => s.CreatedBy)
            .Where(s => s.FlagId == flag.Id && s.EnvironmentId == environment.Id)
            .OrderBy(s => s.ExecuteAt)
            .ThenBy(s => s.CreatedAt)
            .ToListAsync(cancellationToken);
        return [.. changes.Select(c => ScheduledChangeResponse.From(c, environment.Key, new UserRef(c.CreatedBy.Id, c.CreatedBy.DisplayName)))];
    }

    public async Task<ScheduledChangeResponse> GetAsync(
        string projectKey, string flagKey, string environmentKey, Guid changeId, CancellationToken cancellationToken)
    {
        var project = await db.GetProjectAsync(projectKey, cancellationToken);
        var flag = await db.GetFlagAsync(project, flagKey, cancellationToken);
        var environment = await db.GetEnvironmentAsync(project, environmentKey, cancellationToken);
        var change = await db.ScheduledChanges.AsNoTracking()
            .Include(s => s.CreatedBy)
            .FirstOrDefaultAsync(s => s.Id == changeId && s.FlagId == flag.Id && s.EnvironmentId == environment.Id, cancellationToken)
            ?? throw new NotFoundException("That scheduled change does not exist.");
        return ScheduledChangeResponse.From(change, environment.Key, new UserRef(change.CreatedBy.Id, change.CreatedBy.DisplayName));
    }

    public async Task<ScheduledChangeResponse> CreateAsync(
        string projectKey, string flagKey, string environmentKey, CreateScheduledChangeRequest request, Actor actor, CancellationToken cancellationToken)
    {
        await createValidator.EnsureValidAsync(request, cancellationToken);
        var scope = await LoadForChangeAsync(projectKey, flagKey, environmentKey, actor, cancellationToken);
        var now = timeProvider.GetUtcNow();
        if (request.ExecuteAt < now - PastTolerance)
        {
            throw RequestValidationException.For("executeAt", "Choose a time in the future.");
        }

        var payload = request.Payload is null ? null : NormalizeServe(scope, request.Payload, "payload");
        var change = NewChange(scope, request.ExecuteAt, request.Action, payload, releasePlanId: null, actor, now);
        db.ScheduledChanges.Add(change);
        audit.Record(actor, AuditActions.ScheduleCreated, AuditTarget.For(scope.Project, scope.Flag, scope.Environment), after: Snapshot(change));
        await db.SaveChangesAsync(cancellationToken);
        return ScheduledChangeResponse.From(change, scope.Environment.Key, CreatorRef(actor));
    }

    /// <summary>Creates one <see cref="ScheduledChangeAction.SetFallthrough"/> rollout per step, sharing a release plan id.</summary>
    public async Task<IReadOnlyList<ScheduledChangeResponse>> CreateReleasePlanAsync(
        string projectKey, string flagKey, string environmentKey, CreateReleasePlanRequest request, Actor actor, CancellationToken cancellationToken)
    {
        await releasePlanValidator.EnsureValidAsync(request, cancellationToken);
        var scope = await LoadForChangeAsync(projectKey, flagKey, environmentKey, actor, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var errors = new List<ValidationError>();
        var serves = new List<Serve>();
        for (var i = 0; i < request.Steps.Count; i++)
        {
            var step = request.Steps[i];
            var path = $"steps[{i}]";
            if (step is null)
            {
                errors.Add(new ValidationError(path, "Step is missing."));
                continue;
            }

            if (step.ExecuteAt < now - PastTolerance)
            {
                errors.Add(new ValidationError($"{path}.executeAt", "Choose a time in the future."));
            }

            var serve = Serve.PercentageRollout(new Rollout { BucketBy = request.BucketBy, Weights = step.Weights ?? [] });
            var (normalized, stepErrors) = ValidateServe(scope, serve, fallthroughPath => fallthroughPath
                .Replace("fallthrough.rollout.bucketBy", "bucketBy", StringComparison.Ordinal)
                .Replace("fallthrough.rollout", path, StringComparison.Ordinal));
            errors.AddRange(stepErrors);
            serves.Add(normalized);
        }

        errors.DistinctBy(e => (e.Path, e.Message)).ToList().ThrowIfAny(string.Empty);

        var releasePlanId = Guid.CreateVersion7(now);
        var changes = request.Steps
            .Select((step, i) => NewChange(scope, step.ExecuteAt, ScheduledChangeAction.SetFallthrough, serves[i], releasePlanId, actor, now))
            .ToList();
        db.ScheduledChanges.AddRange(changes);
        audit.Record(actor, AuditActions.ScheduleCreated, AuditTarget.For(scope.Project, scope.Flag, scope.Environment), after: new
        {
            ReleasePlanId = releasePlanId,
            Steps = changes.Select(c => new { c.Id, c.ExecuteAt, c.Payload }),
        });
        await db.SaveChangesAsync(cancellationToken);
        var createdBy = CreatorRef(actor);
        return [.. changes.Select(c => ScheduledChangeResponse.From(c, scope.Environment.Key, createdBy))];
    }

    /// <summary>Cancels a pending change. The conditional update loses cleanly to a worker that already claimed it.</summary>
    public async Task CancelAsync(
        string projectKey, string flagKey, string environmentKey, Guid changeId, Actor actor, CancellationToken cancellationToken)
    {
        var scope = await LoadForChangeAsync(projectKey, flagKey, environmentKey, actor, cancellationToken, allowArchived: true);
        var change = await db.ScheduledChanges.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == changeId && s.FlagId == scope.Flag.Id && s.EnvironmentId == scope.Environment.Id, cancellationToken)
            ?? throw new NotFoundException("That scheduled change does not exist.");
        var cancelled = await db.ScheduledChanges
            .Where(s => s.Id == change.Id && s.Status == ScheduledChangeStatus.Pending)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.Status, ScheduledChangeStatus.Cancelled), cancellationToken);
        if (cancelled == 0)
        {
            throw new ConflictException($"Only pending changes can be cancelled. This change is {change.Status.ToString().ToLowerInvariant()}.");
        }

        audit.Record(actor, AuditActions.ScheduleCancelled, AuditTarget.For(scope.Project, scope.Flag, scope.Environment), before: Snapshot(change));
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Executes one claimed change. Marking it completed is the transaction's first statement, so the row lock
    /// makes this the only execution of the claim; a failure rolls everything back, including that mark.
    /// </summary>
    public async Task<ScheduledChangeOutcome> ExecuteAsync(ClaimedChange claim, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(claim);
        var now = timeProvider.GetUtcNow();
        var (outcome, notification) = await db.ExecuteInTransactionAsync<(ScheduledChangeOutcome, ConfigChangedMessage?)>(
            async token =>
            {
                var marked = await db.ScheduledChanges
                    .Where(s => s.Id == claim.Id && s.Status == ScheduledChangeStatus.Processing && s.AttemptCount == claim.AttemptCount)
                    .ExecuteUpdateAsync(
                        s => s.SetProperty(c => c.Status, ScheduledChangeStatus.Completed)
                            .SetProperty(c => c.ExecutedAt, now)
                            .SetProperty(c => c.ClaimedUntil, (DateTimeOffset?)null)
                            .SetProperty(c => c.Error, (string?)null),
                        token);
                if (marked == 0)
                {
                    return (ScheduledChangeOutcome.Skipped, null);
                }

                var change = await db.ScheduledChanges.AsNoTracking().Include(s => s.CreatedBy).FirstAsync(s => s.Id == claim.Id, token);

                // Release-plan steps must apply in order, even when several are due at once.
                var earlierOpen = await db.ScheduledChanges.AnyAsync(
                    s => s.FlagId == change.FlagId && s.EnvironmentId == change.EnvironmentId && s.Id != change.Id
                        && (s.Status == ScheduledChangeStatus.Pending || s.Status == ScheduledChangeStatus.Processing)
                        && (s.ExecuteAt < change.ExecuteAt || (s.ExecuteAt == change.ExecuteAt && s.CreatedAt < change.CreatedAt)),
                    token);
                if (earlierOpen)
                {
                    await db.ScheduledChanges
                        .Where(s => s.Id == claim.Id)
                        .ExecuteUpdateAsync(
                            s => s.SetProperty(c => c.Status, ScheduledChangeStatus.Pending)
                                .SetProperty(c => c.AttemptCount, claim.AttemptCount - 1)
                                .SetProperty(c => c.ExecutedAt, (DateTimeOffset?)null),
                            token);
                    return (ScheduledChangeOutcome.Deferred, null);
                }

                var applied = await targeting.ApplyScheduledChangeAsync(change, token);
                audit.Record(
                    Actor.System(TargetingService.SchedulerName),
                    AuditActions.ScheduleExecuted,
                    applied.Target,
                    TargetingService.ScheduledComment(change),
                    after: Snapshot(change));
                await db.SaveChangesAsync(token);
                return (ScheduledChangeOutcome.Executed, applied.Change);
            },
            cancellationToken);

        if (notification is not null)
        {
            await notifier.PublishConfigChangedAsync([notification], cancellationToken);
        }

        return outcome;
    }

    /// <summary>
    /// Records a failed attempt: back to pending while attempts remain, otherwise failed with a
    /// <c>schedule.failed</c> audit entry. Call with a fresh unit of work, not the one that failed.
    /// </summary>
    public async Task RecordFailureAsync(ClaimedChange claim, Exception exception, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(claim);
        var error = Describe(exception);
        await db.ExecuteInTransactionAsync(
            async token =>
            {
                var change = await db.ScheduledChanges
                    .Include(s => s.Flag).ThenInclude(f => f.Project)
                    .Include(s => s.Environment)
                    .FirstOrDefaultAsync(s => s.Id == claim.Id && s.Status == ScheduledChangeStatus.Processing && s.AttemptCount == claim.AttemptCount, token);
                if (change is null)
                {
                    return false;
                }

                change.Error = error;
                change.ClaimedUntil = null;
                if (change.AttemptCount < ScheduledChange.MaxAttempts)
                {
                    change.Status = ScheduledChangeStatus.Pending;
                }
                else
                {
                    change.Status = ScheduledChangeStatus.Failed;
                    audit.Record(
                        Actor.System(TargetingService.SchedulerName),
                        AuditActions.ScheduleFailed,
                        AuditTarget.For(change.Flag.Project, change.Flag, change.Environment),
                        $"Gave up after {ScheduledChange.MaxAttempts} attempts: {error}",
                        after: Snapshot(change));
                }

                await db.SaveChangesAsync(token);
                return true;
            },
            cancellationToken);
    }

    /// <summary>A readable, bounded description of why an attempt failed.</summary>
    public static string Describe(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        var description = exception is RequestValidationException validation
            ? string.Join("; ", validation.Errors.Select(e => $"{e.Key}: {string.Join(" ", e.Value)}"))
            : exception.Message;
        return description.Length <= ScheduledChange.MaxErrorLength ? description : description[..ScheduledChange.MaxErrorLength];
    }

    private async Task<Scope> LoadForChangeAsync(
        string projectKey, string flagKey, string environmentKey, Actor actor, CancellationToken cancellationToken, bool allowArchived = false)
    {
        var project = await db.GetProjectAsync(projectKey, cancellationToken);
        var flag = await db.GetFlagAsync(project, flagKey, cancellationToken);
        var environment = await db.GetEnvironmentAsync(project, environmentKey, cancellationToken);
        if (flag.IsArchived && !allowArchived)
        {
            throw new ConflictException("This flag is archived. Restore it before scheduling changes.");
        }

        if (environment.IsProtected && !actor.CanChangeProtectedEnvironments)
        {
            throw new ForbiddenException($"{environment.Name} is a protected environment. Only admins can schedule changes there.");
        }

        var config = await db.FlagEnvironmentConfigs.AsNoTracking()
            .FirstAsync(c => c.FlagId == flag.Id && c.EnvironmentId == environment.Id, cancellationToken);
        return new Scope(project, flag, environment, config);
    }

    private static Serve NormalizeServe(Scope scope, Serve serve, string path)
    {
        var (normalized, errors) = ValidateServe(scope, serve, fallthroughPath => path + fallthroughPath["fallthrough".Length..]);
        errors.ThrowIfAny(string.Empty);
        return normalized;
    }

    /// <summary>Validates a serve by placing it in the saved config as the fallthrough, then maps error paths.</summary>
    private static (Serve Normalized, List<ValidationError> Errors) ValidateServe(Scope scope, Serve serve, Func<string, string> mapPath)
    {
        var variationIds = scope.Flag.VariationIds;
        var candidate = scope.Config.ToTargetingConfig() with { Fallthrough = serve };
        var errors = TargetingValidator.Validate(candidate, variationIds)
            .Where(e => e.Path.StartsWith("fallthrough", StringComparison.Ordinal))
            .Select(e => e with { Path = mapPath(e.Path) })
            .ToList();
        return (TargetingNormalizer.Normalize(candidate, variationIds).Fallthrough, errors);
    }

    private static ScheduledChange NewChange(
        Scope scope, DateTimeOffset executeAt, ScheduledChangeAction action, Serve? payload, Guid? releasePlanId, Actor actor, DateTimeOffset now) =>
        new()
        {
            Id = Guid.CreateVersion7(now),
            FlagId = scope.Flag.Id,
            EnvironmentId = scope.Environment.Id,
            ExecuteAt = executeAt,
            Action = action,
            Payload = payload,
            ReleasePlanId = releasePlanId,
            CreatedAt = now,
            CreatedByUserId = actor.UserId ?? throw new InvalidOperationException("Scheduled changes are created by users."),
        };

    private static UserRef CreatorRef(Actor actor) => new(actor.UserId!.Value, actor.Name);

    private static object Snapshot(ScheduledChange change) => new { change.Id, change.Action, change.ExecuteAt, change.Payload, change.ReleasePlanId };

    private sealed record Scope(Project Project, Flag Flag, ProjectEnvironment Environment, FlagEnvironmentConfig Config);
}
