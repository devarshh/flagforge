namespace FlagForge.Domain;

/// <summary>A rotating refresh token. Only the SHA-256 hash of the token is stored.</summary>
public sealed class RefreshToken
{
    public Guid Id { get; init; }

    public Guid UserId { get; init; }

    public required string TokenHash { get; init; }

    public DateTimeOffset ExpiresAt { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? RevokedAt { get; set; }

    public string? ReplacedByTokenHash { get; set; }

    public User User { get; init; } = null!;
}
