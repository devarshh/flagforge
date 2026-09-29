namespace FlagForge.Domain;

public static class AuditActions
{
    public const string ProjectCreated = "project.created";
    public const string ProjectUpdated = "project.updated";
    public const string ProjectDeleted = "project.deleted";
    public const string EnvironmentCreated = "environment.created";
    public const string EnvironmentUpdated = "environment.updated";
    public const string EnvironmentDeleted = "environment.deleted";
    public const string SdkKeyCreated = "sdkkey.created";
    public const string SdkKeyRevoked = "sdkkey.revoked";
    public const string FlagCreated = "flag.created";
    public const string FlagUpdated = "flag.updated";
    public const string FlagVariationsUpdated = "flag.variations.updated";
    public const string FlagArchived = "flag.archived";
    public const string FlagRestored = "flag.restored";
    public const string FlagDeleted = "flag.deleted";
    public const string FlagTargetingUpdated = "flag.targeting.updated";
    public const string FlagToggled = "flag.toggled";
    public const string ScheduleCreated = "schedule.created";
    public const string ScheduleCancelled = "schedule.cancelled";
    public const string ScheduleExecuted = "schedule.executed";
    public const string ScheduleFailed = "schedule.failed";
    public const string UserCreated = "user.created";
    public const string UserUpdated = "user.updated";
    public const string UserDeactivated = "user.deactivated";
    public const string UserPasswordReset = "user.password_reset";

    public static IReadOnlyList<string> All { get; } =
    [
        ProjectCreated, ProjectUpdated, ProjectDeleted,
        EnvironmentCreated, EnvironmentUpdated, EnvironmentDeleted,
        SdkKeyCreated, SdkKeyRevoked,
        FlagCreated, FlagUpdated, FlagVariationsUpdated, FlagArchived, FlagRestored, FlagDeleted,
        FlagTargetingUpdated, FlagToggled,
        ScheduleCreated, ScheduleCancelled, ScheduleExecuted, ScheduleFailed,
        UserCreated, UserUpdated, UserDeactivated, UserPasswordReset,
    ];
}
