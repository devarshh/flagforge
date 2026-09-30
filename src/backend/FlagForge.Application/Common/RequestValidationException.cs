namespace FlagForge.Application.Common;

/// <summary>Invalid input (400). <see cref="Errors"/> is keyed by camelCase field path, such as <c>config.rules[1].id</c>.</summary>
public sealed class RequestValidationException(IReadOnlyDictionary<string, string[]> errors)
    : AppException("One or more fields are invalid.")
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;

    public static RequestValidationException For(string path, string message) => new(new Dictionary<string, string[]> { [path] = [message] });
}
