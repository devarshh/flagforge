namespace FlagForge.Domain;

/// <summary>
/// An SDK key for one environment. The plaintext (<c>ffk_</c> plus 43 base64url characters) is returned once at
/// creation; only its SHA-256 hash and a display prefix are stored.
/// </summary>
public sealed class SdkKey
{
    public const int DisplayPrefixLength = 12;
    public const int MaxNameLength = 100;

    public Guid Id { get; init; }

    public Guid EnvironmentId { get; init; }

    public required string Name { get; init; }

    public required string KeyPrefix { get; init; }

    public required string KeyHash { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public Guid CreatedByUserId { get; init; }

    public DateTimeOffset? RevokedAt { get; set; }

    public ProjectEnvironment Environment { get; init; } = null!;

    public static string DisplayPrefixOf(string plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        return plaintext.Length <= DisplayPrefixLength ? plaintext : plaintext[..DisplayPrefixLength];
    }
}
