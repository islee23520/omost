namespace Omodot.Tmux;

public static class PaneCommand
{
    private const string TmuxCommandShell = "/bin/sh";

    public static string BuildTmuxAttachCommand(string serverUrl, string sessionId, string? directory = null)
    {
        var escapedUrl = ShellEnv.ShellEscapeForDoubleQuotedCommand(serverUrl);
        var escapedSessionId = ShellEnv.ShellEscapeForDoubleQuotedCommand(sessionId);
        var escapedDirectory = ShellEnv.ShellEscapeForDoubleQuotedCommand(string.IsNullOrEmpty(directory) ? Environment.CurrentDirectory : directory);
        return $"{TmuxCommandShell} -c \"opencode attach {escapedUrl} --session {escapedSessionId} --dir {escapedDirectory}\"";
    }

    public static string BuildTmuxPlaceholderCommand(string description)
    {
        var escapedDescription = ShellEnv.ShellEscapeForDoubleQuotedCommand(description);
        return $"{TmuxCommandShell} -c \"printf '%s\\n%s\\n' \\\"OMO subagent pane ready: {escapedDescription}\\\" \\\"Focus this pane to attach.\\\"; exec tail -f /dev/null\"";
    }
}
