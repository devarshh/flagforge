using System.ComponentModel.DataAnnotations;
using FlagForge.Domain;
using Microsoft.Extensions.Configuration;

namespace FlagForge.Migrator.Seeding;

/// <summary>Seed settings, read from the <c>FF_SEED_*</c> environment variables.</summary>
public sealed class SeedOptions : IValidatableObject
{
    [ConfigurationKeyName("FF_SEED_ADMIN_EMAIL")]
    public string? AdminEmail { get; set; }

    [ConfigurationKeyName("FF_SEED_ADMIN_PASSWORD")]
    public string? AdminPassword { get; set; }

    [ConfigurationKeyName("FF_SEED_DEMO_DATA")]
    public bool DemoData { get; set; }

    [ConfigurationKeyName("FF_SEED_DEMO_USER_PASSWORD")]
    public string? DemoUserPassword { get; set; }

    [ConfigurationKeyName("FF_SEED_DEMO_SDK_KEY")]
    public string? DemoSdkKey { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!string.IsNullOrEmpty(AdminEmail) && !new EmailAddressAttribute().IsValid(AdminEmail))
        {
            yield return new ValidationResult("FF_SEED_ADMIN_EMAIL must be an email address.", [nameof(AdminEmail)]);
        }

        if (!string.IsNullOrEmpty(AdminPassword) && AdminPassword.Length < User.MinPasswordLength)
        {
            yield return new ValidationResult("FF_SEED_ADMIN_PASSWORD must be at least 10 characters.", [nameof(AdminPassword)]);
        }

        if (!DemoData)
        {
            yield break;
        }

        if (DemoUserPassword is null || DemoUserPassword.Length < User.MinPasswordLength)
        {
            yield return new ValidationResult(
                "FF_SEED_DEMO_USER_PASSWORD must be at least 10 characters when FF_SEED_DEMO_DATA is true.",
                [nameof(DemoUserPassword)]);
        }

        if (!SdkKeyFormat.LooksLikeSdkKey(DemoSdkKey))
        {
            yield return new ValidationResult(
                "FF_SEED_DEMO_SDK_KEY must start with 'ffk_' (at most 128 characters) when FF_SEED_DEMO_DATA is true.",
                [nameof(DemoSdkKey)]);
        }
    }
}
