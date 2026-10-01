using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using FlagForge.Evaluation;

namespace FlagForge.Application.Common;

/// <summary>
/// The one JSON contract used everywhere: HTTP APIs, SignalR, Redis messages, and JSON columns. Property names and
/// enum values are camelCase, except evaluation reason kinds, which use the SDK wire format (<c>RULE_MATCH</c>).
/// Nullable annotations and required members are enforced when reading. A <see cref="Serve"/> is written with only
/// the alternative it uses, the documented shape, so targeting diffs show no null <c>rollout</c> or <c>variationId</c>.
/// </summary>
public static class JsonDefaults
{
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    public static void Configure(JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.PropertyNameCaseInsensitive = true;
        options.NumberHandling = JsonNumberHandling.Strict;
        options.RespectNullableAnnotations = true;
        options.RespectRequiredConstructorParameters = true;

        // The first converter that handles a type wins, so the reason-kind converter must precede the general one.
        options.Converters.Add(new JsonStringEnumConverter<EvaluationReasonKind>(JsonNamingPolicy.SnakeCaseUpper, allowIntegerValues: false));
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));

        options.TypeInfoResolver = (options.TypeInfoResolver ?? new DefaultJsonTypeInfoResolver()).WithAddedModifier(OmitUnusedServeAlternative);
    }

    private static void OmitUnusedServeAlternative(JsonTypeInfo typeInfo)
    {
        if (typeInfo.Type != typeof(Serve))
        {
            return;
        }

        foreach (var property in typeInfo.Properties)
        {
            property.ShouldSerialize = static (_, value) => value is not null;
        }
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        Configure(options);
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
}
