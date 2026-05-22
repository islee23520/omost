using System.Text;
using System.Text.RegularExpressions;

namespace Omodot.RulesEngine;

public static class RuleParser
{
    public static RuleFrontmatterResult ParseRuleFrontmatter(string content)
    {
        var normalized = StripBom(content);
        var openingLength = OpeningDelimiterLength(normalized);
        if (openingLength == 0) return new RuleFrontmatterResult(new RuleMetadata(), normalized);

        var closing = FindClosingDelimiter(normalized, openingLength);
        if (closing is null) return new RuleFrontmatterResult(new RuleMetadata(), normalized);

        var yaml = normalized[openingLength..closing.Value.Start];
        var body = normalized[closing.Value.BodyStart..];
        return new RuleFrontmatterResult(ParseYaml(yaml), body);
    }

    private static RuleMetadata ParseYaml(string yaml)
    {
        var lines = yaml.Replace("\r\n", "\n").Split('\n');
        string? description = null;
        bool alwaysApply = false;
        var globs = new List<string>();
        int index = 0;

        while (index < lines.Length)
        {
            var line = StripComment(lines[index] ?? "").Trim();
            if (string.IsNullOrEmpty(line)) { index++; continue; }

            var colon = line.IndexOf(':');
            if (colon == -1) { index++; continue; }

            var key = line[..colon].Trim();
            var rawValue = line[(colon + 1)..].Trim();

            if (key == "description") description = ParseString(rawValue);
            else if (key == "alwaysApply") alwaysApply = rawValue == "true";
            else if (key is "globs" or "paths" or "applyTo")
            {
                var parsed = ParseGlobValue(rawValue, lines, index);
                globs.AddRange(parsed.Values);
                index += parsed.Consumed;
                continue;
            }
            index++;
        }

        return new RuleMetadata(
            Description: description,
            Globs: globs.Count > 0 ? globs.ToArray() : null,
            AlwaysApply: alwaysApply);
    }

    private static (string[] Values, int Consumed) ParseGlobValue(string rawValue, string[] lines, int currentIndex)
    {
        if (rawValue.StartsWith('[')) return (ParseInlineArray(rawValue), 1);
        if (string.IsNullOrEmpty(rawValue))
        {
            var parsed = ParseMultilineArray(lines, currentIndex);
            return parsed.Values.Length > 0 ? (parsed.Values, parsed.Consumed) : ([], 1);
        }
        var value = ParseString(rawValue);
        if (value.Contains(','))
            return (value.Split(',').Select(s => s.Trim()).Where(s => !string.IsNullOrEmpty(s)).ToArray(), 1);
        return ([value], 1);
    }

    private static (string[] Values, int Consumed) ParseMultilineArray(string[] lines, int currentIndex)
    {
        var values = new List<string>();
        int consumed = 1;
        for (int i = currentIndex + 1; i < lines.Length; i++)
        {
            var line = StripComment(lines[i] ?? "");
            if (line.Trim().Length == 0) { consumed++; continue; }
            var match = Regex.Match(line, @"^\s+-\s*(.*)$");
            if (!match.Success) break;
            var value = ParseString(match.Groups[1].Value);
            if (!string.IsNullOrEmpty(value)) values.Add(value);
            consumed++;
        }
        return (values.ToArray(), consumed);
    }

    private static string[] ParseInlineArray(string value)
    {
        var closing = value.LastIndexOf(']');
        if (closing == -1) return [];
        return SplitCommaSeparated(value[1..closing])
            .Select(ParseString)
            .Where(s => !string.IsNullOrEmpty(s))
            .ToArray();
    }

    private static string[] SplitCommaSeparated(string value)
    {
        var values = new List<string>();
        var current = new StringBuilder();
        char? quote = null;
        bool escaped = false;

        foreach (var c in value)
        {
            if (escaped) { current.Append(c); escaped = false; continue; }
            if (quote.HasValue && c == '\\') { escaped = true; continue; }
            if (c == '"' || c == '\'') { quote = quote == null ? c : (quote == c ? null : quote); current.Append(c); continue; }
            if (quote is null && c == ',') { values.Add(current.ToString().Trim()); current.Clear(); continue; }
            current.Append(c);
        }
        values.Add(current.ToString().Trim());
        return values.ToArray();
    }

    private static string ParseString(string value)
    {
        var trimmed = value.Trim();
        if ((trimmed.StartsWith('"') && trimmed.EndsWith('"')) ||
            (trimmed.StartsWith('\'') && trimmed.EndsWith('\'')))
            return trimmed[1..^1];
        return trimmed;
    }

    private static string StripComment(string line)
    {
        char? quote = null;
        for (int i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"' || c == '\'') quote = quote == null ? c : (quote == c ? null : quote);
            if (quote is null && c == '#') return line[..i];
        }
        return line;
    }

    private static string StripBom(string content) => content.StartsWith('\uFEFF') ? content[1..] : content;

    private static int OpeningDelimiterLength(string content)
    {
        if (content.StartsWith("---\r\n")) return 5;
        if (content.StartsWith("---\n")) return 4;
        return 0;
    }

    private static (int Start, int BodyStart)? FindClosingDelimiter(string content, int openingLength)
    {
        int lineStart = openingLength;
        while (lineStart <= content.Length)
        {
            var nextNewline = content.IndexOf('\n', lineStart);
            var lineEnd = nextNewline == -1 ? content.Length : nextNewline;
            var line = content[lineStart..lineEnd].TrimEnd('\r');
            if (line == "---")
                return (lineStart, nextNewline == -1 ? content.Length : nextNewline + 1);
            if (nextNewline == -1) break;
            lineStart = nextNewline + 1;
        }
        return null;
    }
}
