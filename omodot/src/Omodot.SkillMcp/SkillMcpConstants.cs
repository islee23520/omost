namespace Omodot.SkillMcp;

public static class SkillMcpConstants
{
    public const string SkillMcpToolName = "skill_mcp";

    public const string SkillMcpDescription = "Invoke MCP server operations from skill-embedded MCPs. Requires mcp_name plus exactly one of: tool_name, resource_name, or prompt_name.";

    public static readonly IReadOnlyDictionary<string, string[]> BuiltinMcpToolHints = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        ["context7"] = ["context7_resolve-library-id", "context7_query-docs"],
        ["websearch"] = ["websearch_web_search_exa"],
        ["grep_app"] = ["grep_app_searchGitHub"],
    };
}
