using System.Text;
using System.Text.Json;
using FlagForge.Domain;

namespace FlagForge.Application.Flags;

/// <summary>Rules for a flag's variation list, shared by flag creation and the variations editor.</summary>
public static class VariationRules
{
    public static IEnumerable<(string Path, string Message)> Check(FlagType type, IReadOnlyList<VariationInput>? variations)
    {
        if (type == FlagType.Boolean)
        {
            if (variations is { Count: > 0 })
            {
                yield return ("variations", "Boolean flags always serve true or false. Leave variations out.");
            }

            yield break;
        }

        if (variations is null || variations.Count is < Variation.MinCount or > Variation.MaxCount)
        {
            yield return ("variations", "Add between 2 and 20 variations.");
            yield break;
        }

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var values = new List<JsonElement>();
        for (var i = 0; i < variations.Count; i++)
        {
            var path = $"variations[{i}]";
            var variation = variations[i];
            if (variation is null)
            {
                yield return (path, "Variation is missing.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(variation.Name) || variation.Name.Length > Variation.MaxNameLength)
            {
                yield return ($"{path}.name", "Name each variation (at most 100 characters).");
            }
            else if (!names.Add(variation.Name.Trim()))
            {
                yield return ($"{path}.name", "Each variation needs a different name.");
            }

            if (variation.Description is { Length: > Variation.MaxDescriptionLength })
            {
                yield return ($"{path}.description", "Descriptions can be at most 500 characters.");
            }

            if (!Variation.IsValueValidFor(type, variation.Value))
            {
                yield return ($"{path}.value", TypeMismatch(type));
            }
            else if (type == FlagType.Json && Encoding.UTF8.GetByteCount(variation.Value.GetRawText()) > Variation.MaxJsonValueBytes)
            {
                yield return ($"{path}.value", "JSON values can be at most 32 KB.");
            }
            else if (values.Exists(v => JsonElement.DeepEquals(v, variation.Value)))
            {
                yield return ($"{path}.value", "Each variation needs a different value.");
            }
            else
            {
                values.Add(variation.Value);
            }
        }
    }

    private static string TypeMismatch(FlagType type) => type switch
    {
        FlagType.String => "Values of a string flag must be text.",
        FlagType.Number => "Values of a number flag must be numbers.",
        FlagType.Json => "Values of a JSON flag must be a JSON object or array.",
        _ => "This value does not match the flag type.",
    };
}
