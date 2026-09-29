using FlagForge.Evaluation;

namespace FlagForge.Domain;

/// <summary>A change to one flag in one environment, executed by the worker at <see cref="ExecuteAt"/>.</summary>
public sealed class ScheduledChange
{
    public const int MaxAttempts = 3;
    public const int MaxErrorLength = 2000;

    public Guid Id { get; init; }

    public Guid FlagId { get; init; }

    public Guid EnvironmentId { get; init; }

    public DateTimeOffset ExecuteAt { get; init; }

    public ScheduledChangeAction Action { get; init; }

    /// <summary>The serve to apply for <see cref="ScheduledChangeAction.SetFallthrough"/>; null otherwise.</summary>
    public Serve? Payload { get; init; }

    public ScheduledChangeStatus Status { get; set; } = ScheduledChangeStatus.Pending;

    public int AttemptCount { get; set; }

    public DateTimeOffset? ClaimedUntil { get; set; }

    public DateTimeOffset? ExecutedAt { get; set; }

    public string? Error { get; set; }

    /// <summary>Groups the steps of a release plan created together.</summary>
    public Guid? ReleasePlanId { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public Guid CreatedByUserId { get; init; }

    public Flag Flag { get; init; } = null!;

    public ProjectEnvironment Environment { get; init; } = null!;

    public User CreatedBy { get; init; } = null!;
}
