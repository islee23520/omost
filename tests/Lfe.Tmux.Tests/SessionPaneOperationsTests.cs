namespace Lfe.Tmux.Tests;

public sealed class SessionPaneOperationsTests
{
    [Fact]
    public async Task SpawnTmuxSessionBuildsExpectedCommands()
    {
        var calls = new List<(string Command, IReadOnlyList<string> Args)>();
        var queue = new Queue<TmuxCommandResult>([
            TestData.TmuxResult(output: "120,40"),
            TestData.TmuxResult(success: false, exitCode: 1),
            TestData.TmuxResult(output: "%42"),
            TestData.TmuxResult(),
        ]);

        var dependencies = new SessionSpawnDependencies(
            Log: static (_, _) => { },
            RunTmuxCommandAsync: (command, args, _, _) =>
            {
                calls.Add((command, args.ToArray()));
                return Task.FromResult(queue.Dequeue());
            },
            IsInsideTmux: static () => true,
            IsServerRunningAsync: static (_, _) => Task.FromResult(true),
            GetTmuxPathAsync: static (_, _) => Task.FromResult<string?>("tmux"),
            CurrentProcessId: 999);

        var result = await SessionSpawn.SpawnTmuxSessionAsync("session-1", "worker", TestData.EnabledTmuxConfig, "http://127.0.0.1:1234", "/tmp/project", "%0", dependencies, "mgr");

        Assert.Equal(new SpawnPaneResult(true, "%42"), result);
        Assert.Equal(new[] { "display", "-p", "-t", "%0", "#{window_width},#{window_height}" }, calls[0].Args);
        Assert.Equal(new[] { "has-session", "-t", "omo-agents-999-mgr" }, calls[1].Args);
        Assert.Equal(new[] { "new-session", "-d", "-s", "omo-agents-999-mgr", "-x", "120", "-y", "40" }, calls[2].Args.Take(8));
        Assert.Contains("Focus this pane to attach.", calls[2].Args.Last());
        Assert.Equal(new[] { "select-pane", "-t", "%42", "-T", "omo-subagent-worker" }, calls[3].Args);
    }

    [Fact]
    public async Task SpawnTmuxPaneBuildsSplitWindowAndTitleCommands()
    {
        var calls = new List<IReadOnlyList<string>>();
        var queue = new Queue<TmuxCommandResult>([
            TestData.TmuxResult(output: "%42"),
            TestData.TmuxResult(),
        ]);

        var dependencies = new PaneSpawnDependencies(
            Log: static (_, _) => { },
            RunTmuxCommandAsync: (_, args, _, _) =>
            {
                calls.Add(args.ToArray());
                return Task.FromResult(queue.Dequeue());
            },
            IsInsideTmux: static () => true,
            IsServerRunningAsync: static (_, _) => Task.FromResult(true),
            GetTmuxPathAsync: static (_, _) => Task.FromResult<string?>("tmux"));

        var result = await PaneSpawn.SpawnTmuxPaneAsync("session-1", "worker", TestData.EnabledTmuxConfig, "http://127.0.0.1:1234", "/tmp/project", "%0", SplitDirection.Horizontal, dependencies);

        Assert.Equal(new SpawnPaneResult(true, "%42"), result);
        Assert.Equal(new[] { "split-window", "-h", "-d", "-P", "-F", "#{pane_id}", "-t", "%0" }, calls[0].Take(8));
        Assert.Contains("Focus this pane to attach.", calls[0].Last());
        Assert.Equal(new[] { "select-pane", "-t", "%42", "-T", "omo-subagent-worker" }, calls[1]);
    }

    [Fact]
    public async Task ReplacePaneSendsCtrlCThenRespawnsPane()
    {
        var calls = new List<IReadOnlyList<string>>();
        var queue = new Queue<TmuxCommandResult>([
            TestData.TmuxResult(),
            TestData.TmuxResult(),
            TestData.TmuxResult(),
        ]);

        var dependencies = new PaneReplaceDependencies(
            Log: static (_, _) => { },
            RunTmuxCommandAsync: (_, args, _, _) =>
            {
                calls.Add(args.ToArray());
                return Task.FromResult(queue.Dequeue());
            },
            IsInsideTmux: static () => true,
            GetTmuxPathAsync: static (_, _) => Task.FromResult<string?>("tmux"));

        var result = await PaneReplace.ReplaceTmuxPaneAsync("%42", "session-1", "worker", TestData.EnabledTmuxConfig, "http://127.0.0.1:1234", "/tmp/project", dependencies);

        Assert.Equal(new SpawnPaneResult(true, "%42"), result);
        Assert.Equal(new[] { "send-keys", "-t", "%42", "C-c" }, calls[0]);
        Assert.Equal(new[] { "respawn-pane", "-k", "-t", "%42" }, calls[1].Take(4));
        Assert.Contains("Focus this pane to attach.", calls[1].Last());
        Assert.Equal(new[] { "select-pane", "-t", "%42", "-T", "omo-subagent-worker" }, calls[2]);
    }
}
