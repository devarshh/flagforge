using System.Text.RegularExpressions;
using FlagForge.Domain;
using FluentValidation;

namespace FlagForge.Application.Common;

public static partial class ValidationRules
{
    /// <summary>Project, environment, and flag keys: <c>^[a-z0-9][a-z0-9._-]{0,63}$</c>.</summary>
    public static IRuleBuilderOptions<T, string> MustBeKey<T>(this IRuleBuilderInitial<T, string> rule) =>
        rule.Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Enter a key.")
            .Must(KeyFormat.IsValid).WithMessage(KeyFormat.Requirement);

    public static IRuleBuilderOptions<T, string> MustBeHexColor<T>(this IRuleBuilder<T, string> rule) =>
        rule.Must(color => color is not null && HexColor().IsMatch(color)).WithMessage("Use a hex color such as #3A7CA5.");

    [GeneratedRegex(@"^#[0-9A-Fa-f]{6}\z", RegexOptions.CultureInvariant)]
    private static partial Regex HexColor();
}
