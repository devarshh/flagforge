using System.Globalization;

namespace FlagForge.Evaluation;

/// <summary>The single definition of how clause values parse as numbers: invariant culture, no thousands separators.</summary>
internal static class NumberParsing
{
    public static bool TryParse(string? text, out decimal value) =>
        decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
}
