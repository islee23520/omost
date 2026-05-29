namespace Lfe.SearchTools;

public static class SearchProcessOutputCollector
{
    public static async Task<SearchProcessOutput> CollectSearchProcessOutputAsync(
        SpawnedProcess process,
        int timeoutMs,
        string timeoutMessage)
    {
        ArgumentNullException.ThrowIfNull(process);

        using var timeout = new CancellationTokenSource(timeoutMs);
        using var _ = timeout.Token.Register(process.Kill);

        var stdoutTask = ProcessStreamReader.ReadProcessStreamAsync(process.Stdout);
        var stderrTask = ReadStderrAsync(process.Stderr);
        var exitTask = process.Exited;

        try
        {
            await Task.WhenAll(stdoutTask, stderrTask, exitTask).WaitAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            process.Kill();
            throw new TimeoutException(timeoutMessage);
        }

        return new SearchProcessOutput(
            await stdoutTask.ConfigureAwait(false),
            await stderrTask.ConfigureAwait(false),
            await exitTask.ConfigureAwait(false));
    }

    private static async Task<string> ReadStderrAsync(ProcessReadableStream? stream)
    {
        try
        {
            return await ProcessStreamReader.ReadProcessStreamAsync(stream).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }
}
