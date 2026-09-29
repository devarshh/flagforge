namespace FlagForge.Domain;

public static class SdkKeyFormat
{
    public const string Prefix = "ffk_";

    /// <summary>Upper bound for anything presented as an SDK key; longer values are rejected without hashing.</summary>
    public const int MaxLength = 128;

    public static bool LooksLikeSdkKey(string? value) =>
        value is { Length: > 4 and <= MaxLength } && value.StartsWith(Prefix, StringComparison.Ordinal);
}
