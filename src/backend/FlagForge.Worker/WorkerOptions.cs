using System.ComponentModel.DataAnnotations;

namespace FlagForge.Worker;

/// <summary>Bound from the <c>Worker</c> configuration section.</summary>
public sealed class WorkerOptions
{
    public const string SectionName = "Worker";

    [Range(1, 3600)]
    public int ScheduledChangePollSeconds { get; set; } = 15;
}

/// <summary>Bound from the <c>Retention</c> configuration section.</summary>
public sealed class RetentionOptions
{
    public const string SectionName = "Retention";

    [Range(1, 3650)]
    public int UsageDays { get; set; } = 90;

    [Range(1, 36500)]
    public int AuditDays { get; set; } = 365;
}
