using FlagForge.Application.Common;
using FlagForge.Domain;
using FluentValidation;

namespace FlagForge.Application.Users;

public sealed class UserListQueryValidator : AbstractValidator<UserListQuery>
{
    public UserListQueryValidator()
    {
        RuleFor(q => q.Page).GreaterThanOrEqualTo(1).WithMessage("Page starts at 1.");
        RuleFor(q => q.PageSize).InclusiveBetween(1, Paging.MaxPageSize).WithMessage("Page size must be between 1 and 100.");
    }
}

public sealed class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserRequestValidator()
    {
        RuleFor(r => r.Email)
            .NotEmpty().WithMessage("Enter an email address.")
            .MaximumLength(User.MaxEmailLength).WithMessage("Email addresses can be at most 256 characters.")
            .EmailAddress().WithMessage("Enter a valid email address, such as sam@example.com.");
        RuleFor(r => r.DisplayName)
            .NotEmpty().WithMessage("Enter a display name.")
            .MaximumLength(User.MaxDisplayNameLength).WithMessage("Display names can be at most 100 characters.");
        RuleFor(r => r.Role).IsInEnum().WithMessage("Choose Viewer, Editor, or Admin.");
    }
}

public sealed class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
{
    public UpdateUserRequestValidator()
    {
        RuleFor(r => r.DisplayName!)
            .NotEmpty().WithMessage("Enter a display name.")
            .MaximumLength(User.MaxDisplayNameLength).WithMessage("Display names can be at most 100 characters.")
            .When(r => r.DisplayName is not null);
        RuleFor(r => r.Role!.Value).IsInEnum().WithMessage("Choose Viewer, Editor, or Admin.").When(r => r.Role is not null);
    }
}
