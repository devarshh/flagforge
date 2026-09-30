using System.ComponentModel.DataAnnotations;
using System.Text;

namespace FlagForge.Application.Common;

/// <summary>Authentication settings, bound from the <c>Auth</c> configuration section.</summary>
public sealed class AuthOptions : IValidatableObject
{
    public const string SectionName = "Auth";
    public const int MinSigningKeyBytes = 32;

    [Required(ErrorMessage = "Set Auth__JwtSigningKey (a secret of at least 32 bytes).")]
    public string JwtSigningKey { get; set; } = string.Empty;

    [Required]
    public string JwtIssuer { get; set; } = "flagforge";

    [Required]
    public string JwtAudience { get; set; } = "flagforge";

    [Range(1, 1440)]
    public int AccessTokenMinutes { get; set; } = 15;

    [Range(1, 90)]
    public int RefreshTokenDays { get; set; } = 7;

    /// <summary>Marks the refresh cookie Secure. Set to false only when the site is served over plain HTTP.</summary>
    public bool RefreshCookieSecure { get; set; } = true;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Encoding.UTF8.GetByteCount(JwtSigningKey) < MinSigningKeyBytes)
        {
            yield return new ValidationResult("Auth__JwtSigningKey must be at least 32 bytes.", [nameof(JwtSigningKey)]);
        }
    }
}
