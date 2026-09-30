using FlagForge.Domain;

namespace FlagForge.Application.Environments;

public sealed record EnvironmentResponse(
    Guid Id,
    string Key,
    string Name,
    string Color,
    bool IsProtected,
    int SortOrder,
    long ConfigVersion,
    DateTimeOffset CreatedAt)
{
    public static EnvironmentResponse From(ProjectEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        return new EnvironmentResponse(
            environment.Id,
            environment.Key,
            environment.Name,
            environment.Color,
            environment.IsProtected,
            environment.SortOrder,
            environment.ConfigVersion,
            environment.CreatedAt);
    }
}

public sealed record CreateEnvironmentRequest(string Key, string Name, string Color, bool IsProtected = false);

/// <summary>Partial update: null fields are left unchanged. Keys are immutable.</summary>
public sealed record UpdateEnvironmentRequest(string? Name = null, string? Color = null, bool? IsProtected = null, int? SortOrder = null);
