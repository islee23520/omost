namespace Omodot.Hooks;

using System.Text.RegularExpressions;

public static partial class ToolGuards
{
    #region Bash File Read Guard

    [GeneratedRegex(@"^\s*cat\s+(?!-)[^\s|&;]+\s*$")]
    private static partial Regex CatPattern();

    [GeneratedRegex(@"^\s*head\s+(-n\s+\d+\s+)?(?!-)[^\s|&;]+\s*$")]
    private static partial Regex HeadPattern();

    [GeneratedRegex(@"^\s*tail\s+(-n\s+\d+\s+)?(?!-)[^\s|&;]+\s*$")]
    private static partial Regex TailPattern();

    public static bool IsSimpleFileReadCommand(string command) =>
        CatPattern().IsMatch(command) || HeadPattern().IsMatch(command) || TailPattern().IsMatch(command);

    #endregion

    #region WebFetch Redirect Guard

    [GeneratedRegex("redirect", RegexOptions.IgnoreCase)]
    private static partial Regex RedirectPattern();

    [GeneratedRegex("too many redirects", RegexOptions.IgnoreCase)]
    private static partial Regex TooManyRedirectsPattern();

    [GeneratedRegex("maximum redirects", RegexOptions.IgnoreCase)]
    private static partial Regex MaximumRedirectsPattern();

    private static bool IsRedirectError(string output) =>
        RedirectPattern().IsMatch(output) || TooManyRedirectsPattern().IsMatch(output) || MaximumRedirectsPattern().IsMatch(output);

    public static string BuildWebFetchRedirectLimitMessage(string? url = null)
    {
        var suffix = url is not null ? $" for {url}" : "";
        return $"Error: WebFetch failed: exceeded maximum redirects ({HookDefinitions.MaxWebfetchRedirects}){suffix}";
    }

    public static string NormalizeWebFetchRedirectOutput(string output, string? originalUrl = null)
    {
        var isToolError = output.TrimStart().ToLowerInvariant().StartsWith("error:");
        var isRedirectLoop = IsRedirectError(output);
        return isToolError && isRedirectLoop ? BuildWebFetchRedirectLimitMessage(originalUrl) : output;
    }

    #endregion

    #region Write Existing File Guard

    public static bool IsOverwriteEnabled(object? value) =>
        value is true || (value is string s && s.Equals("true", StringComparison.OrdinalIgnoreCase));

    [GeneratedRegex(@"(^|[/\\])\.omo([/\\]|$)")]
    private static partial Regex OmoWorkspacePattern();

    public static bool IsOmoWorkspacePath(string filePath) =>
        OmoWorkspacePattern().IsMatch(filePath);

    public static WriteExistingFileGuardDecision ResolveWriteExistingFileGuard(
        ToolContext input, ExistingFileGuardArgs? args, WriteExistingFileGuardOptions options)
    {
        var toolName = input.Tool?.ToLowerInvariant();
        if (toolName != "write" && toolName != "read") return WriteExistingFileGuardDecision.Allow;

        var filePath = args?.FilePath ?? args?.Path ?? args?.FilePathSnake;
        if (filePath is null) return WriteExistingFileGuardDecision.Allow;

        if (toolName == "read")
        {
            if (input.SessionID is not null && options.Exists(filePath))
            {
                options.ReadPermissions.Add(filePath);
                return WriteExistingFileGuardDecision.RegisterRead;
            }
            return WriteExistingFileGuardDecision.Allow;
        }

        var overwriteEnabled = IsOverwriteEnabled(args?.Overwrite);
        if (!options.Exists(filePath) || IsOmoWorkspacePath(filePath) || overwriteEnabled)
            return WriteExistingFileGuardDecision.Allow;

        if (input.SessionID is not null && options.ReadPermissions.Remove(filePath))
            return WriteExistingFileGuardDecision.Allow;

        return WriteExistingFileGuardDecision.Block;
    }

    #endregion

    #region Notepad Write Guard

    public static bool IsNotepadPath(string filePath) =>
        filePath.Contains("/.sisyphus/notepads/") || filePath.StartsWith(".sisyphus/notepads/");

    #endregion

    #region Non-Interactive Env

    private static readonly string[] BannedInteractiveCommands =
        ["vim", "nano", "vi", "emacs", "less", "more", "man", "git add -p", "git rebase -i"];

    public static string? DetectBannedInteractiveCommand(string command)
    {
        foreach (var candidate in BannedInteractiveCommands)
        {
            if (Regex.IsMatch(command, $@"\b{Regex.Escape(candidate)}\b"))
                return candidate;
        }
        return null;
    }

    private static readonly Dictionary<string, string> NonInteractiveEnv = new()
    {
        ["CI"] = "true",
        ["DEBIAN_FRONTEND"] = "noninteractive",
        ["GIT_TERMINAL_PROMPT"] = "0",
        ["GCM_INTERACTIVE"] = "never",
        ["HOMEBREW_NO_AUTO_UPDATE"] = "1",
        ["GIT_EDITOR"] = ":",
        ["EDITOR"] = ":",
        ["VISUAL"] = "",
        ["GIT_SEQUENCE_EDITOR"] = ":",
        ["GIT_MERGE_AUTOEDIT"] = "no",
        ["GIT_PAGER"] = "cat",
        ["PAGER"] = "cat",
        ["npm_config_yes"] = "true",
        ["PIP_NO_INPUT"] = "1",
        ["YARN_ENABLE_IMMUTABLE_INSTALLS"] = "false",
    };

    public static string BuildNonInteractiveEnvPrefix(ShellType shellType = ShellType.Posix)
    {
        var entries = NonInteractiveEnv.ToList();
        return shellType switch
        {
            ShellType.Cmd => string.Join(" && ", entries.Select(e => $"set {e.Key}={e.Value}")),
            ShellType.PowerShell => string.Join("; ", entries.Select(e => $"$env:{e.Key}='{e.Value.Replace("'", "''")}'")),
            _ => string.Join(" ", entries.Select(e => $"{e.Key}={JsonEscape(e.Value)}")),
        };
    }

    public static string BuildNonInteractiveGitCommand(string command, ShellType shellType = ShellType.Posix)
    {
        if (!Regex.IsMatch(command, @"\bgit\b")) return command;
        var prefix = BuildNonInteractiveEnvPrefix(shellType);
        return command.TrimStart().StartsWith(prefix.Trim()) ? command : $"{prefix} {command}";
    }

    private static string JsonEscape(string value) =>
        System.Text.Json.JsonSerializer.Serialize(value);

    #endregion

    #region Prometheus Guard

    public static bool IsPrometheusAgent(string? agentName) =>
        agentName?.ToLowerInvariant().Contains("prometheus") ?? false;

    public static bool IsPrometheusAllowedFile(string filePath, string workspaceRoot)
    {
        var resolvedPath = Path.GetFullPath(Path.Combine(workspaceRoot, filePath));
        var relativePath = Path.GetRelativePath(workspaceRoot, resolvedPath);
        return !relativePath.StartsWith("..") && !Path.IsPathRooted(relativePath)
            && Regex.IsMatch(relativePath, @"(^|[/\\])\.omo([/\\]|$)", RegexOptions.IgnoreCase)
            && resolvedPath.EndsWith(".md", StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Todo Tools

    public static bool HasIncompleteTodos(IEnumerable<TodoLike> todos) =>
        todos.Any(t => t.Status != "completed" && t.Status != "cancelled");

    public static bool ShouldBlockTaskTodoTool(string tool, bool taskSystemEnabled) =>
        taskSystemEnabled && HookDefinitions.TaskTodowriteBlockedTools
            .Any(blocked => blocked.Equals(tool, StringComparison.OrdinalIgnoreCase));

    #endregion
}

public sealed record ToolContext(string? Tool = null, string? SessionID = null);

public sealed class WriteExistingFileGuardOptions
{
    public required Func<string, bool> Exists { get; init; }
    public required HashSet<string> ReadPermissions { get; init; } = [];
}
