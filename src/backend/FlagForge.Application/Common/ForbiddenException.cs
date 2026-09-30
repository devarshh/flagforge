namespace FlagForge.Application.Common;

public sealed class ForbiddenException(string message) : AppException(message);
