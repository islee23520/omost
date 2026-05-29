namespace Lfe.SlashCommand;

public enum CommandScope { User, Project, Extra }

public sealed record CommandMetadata(
    string Name,
    string Description = "",
    string? ArgumentHint = null,
    string? Model = null,
    string? Agent = null,
    bool Subtask = false
);

public sealed record SlashCommandInfo(
    string Name,
    string? Path,
    CommandMetadata Metadata,
    string? Content,
    CommandScope Scope
);

public sealed record DiscoverSlashCommandsOptions(
    string? Directory = null,
    string[]? ExtraCommandDirs = null,
    bool IncludeUserCommands = false,
    string? UserHomeDir = null
);

public sealed record HookSlashCommandInfo(
    string Name,
    string Scope,
    string? Content = null,
    string? Description = null,
    string? Model = null,
    string? Agent = null
);
