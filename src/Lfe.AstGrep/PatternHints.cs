using System.Text.RegularExpressions;

namespace Lfe.AstGrep;

public static partial class PatternHints
{
    public static string? DetectRegexMisuse(string pattern)
    {
        var src = pattern.Trim();

        if (RegexEscapesRegex().IsMatch(src))
        {
            return "Hint: \"\\w\", \"\\d\", \"\\s\", \"\\b\" are regex escapes. ast-grep matches AST nodes, not text - use $VAR for identifiers, $$$ for node lists, or switch to grep for text search.";
        }

        if (CharacterClassRegex().IsMatch(src))
        {
            return "Hint: \"[a-z]\" and similar character classes are regex, not AST. Use $VAR to match any identifier, or switch to grep for text search.";
        }

        if (!src.Contains('$') && RegexWildcardsRegex().IsMatch(src))
        {
            return "Hint: \".*\" and \".+\" are regex wildcards. In ast-grep use $$$ for multiple AST nodes and $VAR for a single node. For text patterns, switch to grep.";
        }

        if (RegexAlternationRegex().IsMatch(src))
        {
            return "Hint: \"|\" is regex alternation and does NOT work in ast-grep patterns. Options: (a) fire one ast_grep_search per alternative, or (b) switch to grep with a regex pattern like \"foo|bar\".";
        }

        return null;
    }

    public static string? DetectLanguageSpecificMistake(string pattern, CliLanguage language)
    {
        var src = pattern.Trim();

        if (language == CliLanguage.Python)
        {
            if (src.StartsWith("class ", StringComparison.Ordinal) && src.EndsWith(':'))
            {
                return $"Hint: Remove trailing colon. Try: \"{src[..^1]}\"";
            }

            if ((src.StartsWith("def ", StringComparison.Ordinal) || src.StartsWith("async def ", StringComparison.Ordinal)) && src.EndsWith(':'))
            {
                return $"Hint: Remove trailing colon. Try: \"{src[..^1]}\"";
            }
        }

        if (language is CliLanguage.Javascript or CliLanguage.Typescript or CliLanguage.Tsx)
        {
            if (JavascriptFunctionRegex().IsMatch(src))
            {
                return "Hint: Function patterns need params and body. Try \"function $NAME($$$) { $$$ }\"";
            }
        }

        if (language == CliLanguage.Go && GoFunctionRegex().IsMatch(src))
        {
            return "Hint: Go function patterns need params and body. Try \"func $NAME($$$) { $$$ }\"";
        }

        if (language == CliLanguage.Rust && RustFunctionRegex().IsMatch(src))
        {
            return "Hint: Rust fn patterns need params and body. Try \"fn $NAME($$$) { $$$ }\"";
        }

        return null;
    }

    public static string? GetPatternHint(string pattern, CliLanguage language)
    {
        return DetectRegexMisuse(pattern) ?? DetectLanguageSpecificMistake(pattern, language);
    }

    [GeneratedRegex("\\\\[wWdDsSbB]")]
    private static partial Regex RegexEscapesRegex();

    [GeneratedRegex("\\[[a-zA-Z0-9]-[a-zA-Z0-9]\\]")]
    private static partial Regex CharacterClassRegex();

    [GeneratedRegex("\\w\\.[*+]")]
    private static partial Regex RegexWildcardsRegex();

    [GeneratedRegex("^[-\\w.*]+\\|[-\\w.*|]+$")]
    private static partial Regex RegexAlternationRegex();

    [GeneratedRegex("^(export\\s+)?(async\\s+)?function\\s+\\$[A-Z_]+\\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex JavascriptFunctionRegex();

    [GeneratedRegex("^func\\s+\\$[A-Z_]+\\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex GoFunctionRegex();

    [GeneratedRegex("^fn\\s+\\$[A-Z_]+\\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex RustFunctionRegex();
}
