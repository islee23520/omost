using System.Text.RegularExpressions;

namespace Lfe.Utils;

public static partial class ExtractSemver
{
    public static string? FromOutput(string output)
    {
        var trimmed = output.Trim();
        if (trimmed.Length == 0)
        {
            return null;
        }

        var match = SemverPattern().Match(trimmed);
        return match.Success ? match.Groups[1].Value : null;
    }

    [GeneratedRegex("(?<![\\d:])v?(\\d+\\.\\d+\\.\\d+(?:[-+][\\w.]+)*)", RegexOptions.CultureInvariant)]
    private static partial Regex SemverPattern();
}
