using System.Text.Json;

namespace FlagForge.Domain;

/// <summary>A flag variation. Ids are stable when names or values change.</summary>
public sealed record Variation
{
    public const int MaxNameLength = 100;
    public const int MaxDescriptionLength = 500;
    public const int MaxJsonValueBytes = 32 * 1024;
    public const int MinCount = 2;
    public const int MaxCount = 20;

    public const string TrueId = "true";
    public const string FalseId = "false";

    public required string Id { get; init; }

    public required string Name { get; init; }

    public required JsonElement Value { get; init; }

    public string? Description { get; init; }

    /// <summary>The fixed variations of a boolean flag: <c>true</c> then <c>false</c>.</summary>
    public static IReadOnlyList<Variation> ForBoolean() =>
    [
        new() { Id = TrueId, Name = "True", Value = JsonSerializer.SerializeToElement(true) },
        new() { Id = FalseId, Name = "False", Value = JsonSerializer.SerializeToElement(false) },
    ];

    /// <summary>Whether a JSON value is valid for a flag type (JSON flags hold an object or an array).</summary>
    public static bool IsValueValidFor(FlagType type, JsonElement value) => type switch
    {
        FlagType.Boolean => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
        FlagType.String => value.ValueKind == JsonValueKind.String,
        FlagType.Number => value.ValueKind == JsonValueKind.Number,
        FlagType.Json => value.ValueKind is JsonValueKind.Object or JsonValueKind.Array,
        _ => false,
    };
}
