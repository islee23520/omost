using System.ComponentModel;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Lfe.Tmux;

public sealed record SweepTmuxSessionsOptions(string? Prefix = null, Func<string, bool>? Predicate = null);

public sealed record SweepTmuxSessionsDependencies(
    Func<bool>? IsInsideTmux = null,
    Func<IReadOnlyDictionary<string, string?>?, CancellationToken, Task<string?>>? GetTmuxPathAsync = null,
    Func<string, CancellationToken, Task<IReadOnlyList<string>>>? ListCandidateSessionsAsync = null,
    Func<string, CancellationToken, Task<bool>>? KillSessionAsync = null,
    Action<string, object?>? Log = null);

public sealed record SweepDependencies(
    Func<bool>? IsInsideTmux = null,
    Func<IReadOnlyDictionary<string, string?>?, CancellationToken, Task<string?>>? GetTmuxPathAsync = null,
    Func<string, CancellationToken, Task<IReadOnlyList<string>>>? ListCandidateSessionsAsync = null,
    Func<string, CancellationToken, Task<bool>>? KillSessionAsync = null,
    Action<string, object?>? Log = null,
    Func<int, bool>? ProcessAlive = null,
    int? CurrentProcessId = null);

public static class StaleSessionSweep
{
    private static readonly Regex StaleSessionPattern = new("^omo-agents-(\\d+)(?:-([A-Za-z0-9]+))?$", RegexOptions.Compiled);

    public static async Task<IReadOnlyList<string>> ListTmuxSessionsViaTmuxAsync(
        string tmux,
        Func<string, IReadOnlyList<string>, RunTmuxOptions?, CancellationToken, Task<TmuxCommandResult>>? runTmuxCommandAsync = null,
        CancellationToken cancellationToken = default)
    {
        var runner = runTmuxCommandAsync ?? TmuxRunner.RunTmuxCommandAsync;
        var result = await runner(tmux, ["list-sessions", "-F", "#{session_name}"], null, cancellationToken);
        return result.ExitCode != 0
            ? Array.Empty<string>()
            : result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    public static async Task<IReadOnlyList<string>> SweepTmuxSessionsWithAsync(
        SweepTmuxSessionsDependencies dependencies,
        SweepTmuxSessionsOptions options,
        CancellationToken cancellationToken = default)
    {
        var isInsideTmux = dependencies.IsInsideTmux ?? TmuxEnvironment.IsInsideTmux;
        if (!isInsideTmux())
        {
            return Array.Empty<string>();
        }

        var getTmuxPathAsync = dependencies.GetTmuxPathAsync ?? TmuxPathResolver.GetTmuxPathAsync;
        var tmux = await getTmuxPathAsync(null, cancellationToken);
        if (string.IsNullOrEmpty(tmux))
        {
            return Array.Empty<string>();
        }

        var listCandidateSessionsAsync = dependencies.ListCandidateSessionsAsync ?? ((tmuxPath, ct) => ListTmuxSessionsViaTmuxAsync(tmuxPath, cancellationToken: ct));
        var killSessionAsync = dependencies.KillSessionAsync ?? SessionKill.KillTmuxSessionIfExistsAsync;
        var log = dependencies.Log ?? TmuxLogger.Log;

        IReadOnlyList<string> candidateSessions;
        try
        {
            candidateSessions = await listCandidateSessionsAsync(tmux, cancellationToken);
        }
        catch (Exception error)
        {
            log("[sweepTmuxSessionsWith] failed to list candidate sessions", new { error = error.Message });
            return Array.Empty<string>();
        }

        var killedSessionNames = new List<string>();
        foreach (var sessionName in candidateSessions)
        {
            if (!MatchesSweepOptions(sessionName, options))
            {
                continue;
            }

            try
            {
                if (await killSessionAsync(sessionName, cancellationToken))
                {
                    killedSessionNames.Add(sessionName);
                }
            }
            catch (Exception error)
            {
                log("[sweepTmuxSessionsWith] failed to kill stale session", new { error = error.Message, sessionName });
            }
        }

        return killedSessionNames;
    }

    public static async Task<int> SweepStaleOmoAgentSessionsWithAsync(SweepDependencies dependencies, CancellationToken cancellationToken = default)
    {
        var currentProcessId = dependencies.CurrentProcessId ?? Environment.ProcessId;
        var processAlive = dependencies.ProcessAlive ?? IsProcessAlive;
        var killedSessionNames = await SweepTmuxSessionsWithAsync(
            new SweepTmuxSessionsDependencies(
                dependencies.IsInsideTmux,
                dependencies.GetTmuxPathAsync,
                dependencies.ListCandidateSessionsAsync,
                dependencies.KillSessionAsync,
                dependencies.Log),
            new SweepTmuxSessionsOptions(Predicate: sessionName =>
            {
                var match = StaleSessionPattern.Match(sessionName);
                if (!match.Success || !int.TryParse(match.Groups[1].Value, out var pid) || pid == currentProcessId)
                {
                    return false;
                }

                return !processAlive(pid);
            }),
            cancellationToken);

        return killedSessionNames.Count;
    }

    public static async Task<int> SweepStaleOmoAgentSessionsAsync(CancellationToken cancellationToken = default)
    {
        var dependencies = BuildRuntimeDependencies();
        return await SweepStaleOmoAgentSessionsWithAsync(dependencies, cancellationToken);
    }

    public static SweepDependencies BuildRuntimeDependencies()
        => new(
            TmuxEnvironment.IsInsideTmux,
            TmuxPathResolver.GetTmuxPathAsync,
            (tmux, ct) => ListTmuxSessionsViaTmuxAsync(tmux, cancellationToken: ct),
            SessionKill.KillTmuxSessionIfExistsAsync,
            TmuxLogger.Log,
            IsProcessAlive,
            Environment.ProcessId);

    public static bool IsProcessAlive(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch (Win32Exception)
        {
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool MatchesSweepOptions(string sessionName, SweepTmuxSessionsOptions options)
    {
        if (options.Predicate is not null)
        {
            return options.Predicate(sessionName);
        }

        return string.IsNullOrEmpty(options.Prefix) || sessionName.StartsWith(options.Prefix, StringComparison.Ordinal);
    }
}
