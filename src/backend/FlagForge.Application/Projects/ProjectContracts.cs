using FlagForge.Application.Environments;

namespace FlagForge.Application.Projects;

public sealed record ProjectResponse(
    Guid Id,
    string Key,
    string Name,
    string? Description,
    DateTimeOffset CreatedAt,
    IReadOnlyList<EnvironmentResponse> Environments);

public sealed record CreateProjectRequest(string Key, string Name, string? Description = null);

/// <summary>Partial update: null fields are left unchanged; an empty description clears it. Keys are immutable.</summary>
public sealed record UpdateProjectRequest(string? Name = null, string? Description = null);
