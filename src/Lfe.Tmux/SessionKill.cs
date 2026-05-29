namespace Lfe.Tmux;

public sealed record SessionKillDependencies(
    Action<string, object?>? Log = null,
    Func<bool>? IsInsideTmux = null,
    Func<IReadOnlyDictionary<string, string?>?, CancellationToken, Task<string?>>? GetTmuxPathAsync = null,
    Func<string, IReadOnlyList<string>, RunTmuxOptions?, CancellationToken, Task<TmuxCommandResult>>? RunTmuxCommandAsync = null);

public static class SessionKill
{
    public static async Task<bool> KillTmuxSessionIfExistsAsync(string sessionName, CancellationToken cancellationToken = default)
        => await KillTmuxSessionIfExistsWithDependenciesAsync(sessionName, null, cancellationToken);

    public static async Task<bool> KillTmuxSessionIfExistsWithDependenciesAsync(
        string sessionName,
        SessionKillDependencies? dependencies,
        CancellationToken cancellationToken = default)
    {
        var log = dependencies?.Log ?? TmuxLogger.Log;
        var isInsideTmux = dependencies?.IsInsideTmux ?? TmuxEnvironment.IsInsideTmux;
        var getTmuxPathAsync = dependencies?.GetTmuxPathAsync ?? TmuxPathResolver.GetTmuxPathAsync;
        var runTmuxCommandAsync = dependencies?.RunTmuxCommandAsync ?? TmuxRunner.RunTmuxCommandAsync;

        if (!isInsideTmux())
        {
            log("[killTmuxSessionIfExists] SKIP: not inside tmux", new { sessionName });
            return false;
        }

        var tmux = await getTmuxPathAsync(null, cancellationToken);
        if (string.IsNullOrEmpty(tmux))
        {
            log("[killTmuxSessionIfExists] SKIP: tmux not found", new { sessionName });
            return false;
        }

        var hasSessionResult = await runTmuxCommandAsync(tmux, ["has-session", "-t", sessionName], null, cancellationToken);
        if (hasSessionResult.ExitCode != 0)
        {
            return false;
        }

        var killSessionResult = await runTmuxCommandAsync(tmux, ["kill-session", "-t", sessionName], null, cancellationToken);
        return killSessionResult.ExitCode == 0;
    }
}
