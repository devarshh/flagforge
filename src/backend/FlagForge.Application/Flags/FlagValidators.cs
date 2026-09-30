using FlagForge.Application.Common;
using FlagForge.Domain;
using FluentValidation;

namespace FlagForge.Application.Flags;

public sealed class FlagListQueryValidator : AbstractValidator<FlagListQuery>
{
    public FlagListQueryValidator()
    {
        RuleFor(q => q.Page).GreaterThanOrEqualTo(1).WithMessage("Page starts at 1.");
        RuleFor(q => q.PageSize).InclusiveBetween(1, Paging.MaxPageSize).WithMessage("Page size must be between 1 and 100.");
        RuleFor(q => q.Search).MaximumLength(100);
        RuleFor(q => q.Tag).MaximumLength(Flag.MaxTagLength);
    }
}

public sealed class CreateFlagRequestValidator : AbstractValidator<CreateFlagRequest>
{
    public CreateFlagRequestValidator()
    {
        RuleFor(r => r.Key).MustBeKey();
        RuleFor(r => r.Name)
            .NotEmpty().WithMessage("Enter a name.")
            .MaximumLength(Flag.MaxNameLength).WithMessage("Names can be at most 100 characters.");
        RuleFor(r => r.Description).MaximumLength(Flag.MaxDescriptionLength).WithMessage("Descriptions can be at most 1000 characters.");
        RuleFor(r => r.Type).IsInEnum().WithMessage("Choose boolean, string, number, or JSON.");
        RuleFor(r => r.Tags).MustBeValidTags();
        RuleFor(r => r.Variations).Custom((variations, context) =>
        {
            foreach (var (path, message) in VariationRules.Check(context.InstanceToValidate.Type, variations))
            {
                context.AddFailure(path, message);
            }
        });
    }
}

public sealed class UpdateFlagRequestValidator : AbstractValidator<UpdateFlagRequest>
{
    public UpdateFlagRequestValidator()
    {
        RuleFor(r => r.Name!)
            .NotEmpty().WithMessage("Enter a name.")
            .MaximumLength(Flag.MaxNameLength).WithMessage("Names can be at most 100 characters.")
            .When(r => r.Name is not null);
        RuleFor(r => r.Description).MaximumLength(Flag.MaxDescriptionLength).WithMessage("Descriptions can be at most 1000 characters.");
        RuleFor(r => r.Tags).MustBeValidTags();
    }
}

internal static class TagRules
{
    public static IRuleBuilderOptionsConditions<T, IReadOnlyList<string>?> MustBeValidTags<T>(this IRuleBuilder<T, IReadOnlyList<string>?> rule) =>
        rule.Custom((tags, context) =>
        {
            if (tags is null)
            {
                return;
            }

            if (tags.Count > Flag.MaxTags)
            {
                context.AddFailure("tags", "Use at most 10 tags.");
            }

            for (var i = 0; i < tags.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(tags[i]) || tags[i].Trim().Length > Flag.MaxTagLength)
                {
                    context.AddFailure($"tags[{i}]", "Tags are 1 to 32 characters.");
                }
            }
        });

    public static IReadOnlyList<string> Normalize(IReadOnlyList<string>? tags) =>
        tags is null ? [] : [.. tags.Select(t => t.Trim()).Distinct(StringComparer.Ordinal)];
}
