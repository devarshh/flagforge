namespace FlagForge.Domain;

public sealed class User
{
    public const int MaxEmailLength = 256;
    public const int MaxDisplayNameLength = 100;
    public const int MinPasswordLength = 10;

    public Guid Id { get; init; }

    /// <summary>Stored lower-case; unique.</summary>
    public required string Email { get; set; }

    public required string DisplayName { get; set; }

    public string PasswordHash { get; set; } = string.Empty;

    public Role Role { get; set; }

    public bool IsActive { get; set; } = true;

    public bool MustChangePassword { get; set; }

    public int FailedLoginCount { get; set; }

    public DateTimeOffset? LockoutEndsAt { get; set; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? LastLoginAt { get; set; }

    public static string NormalizeEmail(string email)
    {
        ArgumentNullException.ThrowIfNull(email);
        return email.Trim().ToLowerInvariant();
    }
}
