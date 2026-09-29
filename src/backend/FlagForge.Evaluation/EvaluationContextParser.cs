using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace FlagForge.Evaluation;

/// <summary>Parses and validates an evaluation context from JSON, enforcing the limits in <see cref="ContextLimits"/>.</summary>
public static class EvaluationContextParser
{
    private const string AttributesProperty = "attributes";

    public static bool TryParse(
        JsonElement json,
        [NotNullWhen(true)] out EvaluationContext? context,
        out IReadOnlyList<ValidationError> errors)
    {
        var problems = new List<ValidationError>();
        context = null;
        errors = problems;

        if (json.ValueKind != JsonValueKind.Object)
        {
            problems.Add(new ValidationError(string.Empty, "Send the context as a JSON object with a key."));
            return false;
        }

        var key = ReadKey(json, problems);
        var attributes = ReadAttributes(json, problems);
        if (problems.Count > 0 || key is null)
        {
            return false;
        }

        context = EvaluationContext.FromValidated(key, attributes);
        return true;
    }

    private static string? ReadKey(JsonElement json, List<ValidationError> problems)
    {
        if (!json.TryGetProperty(AttributeNames.Key, out var keyElement) || keyElement.ValueKind == JsonValueKind.Null)
        {
            problems.Add(new ValidationError(AttributeNames.Key, "Provide a context key between 1 and 256 characters."));
            return null;
        }

        if (keyElement.ValueKind != JsonValueKind.String)
        {
            problems.Add(new ValidationError(AttributeNames.Key, "The context key must be a string."));
            return null;
        }

        var key = keyElement.GetString()!;
        if (key.Length is 0 or > ContextLimits.MaxKeyLength)
        {
            problems.Add(new ValidationError(AttributeNames.Key, "Provide a context key between 1 and 256 characters."));
            return null;
        }

        return key;
    }

    private static Dictionary<string, AttributeValue> ReadAttributes(JsonElement json, List<ValidationError> problems)
    {
        var attributes = new Dictionary<string, AttributeValue>(StringComparer.Ordinal);
        if (!json.TryGetProperty(AttributesProperty, out var element) || element.ValueKind == JsonValueKind.Null)
        {
            return attributes;
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            problems.Add(new ValidationError(AttributesProperty, "Attributes must be a JSON object."));
            return attributes;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var count = 0;
        foreach (var property in element.EnumerateObject())
        {
            count++;
            var path = ValidationPath.Combine(AttributesProperty, property.Name);
            if (!seen.Add(property.Name))
            {
                problems.Add(new ValidationError(path, $"Attribute '{property.Name}' appears more than once."));
                continue;
            }

            if (property.Name == AttributeNames.Key)
            {
                problems.Add(new ValidationError(path, "'key' is reserved. Set the context key with the top-level key property."));
                continue;
            }

            if (!AttributeNames.IsValid(property.Name))
            {
                problems.Add(new ValidationError(
                    path,
                    "Attribute names start with a letter or underscore and contain only letters, digits, '_', '.', or '-' (at most 64 characters)."));
                continue;
            }

            if (property.Value.ValueKind == JsonValueKind.Null)
            {
                // A null attribute behaves exactly like a missing one, so it is simply not stored.
                continue;
            }

            var value = property.Value.ValueKind == JsonValueKind.Array
                ? ReadArray(property.Value, path, problems)
                : ReadScalar(property.Value, path, problems, "Attribute values must be strings, numbers, booleans, or arrays of those.");
            if (value is not null)
            {
                attributes[property.Name] = value;
            }
        }

        if (count > ContextLimits.MaxAttributes)
        {
            problems.Add(new ValidationError(AttributesProperty, "A context can have at most 50 attributes."));
        }

        return attributes;
    }

    private static AttributeValue? ReadArray(JsonElement array, string path, List<ValidationError> problems)
    {
        if (array.GetArrayLength() > ContextLimits.MaxArrayItems)
        {
            problems.Add(new ValidationError(path, "Array attributes can have at most 100 items."));
            return null;
        }

        var items = new List<AttributeValue>(array.GetArrayLength());
        var index = 0;
        foreach (var item in array.EnumerateArray())
        {
            var value = ReadScalar(item, ValidationPath.Index(path, index), problems, "Array items must be strings, numbers, or booleans.");
            if (value is not null)
            {
                items.Add(value);
            }

            index++;
        }

        return items.Count == index ? AttributeValue.FromArray(items) : null;
    }

    private static AttributeValue? ReadScalar(JsonElement element, string path, List<ValidationError> problems, string typeMessage)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                var text = element.GetString()!;
                if (text.Length > ContextLimits.MaxStringLength)
                {
                    problems.Add(new ValidationError(path, "Text values can be at most 1024 characters."));
                    return null;
                }

                return AttributeValue.FromString(text);
            case JsonValueKind.Number:
                if (!element.TryGetDecimal(out var number))
                {
                    problems.Add(new ValidationError(path, "Numbers must be within ±79228162514264337593543950335."));
                    return null;
                }

                return AttributeValue.FromNumber(number);
            case JsonValueKind.True:
                return AttributeValue.True;
            case JsonValueKind.False:
                return AttributeValue.False;
            default:
                problems.Add(new ValidationError(path, typeMessage));
                return null;
        }
    }
}
