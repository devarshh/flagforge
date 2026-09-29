namespace FlagForge.Domain;

/// <summary>The environments every new project starts with, and the palette the dashboard offers.</summary>
public static class EnvironmentDefaults
{
    public static IReadOnlyList<(string Key, string Name, string Color, bool IsProtected)> Initial { get; } =
    [
        ("development", "Development", "#3A7CA5", false),
        ("staging", "Staging", "#8E6CC0", false),
        ("production", "Production", "#C2362B", true),
    ];

    public static IReadOnlyList<string> Palette { get; } =
    [
        "#3A7CA5", "#8E6CC0", "#C2362B", "#2F8F83", "#B86E00", "#5B6B7F", "#A0527A", "#4F7A28",
    ];
}
