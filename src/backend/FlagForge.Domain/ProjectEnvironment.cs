namespace FlagForge.Domain;

/// <summary>
/// An environment of a project (an "environment" in the API and dashboard; named ProjectEnvironment to avoid
/// clashing with <see cref="System.Environment"/>).
/// </summary>
public sealed class ProjectEnvironment
{
    public const int MaxNameLength = 100;

    public Guid Id { get; init; }

    public Guid ProjectId { get; init; }

    /// <summary>Unique per project; immutable after creation.</summary>
    public required string Key { get; init; }

    public required string Name { get; set; }

    /// <summary>Hex color such as <c>#3A7CA5</c>, used for UI chips.</summary>
    public required string Color { get; set; }

    public bool IsProtected { get; set; }

    public int SortOrder { get; set; }

    /// <summary>
    /// Incremented atomically in the database whenever anything that affects evaluation output changes; SDK clients
    /// receive it as <c>environmentVersion</c>.
    /// </summary>
    public long ConfigVersion { get; init; } = 1;

    public DateTimeOffset CreatedAt { get; init; }

    public Project Project { get; init; } = null!;
}
