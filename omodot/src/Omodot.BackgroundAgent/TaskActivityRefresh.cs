namespace Omodot.BackgroundAgent;

public abstract record TaskActivityRefreshResult
{
    public sealed record Activity(long ActivityTime) : TaskActivityRefreshResult;
    public sealed record Missing : TaskActivityRefreshResult;
    public sealed record Unavailable : TaskActivityRefreshResult;
}

public static class TaskActivityRefresh
{
    public static TaskActivityRefreshResult UpdateTaskActivityFromLookup(BackgroundTask task, SessionActivityLookup lookup)
    {
        if (lookup is not SessionActivityLookup.Activity activity)
        {
            return lookup switch
            {
                SessionActivityLookup.Missing => new TaskActivityRefreshResult.Missing(),
                SessionActivityLookup.Unavailable => new TaskActivityRefreshResult.Unavailable(),
                _ => new TaskActivityRefreshResult.Missing(),
            };
        }

        var activityTime = new DateTimeOffset(activity.Value).ToUnixTimeMilliseconds();
        var baseline = task.Progress is { } progress && progress.LastUpdate != default
            ? new DateTimeOffset(progress.LastUpdate).ToUnixTimeMilliseconds()
            : task.StartedAt is null ? (long?)null : new DateTimeOffset(task.StartedAt.Value).ToUnixTimeMilliseconds();

        if (baseline is not null && activityTime <= baseline.Value)
        {
            return new TaskActivityRefreshResult.Activity(activityTime);
        }

        task.Progress ??= new TaskProgress { ToolCalls = 0, LastUpdate = activity.Value };
        task.Progress.LastUpdate = activity.Value;
        return new TaskActivityRefreshResult.Activity(activityTime);
    }

    public static async Task<TaskActivityRefreshResult> RefreshTaskActivityFromSession(
        BackgroundTask task,
        SessionActivityResolver getSessionActivity,
        Action<BackgroundTask, Exception>? onError = null)
    {
        if (string.IsNullOrEmpty(task.SessionId))
        {
            return new TaskActivityRefreshResult.Missing();
        }

        try
        {
            var lookup = await getSessionActivity(task.SessionId).ConfigureAwait(false);
            return UpdateTaskActivityFromLookup(task, lookup);
        }
        catch (Exception exception)
        {
            onError?.Invoke(task, exception);
            return new TaskActivityRefreshResult.Unavailable();
        }
    }
}
