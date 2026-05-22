using System.Diagnostics;
using System.Text;

namespace Omodot.CommandExecutor;

internal sealed record ProcessExecutionRequest(
    string FileName,
    IReadOnlyList<string> Arguments,
    string? WorkingDirectory = null,
    IReadOnlyDictionary<string, string?>? EnvironmentVariables = null,
    string? StandardInput = null,
    int? TimeoutMs = null);

internal static class ProcessExecution
{
    public static async Task<CommandResult> RunAsync(ProcessExecutionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var process = new Process
        {
            StartInfo = CreateStartInfo(request),
            EnableRaisingEvents = true,
        };

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        var stdoutClosed = CreateCompletionSource();
        var stderrClosed = CreateCompletionSource();

        process.OutputDataReceived += (_, args) => AppendData(stdout, stdoutClosed, args.Data);
        process.ErrorDataReceived += (_, args) => AppendData(stderr, stderrClosed, args.Data);

        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            return new CommandResult(1, StdinTrimOrNull(stdout), ex.Message);
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        if (request.StandardInput is not null)
        {
            await process.StandardInput.WriteAsync(request.StandardInput.AsMemory(), cancellationToken).ConfigureAwait(false);
            await process.StandardInput.FlushAsync(cancellationToken).ConfigureAwait(false);
            process.StandardInput.Close();
        }

        var timedOut = false;
        CancellationTokenSource? timeoutCts = null;
        CancellationTokenSource? linkedCts = null;

        try
        {
            if (request.TimeoutMs is int timeoutMs)
            {
                timeoutCts = new CancellationTokenSource(timeoutMs);
                linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
                await process.WaitForExitAsync(linkedCts.Token).ConfigureAwait(false);
            }
            else
            {
                await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (timeoutCts?.IsCancellationRequested == true)
        {
            timedOut = true;
            KillProcess(process);
            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
            KillProcess(process);
            throw;
        }
        finally
        {
            linkedCts?.Dispose();
            timeoutCts?.Dispose();
        }

        await Task.WhenAll(stdoutClosed.Task, stderrClosed.Task).ConfigureAwait(false);

        var trimmedStdout = StdinTrimOrNull(stdout);
        var trimmedStderr = StdinTrimOrNull(stderr);
        if (timedOut && request.TimeoutMs is int timeout)
        {
            trimmedStderr = string.IsNullOrEmpty(trimmedStderr)
                ? $"Hook command timed out after {timeout}ms"
                : $"{trimmedStderr}{Environment.NewLine}Hook command timed out after {timeout}ms";
        }

        return new CommandResult(process.ExitCode, trimmedStdout, trimmedStderr);
    }

    private static ProcessStartInfo CreateStartInfo(ProcessExecutionRequest request)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = request.FileName,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = request.StandardInput is not null,
        };

        if (!string.IsNullOrWhiteSpace(request.WorkingDirectory))
        {
            startInfo.WorkingDirectory = request.WorkingDirectory;
        }

        foreach (var argument in request.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        if (request.EnvironmentVariables is not null)
        {
            startInfo.Environment.Clear();
            foreach (var entry in request.EnvironmentVariables)
            {
                if (entry.Value is not null)
                {
                    startInfo.Environment[entry.Key] = entry.Value;
                }
            }
        }

        return startInfo;
    }

    private static TaskCompletionSource CreateCompletionSource()
    {
        return new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private static void AppendData(StringBuilder builder, TaskCompletionSource closed, string? data)
    {
        if (data is null)
        {
            closed.TrySetResult();
            return;
        }

        if (builder.Length > 0)
        {
            builder.AppendLine();
        }

        builder.Append(data);
    }

    private static void KillProcess(Process process)
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

    private static string? StdinTrimOrNull(StringBuilder builder)
    {
        var value = builder.ToString().Trim();
        return value.Length == 0 ? null : value;
    }
}
