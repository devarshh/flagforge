using FlagForge.Domain;
using FluentValidation;

namespace FlagForge.Application.Targeting;

public sealed class UpdateTargetingRequestValidator : AbstractValidator<UpdateTargetingRequest>
{
    public UpdateTargetingRequestValidator()
    {
        RuleFor(r => r.ExpectedVersion).GreaterThanOrEqualTo(1).WithMessage("Send the version you loaded as expectedVersion.");
        RuleFor(r => r.Comment).MaximumLength(AuditEntry.MaxCommentLength).WithMessage("Comments can be at most 1000 characters.");
    }
}

public sealed class ToggleRequestValidator : AbstractValidator<ToggleRequest>
{
    public ToggleRequestValidator()
    {
        RuleFor(r => r.ExpectedVersion!.Value).GreaterThanOrEqualTo(1).When(r => r.ExpectedVersion is not null);
        RuleFor(r => r.Comment).MaximumLength(AuditEntry.MaxCommentLength).WithMessage("Comments can be at most 1000 characters.");
    }
}
