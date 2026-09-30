using FlagForge.Application.Common;
using FlagForge.Domain;
using FluentValidation;

namespace FlagForge.Application.Audit;

public sealed class AuditQueryValidator : AbstractValidator<AuditQuery>
{
    public AuditQueryValidator()
    {
        RuleFor(q => q.Page).GreaterThanOrEqualTo(1).WithMessage("Page starts at 1.");
        RuleFor(q => q.PageSize).InclusiveBetween(1, Paging.MaxPageSize).WithMessage("Page size must be between 1 and 100.");
        RuleFor(q => q.FlagKey).Null().When(q => q.ProjectKey is null).WithMessage("Filter by project as well when filtering by flag.");
        RuleFor(q => q.EnvironmentKey).Null().When(q => q.ProjectKey is null).WithMessage("Filter by project as well when filtering by environment.");
        RuleFor(q => q.Action).Must(a => a is null || AuditActions.All.Contains(a)).WithMessage("Unknown action. Use a value such as flag.toggled.");
        RuleFor(q => q.To).GreaterThan(q => q.From).When(q => q.From is not null && q.To is not null).WithMessage("Choose an end time after the start time.");
    }
}
