namespace Omodot.CommandExecutor;

public sealed record CommandResult(int ExitCode, string? Stdout = null, string? Stderr = null);
