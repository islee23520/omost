using System.Text.Json.Nodes;

namespace Omodot.Utils.Tests;

public sealed class PatternMatcherTests
{
    [Fact]
    public void MatchesToolMatcher_supports_literal_and_wildcards()
    {
        Assert.True(PatternMatcher.MatchesToolMatcher("bash", "bash"));
        Assert.True(PatternMatcher.MatchesToolMatcher("lsp_goto_definition", "lsp_*"));
    }

    [Fact]
    public void FindMatchingHooks_filters_hooks_by_event_and_tool()
    {
        IReadOnlyDictionary<ClaudeHookEventName, IReadOnlyList<HookMatcher>> config = new Dictionary<ClaudeHookEventName, IReadOnlyList<HookMatcher>>
        {
            [ClaudeHookEventName.PreToolUse] =
            [
                new HookMatcher("bash", [new HookCommand("command", "/test")]),
                new HookMatcher("*", [new HookCommand("command", "/all", new Dictionary<string, JsonNode?>())]),
            ],
        };

        var result = PatternMatcher.FindMatchingHooks(config, ClaudeHookEventName.PreToolUse, "bash");
        Assert.Equal(2, result.Count);
    }
}
