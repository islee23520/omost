namespace Omodot.Tmux;

public sealed record SessionSpawnDependencies(
    Action<string, object?>? Log = null,
    Func<string, IReadOnlyList<string>, RunTmuxOptions?, CancellationToken, Task<TmuxCommandResult>>? RunTmuxCommandAsync = null,
    Func<bool>? IsInsideTmux = null,
    Func<string, CancellationToken, Task<bool>>? IsServerRunningAsync = null,
    Func<IReadOnlyDictionary<string, string?>?, CancellationToken, Task<string?>>? GetTmuxPathAsync = null,
    int? CurrentProcessId = null);

public static class SessionSpawn
{
    private const string IsolatedSessionNamePrefix = "omo-agents";

    public static string GetIsolatedSessionName(int? processId = null, string? managerId = null)
    {
        var pid = processId ?? Environment.ProcessId;
        return string.IsNullOrEmpty(managerId)
            ? $"{IsolatedSessionNamePrefix}-{pid}"
            : $"{IsolatedSessionNamePrefix}-{pid}-{managerId}";
    }

    public static async Task<SpawnPaneResult> SpawnTmuxSessionAsync(
        string sessionId,
        string description,
        TmuxConfig config,
        string serverUrl,
        string directory,
        string? sourcePaneId = null,
        SessionSpawnDependencies? dependencies = null,
        string? managerId = null,
        CancellationToken cancellationToken = default)
    {
        var log = dependencies?.Log ?? TmuxLogger.Log;
        var runTmuxCommandAsync = dependencies?.RunTmuxCommandAsync ?? TmuxRunner.RunTmuxCommandAsync;
        var isInsideTmux = dependencies?.IsInsideTmux ?? TmuxEnvironment.IsInsideTmux;
        var isServerRunningAsync = dependencies?.IsServerRunningAsync ?? ((url, ct) => ServerHealth.IsServerRunningAsync(url, cancellationToken: ct));
        var getTmuxPathAsync = dependencies?.GetTmuxPathAsync ?? TmuxPathResolver.GetTmuxPathAsync;
        var currentProcessId = dependencies?.CurrentProcessId ?? Environment.ProcessId;

        log("[spawnTmuxSession] called", new { sessionId, description, serverUrl, directory });

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
        var sizeArgs = new List<string>();

        if (!string.IsNullOrEmpty(sourcePaneId))
        {
            var dimensions = await GetWindowDimensionsAsync(tmux, sourcePaneId, runTmuxCommandAsync, cancellationToken);
            if (dimensions is not null)
            {
                sizeArgs.AddRange(["-x", dimensions.Value.Width.ToString(), "-y", dimensions.Value.Height.ToString()]);
            }
        }

        var isolatedSessionName = GetIsolatedSessionName(currentProcessId, managerId);
        var sessionAlreadyExists = await SessionExistsAsync(tmux, isolatedSessionName, runTmuxCommandAsync, cancellationToken);
        var args = sessionAlreadyExists
            ? new List<string> { "new-window", "-t", isolatedSessionName, "-P", "-F", "#{pane_id}", placeholderCommand }
            : ["new-session", "-d", "-s", isolatedSessionName, .. sizeArgs, "-P", "-F", "#{pane_id}", placeholderCommand];

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

    private static async Task<(int Width, int Height)?> GetWindowDimensionsAsync(
        string tmux,
        string sourcePaneId,
        Func<string, IReadOnlyList<string>, RunTmuxOptions?, CancellationToken, Task<TmuxCommandResult>> runTmuxCommandAsync,
        CancellationToken cancellationToken)
    {
        var result = await runTmuxCommandAsync(tmux, ["display", "-p", "-t", sourcePaneId, "#{window_width},#{window_height}"], null, cancellationToken);
        if (result.ExitCode != 0)
        {
            return null;
        }

        var parts = result.Output.Split(',', StringSplitOptions.TrimEntries);
        return parts.Length == 2 && int.TryParse(parts[0], out var width) && int.TryParse(parts[1], out var height)
            ? (width, height)
            : null;
    }

    private static async Task<bool> SessionExistsAsync(
        string tmux,
        string sessionName,
        Func<string, IReadOnlyList<string>, RunTmuxOptions?, CancellationToken, Task<TmuxCommandResult>> runTmuxCommandAsync,
        CancellationToken cancellationToken)
    {
        var result = await runTmuxCommandAsync(tmux, ["has-session", "-t", sessionName], null, cancellationToken);
        return result.ExitCode == 0;
    }
}
