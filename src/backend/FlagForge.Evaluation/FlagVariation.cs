using System.Text.Json;

namespace FlagForge.Evaluation;

/// <summary>A flag variation as the engine sees it: a stable id and an opaque JSON value.</summary>
public sealed record FlagVariation(string Id, JsonElement Value);
