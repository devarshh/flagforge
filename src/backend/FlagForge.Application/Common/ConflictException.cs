namespace FlagForge.Application.Common;

/// <summary>A conflict with the current state (409), optionally with ProblemDetails extensions such as <c>currentVersion</c>.</summary>
public sealed class ConflictException(string message, IReadOnlyDictionary<string, object?>? extensions = null)
    : AppException(message)
{
    public IReadOnlyDictionary<string, object?> Extensions { get; } = extensions ?? new Dictionary<string, object?>();
}
