namespace Lfe.Tmux.Tests;

public sealed class RunnerAndServerHealthTests
{
    [Fact]
    public async Task RetriesTransientFailuresButStopsOnTerminalPaneErrors()
    {
        var retryCalls = 0;
        var terminalCalls = 0;

        await TmuxRunner.RunTmuxCommandAsync(
            "tmux",
            ["display-message"],
            new RunTmuxOptions(Retry: 2),
            new TmuxRunnerDependencies((_, _, _, _, _) =>
            {
                retryCalls++;
                return Task.FromResult(new ProcessRunResult(false, 1, string.Empty, "temporary error"));
            }));

        await TmuxRunner.RunTmuxCommandAsync(
            "tmux",
            ["display-message"],
            new RunTmuxOptions(Retry: 2),
            new TmuxRunnerDependencies((_, _, _, _, _) =>
            {
                terminalCalls++;
                return Task.FromResult(new ProcessRunResult(false, 1, string.Empty, "can't find pane: %1"));
            }));

        Assert.Equal(3, retryCalls);
        Assert.Equal(1, terminalCalls);
    }

    [Fact]
    public async Task CachesSuccessfulServerHealthChecks()
    {
        ServerHealth.ResetServerCheck();
        var calls = 0;
        var state = ServerHealth.CreateServerHealthStateForTesting();
        var dependencies = new ServerHealthDependencies(
            FetchAsync: (_, _) =>
            {
                calls++;
                return Task.FromResult(true);
            },
            DelayAsync: static (_, _) => Task.CompletedTask);

        Assert.True(await ServerHealth.IsServerRunningAsync("http://localhost:4096", new ServerHealthCheckOptions(State: state), dependencies));
        Assert.True(await ServerHealth.IsServerRunningAsync("http://localhost:4096", new ServerHealthCheckOptions(State: state), dependencies));
        Assert.Equal(1, calls);
    }
}
