using System.Text.RegularExpressions;

namespace Lfe.Tmux;

public sealed record RunTmuxOptions(
    int Retry = 0,
    int? TimeoutMs = null,
    IReadOnlyDictionary<string, string?>? Environment = null);

public sealed record TmuxCommandResult(bool Success, string Output, string Stdout, string Stderr, int ExitCode);

public sealed record TmuxRunnerDependencies(
    Func<string, IReadOnlyList<string>, IReadOnlyDictionary<string, string?>?, int?, CancellationToken, Task<ProcessRunResult>>? RunProcessAsync = null);

public static class TmuxRunner
{
    private static readonly Regex TerminalTmuxErrorPattern = new("can't find (pane|session)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static Task<TmuxCommandResult> RunTmuxCommandAsync(
        string tmuxPath,
        IReadOnlyList<string> args,
        RunTmuxOptions? options,
        CancellationToken cancellationToken = default)
        => RunTmuxCommandAsync(tmuxPath, args, options, null, cancellationToken);

    public static IReadOnlyList<string> ResolveTmuxExecutable(string tmuxPath, IReadOnlyDictionary<string, string?>? environment = null)
    {
        if (!CmuxDetect.IsCmuxCompatEnvironment(environment))
        {
            return [tmuxPath];
        }

        var executableName = Path.GetFileName(tmuxPath);
        var cmuxExecutable = string.Equals(executableName, "cmux", StringComparison.Ordinal) ? tmuxPath : "cmux";
        return [cmuxExecutable, "__tmux-compat"];
    }

    public static async Task<TmuxCommandResult> RunTmuxCommandAsync(
        string tmuxPath,
        IReadOnlyList<string> args,
        RunTmuxOptions? options = null,
        TmuxRunnerDependencies? dependencies = null,
        CancellationToken cancellationToken = default)
    {
        var retryCount = Math.Max(0, options?.Retry ?? 0);
        var lastResult = CreateResult(string.Empty, string.Empty, 1);

        for (var attempt = 0; attempt <= retryCount; attempt++)
        {
            var result = await RunTmuxCommandOnceAsync(tmuxPath, args, options, dependencies, cancellationToken);
            lastResult = result;

            if (result.ExitCode == 0)
            {
                return result;
            }

            if (attempt == retryCount || TerminalTmuxErrorPattern.IsMatch(result.Stderr))
            {
                return result;
            }
        }

        return lastResult;
    }

    private static async Task<TmuxCommandResult> RunTmuxCommandOnceAsync(
        string tmuxPath,
        IReadOnlyList<string> args,
        RunTmuxOptions? options,
        TmuxRunnerDependencies? dependencies,
        CancellationToken cancellationToken)
    {
        var environment = options?.Environment ?? TmuxEnvironment.CaptureCurrentEnvironment();
        var executable = ResolveTmuxExecutable(tmuxPath, environment);
        var command = executable.Concat(args).ToArray();
        var runner = dependencies?.RunProcessAsync ?? SpawnProcess.RunAsync;
        var processResult = await runner(command[0], command.Skip(1).ToArray(), environment, options?.TimeoutMs, cancellationToken);

        if (processResult.TimedOut)
        {
            return CreateResult(string.Empty, "timeout", -1);
        }

        return CreateResult(processResult.Stdout.Trim(), processResult.Stderr.Trim(), processResult.ExitCode);
    }

    private static TmuxCommandResult CreateResult(string stdout, string stderr, int exitCode)
        => new(exitCode == 0, stdout, stdout, stderr, exitCode);
}
