namespace FlagForge.Domain;

public sealed class Project
{
    public const int MaxNameLength = 100;
    public const int MaxDescriptionLength = 1000;

    public Guid Id { get; init; }

    /// <summary>Immutable after creation.</summary>
    public required string Key { get; init; }

    public required string Name { get; set; }

    public string? Description { get; set; }

    public DateTimeOffset CreatedAt { get; init; }

    public Guid CreatedByUserId { get; init; }

    public ICollection<ProjectEnvironment> Environments { get; init; } = [];

    public ICollection<Flag> Flags { get; init; } = [];
}
