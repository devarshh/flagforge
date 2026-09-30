using FlagForge.Domain;
using FluentValidation;

namespace FlagForge.Application.Auth;

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(r => r.Email).NotEmpty().WithMessage("Enter your email address.").MaximumLength(User.MaxEmailLength);
        RuleFor(r => r.Password).NotEmpty().WithMessage("Enter your password.").MaximumLength(256);
    }
}

public sealed class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(r => r.CurrentPassword).NotEmpty().WithMessage("Enter your current password.");
        RuleFor(r => r.NewPassword)
            .NotEmpty().WithMessage("Enter a new password.")
            .MinimumLength(User.MinPasswordLength).WithMessage("Use at least 10 characters.")
            .MaximumLength(256).WithMessage("Use at most 256 characters.")
            .NotEqual(r => r.CurrentPassword).WithMessage("Choose a password that is different from your current one.");
    }
}
