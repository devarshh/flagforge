using FlagForge.Application.Common;
using FlagForge.Domain;
using FluentValidation;

namespace FlagForge.Application.Environments;

public sealed class CreateEnvironmentRequestValidator : AbstractValidator<CreateEnvironmentRequest>
{
    public CreateEnvironmentRequestValidator()
    {
        RuleFor(r => r.Key).MustBeKey();
        RuleFor(r => r.Name)
            .NotEmpty().WithMessage("Enter a name.")
            .MaximumLength(ProjectEnvironment.MaxNameLength).WithMessage("Names can be at most 100 characters.");
        RuleFor(r => r.Color).MustBeHexColor();
    }
}

public sealed class UpdateEnvironmentRequestValidator : AbstractValidator<UpdateEnvironmentRequest>
{
    public UpdateEnvironmentRequestValidator()
    {
        RuleFor(r => r.Name!)
            .NotEmpty().WithMessage("Enter a name.")
            .MaximumLength(ProjectEnvironment.MaxNameLength).WithMessage("Names can be at most 100 characters.")
            .When(r => r.Name is not null);
        RuleFor(r => r.Color!).MustBeHexColor().When(r => r.Color is not null);
        RuleFor(r => r.SortOrder!.Value).GreaterThanOrEqualTo(0).WithMessage("Sort order cannot be negative.").When(r => r.SortOrder is not null);
    }
}
