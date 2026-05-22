namespace Omodot.Tmux;

public static class TmuxUtils
{
    public static bool IsInsideTmux() => TmuxEnvironment.IsInsideTmux();
    public static bool IsInsideTmuxEnvironment(IReadOnlyDictionary<string, string?> environment) => TmuxEnvironment.IsInsideTmuxEnvironment(environment);
    public static string? GetCurrentPaneId(IReadOnlyDictionary<string, string?>? environment = null) => TmuxEnvironment.GetCurrentPaneId(environment);
    public static ServerHealthState CreateServerHealthStateForTesting() => ServerHealth.CreateServerHealthStateForTesting();
    public static Task<bool> IsServerRunningAsync(string serverUrl, ServerHealthCheckOptions? options = null, ServerHealthDependencies? dependencies = null, CancellationToken cancellationToken = default) => ServerHealth.IsServerRunningAsync(serverUrl, options, dependencies, cancellationToken);
    public static void MarkServerRunningInProcess() => ServerHealth.MarkServerRunningInProcess();
    public static void ResetServerCheck() => ServerHealth.ResetServerCheck();
    public static Task<PaneDimensions?> GetPaneDimensionsAsync(string paneId, PaneDimensionsDependencies? dependencies = null, CancellationToken cancellationToken = default) => PaneDimensionsReader.GetPaneDimensionsAsync(paneId, dependencies, cancellationToken);
    public static Task<SpawnPaneResult> SpawnTmuxPaneAsync(string sessionId, string description, TmuxConfig config, string serverUrl, string directory, string? targetPaneId = null, SplitDirection splitDirection = SplitDirection.Horizontal, PaneSpawnDependencies? dependencies = null, CancellationToken cancellationToken = default) => PaneSpawn.SpawnTmuxPaneAsync(sessionId, description, config, serverUrl, directory, targetPaneId, splitDirection, dependencies, cancellationToken);
    public static Task<bool> CloseTmuxPaneAsync(string paneId, CancellationToken cancellationToken = default) => PaneClose.CloseTmuxPaneAsync(paneId, cancellationToken);
    public static Task<bool> CloseTmuxPaneWithDependenciesAsync(string paneId, PaneCloseDependencies? dependencies, CancellationToken cancellationToken = default) => PaneClose.CloseTmuxPaneWithDependenciesAsync(paneId, dependencies, cancellationToken);
    public static Task<SpawnPaneResult> ReplaceTmuxPaneAsync(string paneId, string sessionId, string description, TmuxConfig config, string serverUrl, string directory, PaneReplaceDependencies? dependencies = null, CancellationToken cancellationToken = default) => PaneReplace.ReplaceTmuxPaneAsync(paneId, sessionId, description, config, serverUrl, directory, dependencies, cancellationToken);
    public static Task<bool> ActivateTmuxPaneAsync(string paneId, string sessionId, string serverUrl, string directory, PaneActivateDependencies? dependencies = null, CancellationToken cancellationToken = default) => PaneActivate.ActivateTmuxPaneAsync(paneId, sessionId, serverUrl, directory, dependencies, cancellationToken);
    public static Task<SpawnPaneResult> SpawnTmuxWindowAsync(string sessionId, string description, TmuxConfig config, string serverUrl, string directory, WindowSpawnDependencies? dependencies = null, CancellationToken cancellationToken = default) => WindowSpawn.SpawnTmuxWindowAsync(sessionId, description, config, serverUrl, directory, dependencies, cancellationToken);
    public static string GetIsolatedSessionName(int? processId = null, string? managerId = null) => SessionSpawn.GetIsolatedSessionName(processId, managerId);
    public static Task<SpawnPaneResult> SpawnTmuxSessionAsync(string sessionId, string description, TmuxConfig config, string serverUrl, string directory, string? sourcePaneId = null, SessionSpawnDependencies? dependencies = null, string? managerId = null, CancellationToken cancellationToken = default) => SessionSpawn.SpawnTmuxSessionAsync(sessionId, description, config, serverUrl, directory, sourcePaneId, dependencies, managerId, cancellationToken);
    public static Task<bool> KillTmuxSessionIfExistsAsync(string sessionName, CancellationToken cancellationToken = default) => SessionKill.KillTmuxSessionIfExistsAsync(sessionName, cancellationToken);
    public static Task<IReadOnlyList<string>> SweepTmuxSessionsWithAsync(SweepTmuxSessionsDependencies dependencies, SweepTmuxSessionsOptions options, CancellationToken cancellationToken = default) => StaleSessionSweep.SweepTmuxSessionsWithAsync(dependencies, options, cancellationToken);
    public static Task<int> SweepStaleOmoAgentSessionsWithAsync(SweepDependencies dependencies, CancellationToken cancellationToken = default) => StaleSessionSweep.SweepStaleOmoAgentSessionsWithAsync(dependencies, cancellationToken);
    public static Task<int> SweepStaleOmoAgentSessionsAsync(CancellationToken cancellationToken = default) => StaleSessionSweep.SweepStaleOmoAgentSessionsAsync(cancellationToken);
    public static string BuildTmuxAttachCommand(string serverUrl, string sessionId, string? directory = null) => PaneCommand.BuildTmuxAttachCommand(serverUrl, sessionId, directory);
    public static string BuildTmuxPlaceholderCommand(string description) => PaneCommand.BuildTmuxPlaceholderCommand(description);
    public static Task ApplyLayoutAsync(string tmux, TmuxLayout layout, int mainPaneSize, LayoutDependencies? dependencies = null, CancellationToken cancellationToken = default) => Layout.ApplyLayoutAsync(tmux, layout, mainPaneSize, dependencies, cancellationToken);
    public static Task EnforceMainPaneWidthAsync(string mainPaneId, int windowWidth, int mainPaneSize, LayoutDependencies? dependencies = null, CancellationToken cancellationToken = default) => Layout.EnforceMainPaneWidthAsync(mainPaneId, windowWidth, mainPaneSize, dependencies, cancellationToken);
    public static Task EnforceMainPaneWidthAsync(string mainPaneId, int windowWidth, MainPaneWidthOptions? options, LayoutDependencies? dependencies = null, CancellationToken cancellationToken = default) => Layout.EnforceMainPaneWidthAsync(mainPaneId, windowWidth, options, dependencies, cancellationToken);
}
