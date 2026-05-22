namespace Omodot.Tmux.Tests;

internal static class TestData
{
    public static readonly TmuxConfig EnabledTmuxConfig = new(
        Enabled: true,
        Layout: TmuxLayout.MainVertical,
        MainPaneSize: 60,
        MainPaneMinWidth: 120,
        AgentPaneMinWidth: 40,
        Isolation: TmuxIsolationMode.Inline);

    public static TmuxCommandResult TmuxResult(
        bool success = true,
        string output = "",
        string? stdout = null,
        string stderr = "",
        int exitCode = 0)
        => new(success, output, stdout ?? output, stderr, exitCode);
}
