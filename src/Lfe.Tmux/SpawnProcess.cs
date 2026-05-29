using System.Diagnostics;

namespace Lfe.Tmux;

public sealed record ProcessRunResult(bool TimedOut, int ExitCode, string Stdout, string Stderr);

public static class SpawnProcess
{
    public static async Task<ProcessRunResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        IReadOnlyDictionary<string, string?>? environment = null,
        int? timeoutMs = null,
        CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        if (environment is not null)
        {
            startInfo.Environment.Clear();
            foreach (var entry in environment)
            {
                if (entry.Value is not null)
                {
                    startInfo.Environment[entry.Key] = entry.Value;
                }
            }
        }

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        if (timeoutMs is int timeout)
        {
            var waitTask = process.WaitForExitAsync(cancellationToken);
            var completedTask = await Task.WhenAny(waitTask, Task.Delay(timeout, cancellationToken));
            if (completedTask != waitTask)
            {
                TryKill(process);
                await IgnoreFailuresAsync(waitTask);
                var timedOutStdout = await SafeReadAsync(stdoutTask);
                var timedOutStderr = await SafeReadAsync(stderrTask);
                return new ProcessRunResult(true, -1, timedOutStdout, timedOutStderr);
            }

            await waitTask;
        }
        else
        {
            await process.WaitForExitAsync(cancellationToken);
        }

        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        return new ProcessRunResult(false, process.ExitCode, stdout, stderr);
    }

    private static async Task IgnoreFailuresAsync(Task task)
    {
        try
        {
            await task;
        }
        catch
        {
        }
    }

    private static async Task<string> SafeReadAsync(Task<string> task)
    {
        try
        {
            return await task;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
        }
    }
}
