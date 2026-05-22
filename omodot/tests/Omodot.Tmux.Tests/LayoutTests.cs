namespace Omodot.Tmux.Tests;

public sealed class LayoutTests
{
    [Fact]
    public async Task ApplyLayoutSetsMainPaneWidthForMainVertical()
    {
        var calls = new List<IReadOnlyList<string>>();
        var dependencies = new LayoutDependencies(
            SpawnCommandAsync: (args, _) =>
            {
                calls.Add(args.ToArray());
                return Task.FromResult(0);
            });

        await Layout.ApplyLayoutAsync("tmux", TmuxLayout.MainVertical, 60, dependencies);

        Assert.Equal(new[] { "tmux", "select-layout", "main-vertical" }, calls[0]);
        Assert.Equal(new[] { "tmux", "set-window-option", "main-pane-width", "60%" }, calls[1]);
    }

    [Fact]
    public void CalculateMainPaneWidthHonorsMinWidths()
    {
        var width = Layout.CalculateMainPaneWidth(200, new MainPaneWidthOptions(MainPaneSize: 60, MainPaneMinWidth: 120, AgentPaneMinWidth: 40));
        Assert.Equal(120, width);
    }

    [Fact]
    public async Task BuildAttachCommandEscapesInputs()
    {
        var command = PaneCommand.BuildTmuxAttachCommand("http://localhost:3000$(whoami);rm -rf /", "ses_abc\"`x`", "/tmp/a b");

        Assert.StartsWith("/bin/sh -c \"", command, StringComparison.Ordinal);
        Assert.Contains("\\$", command, StringComparison.Ordinal);
        Assert.Contains("\\;", command, StringComparison.Ordinal);
        Assert.Contains("\\\"", command, StringComparison.Ordinal);
        Assert.Contains("\\`", command, StringComparison.Ordinal);
        Assert.Contains("--dir /tmp/a b", command, StringComparison.Ordinal);

        await Task.CompletedTask;
    }
}
