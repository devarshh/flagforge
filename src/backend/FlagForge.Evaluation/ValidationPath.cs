namespace FlagForge.Evaluation;

/// <summary>Builds dotted, indexed field paths such as <c>rules[1].clauses[0]</c>.</summary>
public static class ValidationPath
{
    public static string Combine(string prefix, string path)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        ArgumentNullException.ThrowIfNull(path);
        if (prefix.Length == 0)
        {
            return path;
        }

        if (path.Length == 0)
        {
            return prefix;
        }

        return path[0] == '[' ? prefix + path : prefix + "." + path;
    }

    public static string Index(string prefix, int index) => $"{prefix}[{index}]";
}
