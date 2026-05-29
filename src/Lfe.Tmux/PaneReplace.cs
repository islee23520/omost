namespace Lfe.Tmux;

public sealed record PaneReplaceDependencies(
    Action<string, object?>? Log = null,
    Func<string, IReadOnlyList<string>, RunTmuxOptions?, CancellationToken, Task<TmuxCommandResult>>? RunTmuxCommandAsync = null,
    Func<bool>? IsInsideTmux = null,
    Func<IReadOnlyDictionary<string, string?>?, CancellationToken, Task<string?>>? GetTmuxPathAsync = null);

public static class PaneReplace
{
    public static async Task<SpawnPaneResult> ReplaceTmuxPaneAsync(
        string paneId,
        string sessionId,
        string description,
        TmuxConfig config,
        string serverUrl,
        string directory,
        PaneReplaceDependencies? dependencies = null,
        CancellationToken cancellationToken = default)
    {
        var log = dependencies?.Log ?? TmuxLogger.Log;
        var runTmuxCommandAsync = dependencies?.RunTmuxCommandAsync ?? TmuxRunner.RunTmuxCommandAsync;
        var isInsideTmux = dependencies?.IsInsideTmux ?? TmuxEnvironment.IsInsideTmux;
        var getTmuxPathAsync = dependencies?.GetTmuxPathAsync ?? TmuxPathResolver.GetTmuxPathAsync;

        log("[replaceTmuxPane] called", new { paneId, sessionId, description, serverUrl, directory });

        if (!config.Enabled || !isInsideTmux())
        {
            return new SpawnPaneResult(false);
        }

        var tmux = await getTmuxPathAsync(null, cancellationToken);
        if (string.IsNullOrEmpty(tmux))
        {
            return new SpawnPaneResult(false);
        }

        await runTmuxCommandAsync(tmux, ["send-keys", "-t", paneId, "C-c"], null, cancellationToken);
        var result = await runTmuxCommandAsync(tmux, ["respawn-pane", "-k", "-t", paneId, PaneCommand.BuildTmuxPlaceholderCommand(description)], null, cancellationToken);
        if (result.ExitCode != 0)
        {
            return new SpawnPaneResult(false);
        }

        var title = $"omo-subagent-{description[..Math.Min(description.Length, 20)]}";
        await runTmuxCommandAsync(tmux, ["select-pane", "-t", paneId, "-T", title], null, cancellationToken);
        return new SpawnPaneResult(true, paneId);
    }
}
