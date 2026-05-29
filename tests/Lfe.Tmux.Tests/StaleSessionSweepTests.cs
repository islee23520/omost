namespace Lfe.Tmux.Tests;

public sealed class StaleSessionSweepTests
{
    [Fact]
    public async Task KillsDeadAgentSessionsButNotCurrentOrAliveOnes()
    {
        var killed = new List<string>();
        var dependencies = new SweepDependencies(
            IsInsideTmux: static () => true,
            GetTmuxPathAsync: static (_, _) => Task.FromResult<string?>("tmux"),
            ListCandidateSessionsAsync: static (_, _) => Task.FromResult<IReadOnlyList<string>>([
                "main",
                "lfe-agents-12345",
                "lfe-agents-88888",
                "lfe-agents-99999",
                "lfe-agents-99991-abc",
            ]),
            KillSessionAsync: (sessionName, _) =>
            {
                killed.Add(sessionName);
                return Task.FromResult(true);
            },
            Log: static (_, _) => { },
            ProcessAlive: static pid => pid == 88888,
            CurrentProcessId: 12345);

        var count = await StaleSessionSweep.SweepStaleLfeAgentSessionsWithAsync(dependencies);

        Assert.Equal(2, count);
        Assert.Equal(["lfe-agents-99999", "lfe-agents-99991-abc"], killed);
    }

    [Fact]
    public async Task SupportsPrefixAndPredicateSweeping()
    {
        var killed = new List<string>();
        var dependencies = new SweepTmuxSessionsDependencies(
            IsInsideTmux: static () => true,
            GetTmuxPathAsync: static (_, _) => Task.FromResult<string?>("tmux"),
            ListCandidateSessionsAsync: static (_, _) => Task.FromResult<IReadOnlyList<string>>(["lfe-team-A", "lfe-team-B", "main", "lfe-agents-99999"]),
            KillSessionAsync: (sessionName, _) =>
            {
                killed.Add(sessionName);
                return Task.FromResult(true);
            },
            Log: static (_, _) => { });

        var prefixResult = await StaleSessionSweep.SweepTmuxSessionsWithAsync(dependencies, new SweepTmuxSessionsOptions(Prefix: "lfe-team-"));
        var predicateResult = await StaleSessionSweep.SweepTmuxSessionsWithAsync(
            new SweepTmuxSessionsDependencies(
                IsInsideTmux: static () => true,
                GetTmuxPathAsync: static (_, _) => Task.FromResult<string?>("tmux"),
                ListCandidateSessionsAsync: static (_, _) => Task.FromResult<IReadOnlyList<string>>(["keep", "kill-me"]),
                KillSessionAsync: static (sessionName, _) => Task.FromResult(sessionName == "kill-me"),
                Log: static (_, _) => { }),
            new SweepTmuxSessionsOptions(Predicate: static sessionName => sessionName == "kill-me"));

        Assert.Equal(["lfe-team-A", "lfe-team-B"], prefixResult);
        Assert.Equal(["lfe-team-A", "lfe-team-B"], killed);
        Assert.Equal(["kill-me"], predicateResult);
    }
}
