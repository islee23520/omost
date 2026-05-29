using System.Text.RegularExpressions;

namespace Lfe.SkillMcp;

public static class SkillMcpHelpers
{
    public static SkillMcpOperation ValidateSkillMcpOperation(SkillMcpArgs args)
    {
        var operations = new List<SkillMcpOperation>();

        if (!string.IsNullOrEmpty(args.ToolName))
        {
            operations.Add(new SkillMcpOperation("tool", args.ToolName));
        }

        if (!string.IsNullOrEmpty(args.ResourceName))
        {
            operations.Add(new SkillMcpOperation("resource", args.ResourceName));
        }

        if (!string.IsNullOrEmpty(args.PromptName))
        {
            operations.Add(new SkillMcpOperation("prompt", args.PromptName));
        }

        if (operations.Count == 0)
        {
            throw new InvalidOperationException("Missing operation. Exactly one of tool_name, resource_name, or prompt_name must be specified.\n\nExamples:\n  skill_mcp(mcp_name=\"sqlite\", tool_name=\"query\", arguments='{\"sql\": \"SELECT * FROM users\"}')\n  skill_mcp(mcp_name=\"memory\", resource_name=\"memory://notes\")\n  skill_mcp(mcp_name=\"helper\", prompt_name=\"summarize\", arguments='{\"text\": \"...\"}')");
        }

        if (operations.Count > 1)
        {
            var provided = new List<string>();

            if (!string.IsNullOrEmpty(args.ToolName))
            {
                provided.Add($"tool_name=\"{args.ToolName}\"");
            }

            if (!string.IsNullOrEmpty(args.ResourceName))
            {
                provided.Add($"resource_name=\"{args.ResourceName}\"");
            }

            if (!string.IsNullOrEmpty(args.PromptName))
            {
                provided.Add($"prompt_name=\"{args.PromptName}\"");
            }

            throw new InvalidOperationException($"Multiple operations specified. Exactly one of tool_name, resource_name, or prompt_name must be provided.\n\nReceived: {string.Join(", ", provided)}\n\nUse separate calls for each operation.");
        }

        return operations[0];
    }

    public static SkillMcpServerMatch? FindSkillMcpServer(string mcpName, IEnumerable<SkillMcpSkillLike> skills)
    {
        foreach (var skill in skills)
        {
            if (skill.McpConfig is null)
            {
                continue;
            }

            if (skill.McpConfig.Servers.TryGetValue(mcpName, out var config))
            {
                return new SkillMcpServerMatch(skill, config);
            }
        }

        return null;
    }

    public static string FormatAvailableSkillMcps(IEnumerable<SkillMcpSkillLike> skills)
    {
        var mcps = new List<string>();

        foreach (var skill in skills)
        {
            if (skill.McpConfig is null)
            {
                continue;
            }

            foreach (var serverName in skill.McpConfig.Servers.Keys)
            {
                mcps.Add($"  - \"{serverName}\" from skill \"{skill.Name}\"");
            }
        }

        return mcps.Count > 0 ? string.Join("\n", mcps) : "  (none found)";
    }

    public static string? FormatBuiltinMcpHint(string mcpName)
    {
        if (!SkillMcpConstants.BuiltinMcpToolHints.TryGetValue(mcpName, out var nativeTools))
        {
            return null;
        }

        return $"\"{mcpName}\" is a builtin MCP, not a skill MCP.\nUse the native tools directly:\n{string.Join("\n", nativeTools.Select(toolName => $"  - {toolName}"))}";
    }

    public static string ApplyGrepFilter(string output, string? pattern)
    {
        if (string.IsNullOrEmpty(pattern))
        {
            return output;
        }

        try
        {
            var regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            var filtered = output.Split('\n').Where(line => regex.IsMatch(line)).ToArray();
            return filtered.Length > 0 ? string.Join("\n", filtered) : $"[grep] No lines matched pattern: {pattern}";
        }
        catch
        {
            return output;
        }
    }
}
