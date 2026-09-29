using Microsoft.Extensions.Logging;

namespace FlagForge.Migrator;

internal static partial class MigratorLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Migrator finished successfully")]
    public static partial void Finished(ILogger logger);

    [LoggerMessage(Level = LogLevel.Critical, Message = "Migrator failed")]
    public static partial void Failed(ILogger logger, Exception exception);
}
