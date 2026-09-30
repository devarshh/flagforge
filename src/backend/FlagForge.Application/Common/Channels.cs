namespace FlagForge.Application.Common;

/// <summary>Redis pub/sub channels that carry change notifications (never data).</summary>
public static class Channels
{
    public const string ConfigChanged = "flagforge:config-changed";
    public const string SdkKeyRevoked = "flagforge:sdk-key-revoked";
}
