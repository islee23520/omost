namespace Omodot.SkillsCore;

public sealed record SkillMcpServerConfig(string Command, string[]? Args = null, Dictionary<string, string>? Env = null);

public sealed record SkillMcpConfig(Dictionary<string, SkillMcpServerConfig> Servers);

public sealed record BuiltinSkill(
    string Name,
    string Description,
    string Template,
    string? License = null,
    string? Compatibility = null,
    Dictionary<string, object?>? Metadata = null,
    string[]? AllowedTools = null,
    string? Agent = null,
    string? Model = null,
    bool Subtask = false,
    string? ArgumentHint = null,
    SkillMcpConfig? McpConfig = null);
