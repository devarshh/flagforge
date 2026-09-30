using FlagForge.Domain;
using FluentValidation;

namespace FlagForge.Application.SdkKeys;

public sealed class CreateSdkKeyRequestValidator : AbstractValidator<CreateSdkKeyRequest>
{
    public CreateSdkKeyRequestValidator()
    {
        RuleFor(r => r.Name)
            .NotEmpty().WithMessage("Name the key after the app that uses it.")
            .MaximumLength(SdkKey.MaxNameLength).WithMessage("Names can be at most 100 characters.");
    }
}
