namespace FlagForge.Application.Common;

/// <summary>An expected failure that maps to an HTTP ProblemDetails response.</summary>
public abstract class AppException(string message) : Exception(message);
