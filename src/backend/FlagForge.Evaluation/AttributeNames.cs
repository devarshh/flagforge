using System.Text.RegularExpressions;

namespace FlagForge.Evaluation;

/// <summary>Rules for context attribute names.</summary>
public static partial class AttributeNames
{
    /// <summary>The reserved attribute name that addresses the context key.</summary>
    public const string Key = "key";

    public static bool IsValid(string? name) => name is not null && Pattern().IsMatch(name);

    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_.-]{0,63}\z", RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();
}
