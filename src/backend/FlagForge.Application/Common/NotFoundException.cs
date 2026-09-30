namespace FlagForge.Application.Common;

public sealed class NotFoundException(string message) : AppException(message);
