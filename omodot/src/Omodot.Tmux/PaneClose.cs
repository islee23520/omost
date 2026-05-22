using System.Text.RegularExpressions;

namespace Omodot.Tmux;

public sealed record PaneCloseDependencies(
    Func<bool>? IsInsideTmux = null,
    Func<IReadOnlyDictionary<string, string?>?, CancellationToken, Task<string?>>? GetTmuxPathAsync = null,
    Func<string, IReadOnlyList<string>, RunTmuxOptions?, CancellationToken, Task<TmuxCommandResult>>? RunTmuxCommandAsync = null,
    Action<string, object?>? Log = null,
    Func<TimeSpan, CancellationToken, Task>? DelayAsync = null);

public static class PaneClose
{
    private static readonly Regex PaneAlreadyGonePattern = new("can't find pane", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static async Task<bool> CloseTmuxPaneAsync(string paneId, CancellationToken cancellationToken = default)
        => await CloseTmuxPaneWithDependenciesAsync(paneId, null, cancellationToken);

    public static async Task<bool> CloseTmuxPaneWithDependenciesAsync(
        string paneId,
        PaneCloseDependencies? dependencies,
        CancellationToken cancellationToken = default)
    {
        var isInsideTmux = dependencies?.IsInsideTmux ?? TmuxEnvironment.IsInsideTmux;
        var getTmuxPathAsync = dependencies?.GetTmuxPathAsync ?? TmuxPathResolver.GetTmuxPathAsync;
        var runTmuxCommandAsync = dependencies?.RunTmuxCommandAsync ?? TmuxRunner.RunTmuxCommandAsync;
        var log = dependencies?.Log ?? TmuxLogger.Log;
        var delayAsync = dependencies?.DelayAsync ?? ((TimeSpan delay, CancellationToken ct) => Task.Delay(delay, ct));

        if (!isInsideTmux())
        {
            log("[closeTmuxPane] SKIP: not inside tmux", null);
            return false;
        }

        var tmux = await getTmuxPathAsync(null, cancellationToken);
        if (string.IsNullOrEmpty(tmux))
        {
            log("[closeTmuxPane] SKIP: tmux not found", null);
            return false;
        }

        await runTmuxCommandAsync(tmux, ["send-keys", "-t", paneId, "C-c"], null, cancellationToken);
        await delayAsync(TimeSpan.FromMilliseconds(250), cancellationToken);
        var result = await runTmuxCommandAsync(tmux, ["kill-pane", "-t", paneId], null, cancellationToken);

        if (result.ExitCode != 0 && PaneAlreadyGonePattern.IsMatch(result.Stderr.Trim()))
        {
            return true;
        }

        return result.ExitCode == 0;
    }
}
