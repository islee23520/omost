namespace Lfe.BackgroundAgent;

public interface ITaskSessionReader
{
    BackgroundTaskSessionSnapshot? GetTask(string taskId);
}

public sealed record BackgroundTaskSessionSnapshot
{
    public string? SessionId { get; init; }
    public string? Status { get; init; }
}

public sealed record WaitForTaskSessionIdOptions
{
    public int? TimeoutMs { get; init; }
    public int? IntervalMs { get; init; }
    public CancellationToken CancellationToken { get; init; }
}

public static class WaitForTaskSession
{
    public static async Task<string?> WaitForTaskSessionIdAsync(ITaskSessionReader manager, string taskId, WaitForTaskSessionIdOptions? options = null)
    {
        options ??= new WaitForTaskSessionIdOptions();
        var timeoutMs = options.TimeoutMs ?? BackgroundAgentConstants.DefaultWaitForSessionTimeoutMs;
        var intervalMs = options.IntervalMs ?? BackgroundAgentConstants.DefaultWaitForSessionIntervalMs;
        if (options.CancellationToken.IsCancellationRequested)
        {
            return null;
        }

        var initialTask = manager.GetTask(taskId);
        if (!string.IsNullOrEmpty(initialTask?.SessionId))
        {
            return initialTask.SessionId;
        }

        if (IsTerminalStatus(initialTask?.Status))
        {
            return null;
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(options.CancellationToken);
        timeoutCts.CancelAfter(timeoutMs);

        try
        {
            while (!timeoutCts.Token.IsCancellationRequested)
            {
                await Task.Delay(intervalMs, timeoutCts.Token).ConfigureAwait(false);
                var task = manager.GetTask(taskId);
                if (!string.IsNullOrEmpty(task?.SessionId))
                {
                    return task.SessionId;
                }

                if (IsTerminalStatus(task?.Status))
                {
                    return null;
                }
            }
        }
        catch (OperationCanceledException)
        {
        }

        return null;
    }

    private static bool IsTerminalStatus(string? status)
    {
        return status is BackgroundTaskStatuses.Error or BackgroundTaskStatuses.Cancelled or BackgroundTaskStatuses.Interrupt;
    }
}
