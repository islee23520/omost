namespace Lfe.Utils;

public static class ToolName
{
    private static readonly IReadOnlyDictionary<string, string> SpecialToolMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["webfetch"] = "WebFetch",
        ["websearch"] = "WebSearch",
        ["todoread"] = "TodoRead",
        ["todowrite"] = "TodoWrite",
    };

    public static string Transform(string toolName)
    {
        var trimmed = toolName.Trim();
        if (SpecialToolMappings.TryGetValue(trimmed, out var mapped))
        {
            return mapped;
        }

        if (trimmed.Contains('-') || trimmed.Contains('_') || trimmed.Contains(' '))
        {
            return string.Concat(trimmed.Split(['-', '_', ' '], StringSplitOptions.RemoveEmptyEntries).Select(ToTitleCase));
        }

        return trimmed.Length == 0 ? trimmed : char.ToUpperInvariant(trimmed[0]) + trimmed[1..];
    }

    private static string ToTitleCase(string value) => value.Length == 0 ? value : char.ToUpperInvariant(value[0]) + value[1..].ToLowerInvariant();
}
