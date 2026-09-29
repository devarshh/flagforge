using System.Text.Json;
using FlagForge.Application.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FlagForge.Infrastructure.Persistence;

/// <summary>
/// Maps a property to an <c>nvarchar(max)</c> JSON document serialized with <see cref="JsonDefaults"/>, so stored
/// JSON has exactly the shape of the API JSON. Comparison uses the serialized form, which makes change tracking
/// correct for immutable records and lists.
/// </summary>
internal static class JsonColumnExtensions
{
    public static PropertyBuilder<T> HasJsonConversion<T>(this PropertyBuilder<T> property)
        where T : class?
    {
        property
            .HasConversion(
                value => Serialize(value),
                json => Deserialize<T>(json),
                new ValueComparer<T>(
                    (left, right) => Serialize(left) == Serialize(right),
                    value => StringComparer.Ordinal.GetHashCode(Serialize(value)),
                    value => Deserialize<T>(Serialize(value))))
            .HasColumnType("nvarchar(max)");
        return property;
    }

    public static PropertyBuilder<JsonElement?> HasJsonConversion(this PropertyBuilder<JsonElement?> property)
    {
        property
            .HasConversion(
                value => value!.Value.GetRawText(),
                json => ParseElement(json),
                new ValueComparer<JsonElement?>(
                    (left, right) => RawText(left) == RawText(right),
                    value => StringComparer.Ordinal.GetHashCode(RawText(value)),
                    value => value))
            .HasColumnType("nvarchar(max)");
        return property;
    }

    private static string Serialize<T>(T value) => JsonSerializer.Serialize(value, JsonDefaults.Options);

    private static T Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, JsonDefaults.Options)
        ?? throw new JsonException($"Stored JSON for {typeof(T).Name} was null.");

    private static JsonElement? ParseElement(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static string RawText(JsonElement? value) => value?.GetRawText() ?? string.Empty;
}
