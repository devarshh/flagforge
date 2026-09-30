using System.Text;
using FlagForge.Evaluation;
using FluentValidation;

namespace FlagForge.Application.Common;

public static class ValidationExtensions
{
    /// <summary>Runs a FluentValidation validator and throws <see cref="RequestValidationException"/> on failure.</summary>
    public static async Task EnsureValidAsync<T>(this IValidator<T> validator, T request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(validator);
        var result = await validator.ValidateAsync(request, cancellationToken);
        if (!result.IsValid)
        {
            throw new RequestValidationException(result.Errors
                .GroupBy(e => ToCamelCasePath(e.PropertyName))
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).Distinct().ToArray()));
        }
    }

    /// <summary>Throws when an evaluation-engine validator reported errors, prefixing each path.</summary>
    public static void ThrowIfAny(this IReadOnlyList<ValidationError> errors, string pathPrefix)
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (errors.Count > 0)
        {
            throw new RequestValidationException(errors
                .GroupBy(e => ValidationPath.Combine(pathPrefix, e.Path))
                .ToDictionary(g => g.Key, g => g.Select(e => e.Message).Distinct().ToArray()));
        }
    }

    /// <summary>Converts FluentValidation paths such as <c>Variations[0].Name</c> to <c>variations[0].name</c>.</summary>
    public static string ToCamelCasePath(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var builder = new StringBuilder(path.Length);
        var atSegmentStart = true;
        foreach (var character in path)
        {
            builder.Append(atSegmentStart ? char.ToLowerInvariant(character) : character);
            atSegmentStart = character is '.';
        }

        return builder.ToString();
    }
}
