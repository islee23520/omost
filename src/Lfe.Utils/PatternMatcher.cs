using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Lfe.Utils;

public sealed record HookCommand(string Type, string? Command = null, IReadOnlyDictionary<string, JsonNode?>? Properties = null);

public sealed record HookMatcher(string? Matcher, IReadOnlyList<HookCommand> Hooks);

public enum ClaudeHookEventName
{
    PreToolUse,
    PostToolUse,
    Notification,
    Stop,
    SubagentStop,
    UserPromptSubmit,
    SessionStart,
    SessionEnd,
    PreCompact,
}

public static class PatternMatcher
{
    private static readonly Dictionary<string, Regex> RegexCache = new(StringComparer.Ordinal);

    public static bool MatchesToolMatcher(string toolName, string? matcher)
    {
        if (string.IsNullOrEmpty(matcher))
        {
            return true;
        }

        var patterns = matcher.Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return patterns.Any(pattern => MatchesPattern(toolName, pattern));
    }

    public static IReadOnlyList<HookMatcher> FindMatchingHooks(IReadOnlyDictionary<ClaudeHookEventName, IReadOnlyList<HookMatcher>> config, ClaudeHookEventName eventName, string? toolName = null)
    {
        if (!config.TryGetValue(eventName, out var hookMatchers))
        {
            return [];
        }

        if (toolName is null)
        {
            return hookMatchers;
        }

        return hookMatchers.Where(hookMatcher => MatchesToolMatcher(toolName, hookMatcher.Matcher)).ToArray();
    }

    private static bool MatchesPattern(string toolName, string pattern)
    {
        if (pattern.Contains('*'))
        {
            if (!RegexCache.TryGetValue(pattern, out var regex))
            {
                regex = new Regex($"^{Regex.Escape(pattern).Replace("\\*", ".*")}$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                RegexCache[pattern] = regex;
            }

            return regex.IsMatch(toolName);
        }

        return string.Equals(pattern, toolName, StringComparison.OrdinalIgnoreCase);
    }
}
