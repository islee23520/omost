using System.Text.RegularExpressions;

namespace Omodot.ModelCore;

public static class ModelCapabilityAliases
{
    private static readonly ExactAliasRule[] ExactAliasRules =
    [
        new("gemini-3-pro-high", "gemini-3-pro-tier-alias", "gemini-3-pro-preview", "Legacy Gemini 3 tier suffixes"),
        new("gemini-3-pro-low", "gemini-3-pro-tier-alias", "gemini-3-pro-preview", "Legacy Gemini 3 tier suffixes"),
        new("k2pb", "kimi-k2pb-alias", "k2p5", "Kimi for Coding k2pb alias"),
        new("claude-opus-4.7", "claude-opus-dotted-version-alias", "claude-opus-4-7", "Dotted version syntax alias"),
    ];

    private static readonly PatternAliasRule[] PatternAliasRules =
    [
        new("claude-thinking-legacy-alias", "Normalizes claude-opus-4-7-thinking",
            id => Regex.IsMatch(id, "^claude-opus-4-7-thinking$"),
            _ => "claude-opus-4-7"),
        new("gemini-3.1-pro-tier-alias", "Normalizes Gemini 3.1 Pro tier suffixes",
            id => Regex.IsMatch(id, "^gemini-3\\.1-pro-(?:high|low)$"),
            _ => "gemini-3.1-pro"),
    ];

    private static readonly Dictionary<string, ExactAliasRule> ExactByModel =
        ExactAliasRules.ToDictionary(r => r.AliasModelID);

    public static ModelIDAliasResolution ResolveModelIDAlias(string modelID)
    {
        var requested = modelID.Trim().ToLowerInvariant();
        var lookup = StripProviderPrefix(requested);

        if (ExactByModel.TryGetValue(lookup, out var exact))
            return new(requested, exact.CanonicalModelID, "exact-alias", exact.RuleID);

        foreach (var rule in PatternAliasRules)
        {
            if (rule.Match(lookup))
                return new(requested, rule.Canonicalize(lookup), "pattern-alias", rule.RuleID);
        }

        return new(requested, lookup, "canonical");
    }

    public static IReadOnlyList<ExactAliasRule> GetExactAliasRules() => ExactAliasRules;
    public static IReadOnlyList<PatternAliasRule> GetPatternAliasRules() => PatternAliasRules;

    private static string StripProviderPrefix(string modelID)
    {
        var slash = modelID.IndexOf('/');
        if (slash <= 0 || slash == modelID.Length - 1) return modelID;
        return modelID[(slash + 1)..];
    }
}
