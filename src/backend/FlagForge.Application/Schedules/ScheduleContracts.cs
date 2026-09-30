using FlagForge.Application.Common;
using FlagForge.Domain;
using FlagForge.Evaluation;

namespace FlagForge.Application.Schedules;

public sealed record ScheduledChangeResponse(
    Guid Id,
    string EnvironmentKey,
    DateTimeOffset ExecuteAt,
    ScheduledChangeAction Action,
    Serve? Payload,
    ScheduledChangeStatus Status,
    int AttemptCount,
    DateTimeOffset? ExecutedAt,
    string? Error,
    Guid? ReleasePlanId,
    DateTimeOffset CreatedAt,
    UserRef CreatedBy)
{
    public static ScheduledChangeResponse From(ScheduledChange change, string environmentKey, UserRef createdBy)
    {
        ArgumentNullException.ThrowIfNull(change);
        return new ScheduledChangeResponse(
            change.Id,
            environmentKey,
            change.ExecuteAt,
            change.Action,
            change.Payload,
            change.Status,
            change.AttemptCount,
            change.ExecutedAt,
            change.Error,
            change.ReleasePlanId,
            change.CreatedAt,
            createdBy);
    }
}

/// <summary><see cref="Payload"/> is required for <see cref="ScheduledChangeAction.SetFallthrough"/> and not allowed otherwise.</summary>
public sealed record CreateScheduledChangeRequest(DateTimeOffset ExecuteAt, ScheduledChangeAction Action, Serve? Payload = null);

public sealed record ReleasePlanStep(DateTimeOffset ExecuteAt, IReadOnlyList<WeightedVariation> Weights);

/// <summary>1–10 steps in ascending time; each becomes a <see cref="ScheduledChangeAction.SetFallthrough"/> rollout.</summary>
public sealed record CreateReleasePlanRequest(IReadOnlyList<ReleasePlanStep> Steps, string BucketBy = AttributeNames.Key);

/// <summary>A change claimed by a worker: its id and the attempt number the claim recorded.</summary>
public sealed record ClaimedChange(Guid Id, int AttemptCount);
