namespace FlagForge.Application.Common;

public sealed class UnauthorizedException(string message) : AppException(message);
