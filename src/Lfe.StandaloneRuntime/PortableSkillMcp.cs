using System.Text.Json;
using Lfe.SkillMcp;
using Lfe.SkillsCore;

namespace Lfe.StandaloneRuntime;

public static class PortableSkillMcp
{
    public static PortableToolDefinition CreatePortableSkillMcpTool(IReadOnlyList<SkillMcpSkillLike> skills)
    {
        return new PortableToolDefinition
        {
            Name = "skill_mcp",
            Description = SkillMcpConstants.SkillMcpDescription,
            ExecuteAsync = (params_) =>
            {
                var args = new SkillMcpArgs(
                    McpName: RequireString(params_, "mcp_name"),
                    ToolName: OptionalString(params_, "tool_name"),
                    ResourceName: OptionalString(params_, "resource_name"),
                    PromptName: OptionalString(params_, "prompt_name"),
                    Arguments: params_.TryGetValue("arguments", out var argVal) ? argVal : null,
                    Grep: OptionalString(params_, "grep"));

                var operation = SkillMcpHelpers.ValidateSkillMcpOperation(args);
                var found = SkillMcpHelpers.FindSkillMcpServer(args.McpName, skills);
                if (found is null)
                {
                    var builtinHint = SkillMcpHelpers.FormatBuiltinMcpHint(args.McpName);
                    if (builtinHint is not null)
                        throw new InvalidOperationException(builtinHint);
                    throw new InvalidOperationException(
                        $"MCP server \"{args.McpName}\" not found.\n\n" +
                        $"Available MCP servers in loaded skills:\n{SkillMcpHelpers.FormatAvailableSkillMcps(skills)}\n\n" +
                        "Hint: load or enable the skill that declares this MCP before calling skill_mcp.");
                }

                var parsedArgs = SkillMcpArgumentParser.ParseSkillMcpArguments(args.Arguments);
                var output = JsonSerializer.Serialize(new
                {
                    portable = true,
                    note = "Portable skill_mcp surface validates configuration and arguments, but does not start adapter-bound MCP manager clients.",
                    serverName = args.McpName,
                    skillName = found.Skill.Name,
                    command = found.Config.Command,
                    args_ = found.Config.Args ?? [],
                    env = found.Config.Env ?? new Dictionary<string, string>(),
                    operation,
                    parsedArguments = parsedArgs,
                }, new JsonSerializerOptions { WriteIndented = true });

                return Task.FromResult(SkillMcpHelpers.ApplyGrepFilter(output, args.Grep));
            },
        };
    }

    public static List<string> ListPortableSkillMcpServers(IReadOnlyList<SkillMcpSkillLike> skills)
        => skills
            .Where(s => s.McpConfig is not null)
            .SelectMany(s => s.McpConfig!.Servers.Keys)
            .ToList();

    private static string RequireString(Dictionary<string, object?> params_, string key)
    {
        if (params_.TryGetValue(key, out var value) && value is string s && s.Length > 0)
            return s;
        throw new ArgumentException($"Missing required string parameter '{key}'");
    }

    private static string? OptionalString(Dictionary<string, object?> params_, string key)
    {
        if (params_.TryGetValue(key, out var value) && value is string s)
            return s;
        return null;
    }
}
