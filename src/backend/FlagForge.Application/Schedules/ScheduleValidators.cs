using FluentValidation;

namespace FlagForge.Application.Schedules;

public sealed class CreateScheduledChangeRequestValidator : AbstractValidator<CreateScheduledChangeRequest>
{
    public CreateScheduledChangeRequestValidator()
    {
        RuleFor(r => r.Action).IsInEnum().WithMessage("Choose turnOn, turnOff, or setFallthrough.");
        RuleFor(r => r.Payload)
            .NotNull().WithMessage("Choose what the default rule should serve.")
            .When(r => r.Action == Domain.ScheduledChangeAction.SetFallthrough);
        RuleFor(r => r.Payload)
            .Null().WithMessage("Only setFallthrough changes take a payload.")
            .When(r => r.Action != Domain.ScheduledChangeAction.SetFallthrough);
    }
}

public sealed class CreateReleasePlanRequestValidator : AbstractValidator<CreateReleasePlanRequest>
{
    public const int MaxSteps = 10;

    public CreateReleasePlanRequestValidator()
    {
        RuleFor(r => r.Steps)
            .NotNull().WithMessage("Add at least one step.")
            .Must(steps => steps is { Count: >= 1 and <= MaxSteps }).WithMessage("A release plan has between 1 and 10 steps.");
        RuleFor(r => r.Steps).Custom((steps, context) =>
        {
            if (steps is null)
            {
                return;
            }

            for (var i = 1; i < steps.Count; i++)
            {
                if (steps[i] is not null && steps[i - 1] is not null && steps[i].ExecuteAt <= steps[i - 1].ExecuteAt)
                {
                    context.AddFailure($"steps[{i}].executeAt", "Each step must be later than the step before it.");
                }
            }
        });
    }
}
