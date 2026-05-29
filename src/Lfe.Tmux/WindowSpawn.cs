namespace Lfe.Tmux;

public sealed record WindowSpawnDependencies(
    Action<string, object?>? Log = null,
    Func<string, IReadOnlyList<string>, RunTmuxOptions?, CancellationToken, Task<TmuxCommandResult>>? RunTmuxCommandAsync = null,
    Func<bool>? IsInsideTmux = null,
    Func<string, CancellationToken, Task<bool>>? IsServerRunningAsync = null,
    Func<IReadOnlyDictionary<string, string?>?, CancellationToken, Task<string?>>? GetTmuxPathAsync = null);

public static class WindowSpawn
{
    private const string IsolatedWindowName = "omo-agents";

    public static async Task<SpawnPaneResult> SpawnTmuxWindowAsync(
        string sessionId,
        string description,
        TmuxConfig config,
        string serverUrl,
        string directory,
        WindowSpawnDependencies? dependencies = null,
        CancellationToken cancellationToken = default)
    {
        var log = dependencies?.Log ?? TmuxLogger.Log;
        var runTmuxCommandAsync = dependencies?.RunTmuxCommandAsync ?? TmuxRunner.RunTmuxCommandAsync;
        var isInsideTmux = dependencies?.IsInsideTmux ?? TmuxEnvironment.IsInsideTmux;
        var isServerRunningAsync = dependencies?.IsServerRunningAsync ?? ((url, ct) => ServerHealth.IsServerRunningAsync(url, cancellationToken: ct));
        var getTmuxPathAsync = dependencies?.GetTmuxPathAsync ?? TmuxPathResolver.GetTmuxPathAsync;

        log("[spawnTmuxWindow] called", new { sessionId, description, serverUrl, directory });

        if (!config.Enabled || !isInsideTmux() || !await isServerRunningAsync(serverUrl, cancellationToken))
        {
            return new SpawnPaneResult(false);
        }

        var tmux = await getTmuxPathAsync(null, cancellationToken);
        if (string.IsNullOrEmpty(tmux))
        {
            return new SpawnPaneResult(false);
        }

        var result = await runTmuxCommandAsync(
            tmux,
            ["new-window", "-d", "-n", IsolatedWindowName, "-P", "-F", "#{pane_id}", PaneCommand.BuildTmuxPlaceholderCommand(description)],
            null,
            cancellationToken);

        if (result.ExitCode != 0 || string.IsNullOrEmpty(result.Output))
        {
            return new SpawnPaneResult(false);
        }

        var paneId = result.Output;
        var title = $"omo-subagent-{description[..Math.Min(description.Length, 20)]}";
        await runTmuxCommandAsync(tmux, ["select-pane", "-t", paneId, "-T", title], null, cancellationToken);
        return new SpawnPaneResult(true, paneId);
    }
}
