namespace Omodot.Tmux;

public sealed record PaneActivateDependencies(
    Action<string, object?>? Log = null,
    Func<string, IReadOnlyList<string>, RunTmuxOptions?, CancellationToken, Task<TmuxCommandResult>>? RunTmuxCommandAsync = null,
    Func<IReadOnlyDictionary<string, string?>?, CancellationToken, Task<string?>>? GetTmuxPathAsync = null,
    Func<bool>? IsInsideTmux = null,
    Func<string, string, string?, string>? BuildTmuxAttachCommand = null);

public static class PaneActivate
{
    public static async Task<bool> ActivateTmuxPaneAsync(
        string paneId,
        string sessionId,
        string serverUrl,
        string directory,
        PaneActivateDependencies? dependencies = null,
        CancellationToken cancellationToken = default)
    {
        var log = dependencies?.Log ?? TmuxLogger.Log;
        var runTmuxCommandAsync = dependencies?.RunTmuxCommandAsync ?? TmuxRunner.RunTmuxCommandAsync;
        var getTmuxPathAsync = dependencies?.GetTmuxPathAsync ?? TmuxPathResolver.GetTmuxPathAsync;
        var isInsideTmux = dependencies?.IsInsideTmux ?? TmuxEnvironment.IsInsideTmux;
        var buildTmuxAttachCommand = dependencies?.BuildTmuxAttachCommand ?? PaneCommand.BuildTmuxAttachCommand;

        if (!isInsideTmux())
        {
            log("[activateTmuxPane] SKIP: not inside tmux", new { paneId, sessionId });
            return false;
        }

        var tmux = await getTmuxPathAsync(null, cancellationToken);
        if (string.IsNullOrEmpty(tmux))
        {
            log("[activateTmuxPane] SKIP: tmux not found", new { paneId, sessionId });
            return false;
        }

        var attachCommand = buildTmuxAttachCommand(serverUrl, sessionId, directory);
        var result = await runTmuxCommandAsync(tmux, ["respawn-pane", "-k", "-t", paneId, attachCommand], null, cancellationToken);
        return result.ExitCode == 0;
    }
}
