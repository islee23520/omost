namespace Omodot.CommandExecutor;

public sealed record ExecuteHookOptions(
    bool ForceZsh = false,
    string? ZshPath = null,
    int TimeoutMs = 30_000,
    IReadOnlyList<string>? AllowedEnvVars = null);
