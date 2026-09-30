using FlagForge.Application.Common;
using FlagForge.Domain;
using FluentValidation;

namespace FlagForge.Application.Projects;

public sealed class CreateProjectRequestValidator : AbstractValidator<CreateProjectRequest>
{
    public CreateProjectRequestValidator()
    {
        RuleFor(r => r.Key).MustBeKey();
        RuleFor(r => r.Name)
            .NotEmpty().WithMessage("Enter a name.")
            .MaximumLength(Project.MaxNameLength).WithMessage("Names can be at most 100 characters.");
        RuleFor(r => r.Description).MaximumLength(Project.MaxDescriptionLength).WithMessage("Descriptions can be at most 1000 characters.");
    }
}

public sealed class UpdateProjectRequestValidator : AbstractValidator<UpdateProjectRequest>
{
    public UpdateProjectRequestValidator()
    {
        RuleFor(r => r.Name!)
            .NotEmpty().WithMessage("Enter a name.")
            .MaximumLength(Project.MaxNameLength).WithMessage("Names can be at most 100 characters.")
            .When(r => r.Name is not null);
        RuleFor(r => r.Description).MaximumLength(Project.MaxDescriptionLength).WithMessage("Descriptions can be at most 1000 characters.");
    }
}
