namespace Omodot.SkillMcp;

public sealed record SkillMcpArgs(
    string McpName,
    string? ToolName = null,
    string? ResourceName = null,
    string? PromptName = null,
    object? Arguments = null,
    string? Grep = null);

public sealed record SkillMcpServerConfig(string Command, string[]? Args = null, Dictionary<string, string>? Env = null);

public sealed record SkillMcpConfig(Dictionary<string, SkillMcpServerConfig> Servers);

public sealed record SkillMcpSkillLike(string Name, SkillMcpConfig? McpConfig = null);

public sealed record SkillMcpOperation(string Type, string Name);

public sealed record SkillMcpServerMatch(SkillMcpSkillLike Skill, SkillMcpServerConfig Config);
