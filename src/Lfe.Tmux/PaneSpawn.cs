namespace Lfe.Tmux;

public sealed record PaneSpawnDependencies(
    Action<string, object?>? Log = null,
    Func<string, IReadOnlyList<string>, RunTmuxOptions?, CancellationToken, Task<TmuxCommandResult>>? RunTmuxCommandAsync = null,
    Func<bool>? IsInsideTmux = null,
    Func<string, CancellationToken, Task<bool>>? IsServerRunningAsync = null,
    Func<IReadOnlyDictionary<string, string?>?, CancellationToken, Task<string?>>? GetTmuxPathAsync = null);

public static class PaneSpawn
{
    public static async Task<SpawnPaneResult> SpawnTmuxPaneAsync(
        string sessionId,
        string description,
        TmuxConfig config,
        string serverUrl,
        string directory,
        string? targetPaneId = null,
        SplitDirection splitDirection = SplitDirection.Horizontal,
        PaneSpawnDependencies? dependencies = null,
        CancellationToken cancellationToken = default)
    {
        var log = dependencies?.Log ?? TmuxLogger.Log;
        var runTmuxCommandAsync = dependencies?.RunTmuxCommandAsync ?? TmuxRunner.RunTmuxCommandAsync;
        var isInsideTmux = dependencies?.IsInsideTmux ?? TmuxEnvironment.IsInsideTmux;
        var isServerRunningAsync = dependencies?.IsServerRunningAsync ?? ((url, ct) => ServerHealth.IsServerRunningAsync(url, cancellationToken: ct));
        var getTmuxPathAsync = dependencies?.GetTmuxPathAsync ?? TmuxPathResolver.GetTmuxPathAsync;

        log("[spawnTmuxPane] called", new { sessionId, description, serverUrl, configEnabled = config.Enabled, targetPaneId, splitDirection, directory });

        if (!config.Enabled || !isInsideTmux() || !await isServerRunningAsync(serverUrl, cancellationToken))
        {
            return new SpawnPaneResult(false);
        }

        var tmux = await getTmuxPathAsync(null, cancellationToken);
        if (string.IsNullOrEmpty(tmux))
        {
            return new SpawnPaneResult(false);
        }

        var placeholderCommand = PaneCommand.BuildTmuxPlaceholderCommand(description);
        var args = new List<string>
        {
            "split-window",
            splitDirection.ToTmuxArgument(),
            "-d",
            "-P",
            "-F",
            "#{pane_id}",
        };

        if (!string.IsNullOrEmpty(targetPaneId))
        {
            args.AddRange(["-t", targetPaneId]);
        }

        args.Add(placeholderCommand);

        var result = await runTmuxCommandAsync(tmux, args, null, cancellationToken);
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
