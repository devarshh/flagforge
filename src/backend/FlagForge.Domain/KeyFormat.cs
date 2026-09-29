using System.Text.RegularExpressions;

namespace FlagForge.Domain;

/// <summary>The format shared by project, environment, and flag keys. Keys are immutable after creation.</summary>
public static partial class KeyFormat
{
    public const int MaxLength = 64;

    public const string Requirement =
        "Keys start with a lowercase letter or digit and use only lowercase letters, digits, '.', '_', and '-' (at most 64 characters).";

    public static bool IsValid(string? key) => key is not null && Pattern().IsMatch(key);

    [GeneratedRegex(@"^[a-z0-9][a-z0-9._-]{0,63}\z", RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();
}
