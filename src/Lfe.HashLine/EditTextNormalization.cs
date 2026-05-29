using System.Text;
using System.Text.RegularExpressions;

namespace Lfe.HashLine;

public static class EditTextNormalization
{
    private static readonly Regex HashlinePrefixRegex = new(
        @"^\s*(?:>>>|>>)?\s*\d+\s*#\s*[ZPMQVRWSNKTXJBYH]{2}\|",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex DiffPlusRegex = new(
        @"^[+](?![+])",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static string[] StripLinePrefixes(IReadOnlyList<string> lines)
    {
        var hashPrefixCount = 0;
        var diffPlusCount = 0;
        var nonEmpty = 0;

        foreach (var line in lines)
        {
            if (line.Length == 0)
            {
                continue;
            }

            nonEmpty += 1;
            if (HashlinePrefixRegex.IsMatch(line))
            {
                hashPrefixCount += 1;
            }

            if (DiffPlusRegex.IsMatch(line))
            {
                diffPlusCount += 1;
            }
        }

        if (nonEmpty == 0)
        {
            return lines.ToArray();
        }

        var stripHash = hashPrefixCount > 0 && hashPrefixCount >= nonEmpty * 0.5;
        var stripPlus = !stripHash && diffPlusCount > 0 && diffPlusCount >= nonEmpty * 0.5;
        if (!stripHash && !stripPlus)
        {
            return lines.ToArray();
        }

        return lines
            .Select(line => stripHash ? HashlinePrefixRegex.Replace(line, string.Empty) : DiffPlusRegex.Replace(line, string.Empty))
            .ToArray();
    }

    public static string[] ToNewLines(string input)
    {
        return StripLinePrefixes(input.Split('\n'));
    }

    public static string[] ToNewLines(IReadOnlyList<string> input)
    {
        return StripLinePrefixes(input);
    }

    public static string RestoreLeadingIndent(string templateLine, string line)
    {
        if (line.Length == 0)
        {
            return line;
        }

        var templateIndent = LeadingWhitespace(templateLine);
        if (templateIndent.Length == 0 || LeadingWhitespace(line).Length > 0 || templateLine.Trim() == line.Trim())
        {
            return line;
        }

        return templateIndent + line;
    }

    public static string[] StripInsertAnchorEcho(string anchorLine, IReadOnlyList<string> newLines)
    {
        if (newLines.Count == 0)
        {
            return newLines.ToArray();
        }

        return EqualsIgnoringWhitespace(newLines[0], anchorLine) ? newLines.Skip(1).ToArray() : newLines.ToArray();
    }

    public static string[] StripInsertBeforeEcho(string anchorLine, IReadOnlyList<string> newLines)
    {
        if (newLines.Count <= 1)
        {
            return newLines.ToArray();
        }

        return EqualsIgnoringWhitespace(newLines[^1], anchorLine) ? newLines.Take(newLines.Count - 1).ToArray() : newLines.ToArray();
    }

    public static string[] StripInsertBoundaryEcho(string afterLine, string beforeLine, IReadOnlyList<string> newLines)
    {
        var output = newLines.ToList();
        if (output.Count > 0 && EqualsIgnoringWhitespace(output[0], afterLine))
        {
            output.RemoveAt(0);
        }

        if (output.Count > 0 && EqualsIgnoringWhitespace(output[^1], beforeLine))
        {
            output.RemoveAt(output.Count - 1);
        }

        return output.ToArray();
    }

    public static string[] StripRangeBoundaryEcho(IReadOnlyList<string> lines, int startLine, int endLine, IReadOnlyList<string> newLines)
    {
        var replacedCount = endLine - startLine + 1;
        if (newLines.Count <= 1 || newLines.Count <= replacedCount)
        {
            return newLines.ToArray();
        }

        var output = newLines.ToList();
        var beforeIndex = startLine - 2;
        if (beforeIndex >= 0 && EqualsIgnoringWhitespace(output[0], lines[beforeIndex]))
        {
            output.RemoveAt(0);
        }

        var afterIndex = endLine;
        if (afterIndex < lines.Count && output.Count > 0 && EqualsIgnoringWhitespace(output[^1], lines[afterIndex]))
        {
            output.RemoveAt(output.Count - 1);
        }

        return output.ToArray();
    }

    private static bool EqualsIgnoringWhitespace(string left, string right)
    {
        return left == right || RemoveAllWhitespace(left) == RemoveAllWhitespace(right);
    }

    private static string LeadingWhitespace(string text)
    {
        var builder = new StringBuilder();
        foreach (var character in text)
        {
            if (!char.IsWhiteSpace(character))
            {
                break;
            }

            builder.Append(character);
        }

        return builder.ToString();
    }

    private static string RemoveAllWhitespace(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var character in text)
        {
            if (!char.IsWhiteSpace(character))
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }
}
