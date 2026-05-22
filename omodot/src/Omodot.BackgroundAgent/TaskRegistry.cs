namespace Omodot.BackgroundAgent;

public static class TaskRegistry
{
    private static readonly object Sync = new();
    private static readonly Dictionary<string, Func<BackgroundTask>> ActiveTasks = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, BackgroundTask> CompletedTasks = new(StringComparer.Ordinal);
    private static readonly LinkedList<string> CompletedTaskOrder = [];

    public static BackgroundTask CloneRegisteredTask(BackgroundTask task)
    {
        return new BackgroundTask
        {
            Id = task.Id,
            RootSessionId = task.RootSessionId,
            ParentSessionId = task.ParentSessionId,
            ParentMessageId = task.ParentMessageId,
            TeamRunId = task.TeamRunId,
            Description = task.Description,
            Prompt = "[redacted]",
            Agent = task.Agent,
            SpawnDepth = task.SpawnDepth,
            SessionId = task.SessionId,
            Status = task.Status,
            QueuedAt = task.QueuedAt,
            StartedAt = task.StartedAt,
            CompletedAt = task.CompletedAt,
            Result = task.Result,
            Progress = CloneProgress(task.Progress),
            ParentModel = task.ParentModel,
            Model = task.Model,
            FallbackChain = task.FallbackChain,
            AttemptCount = task.AttemptCount,
            ConcurrencyKey = task.ConcurrencyKey,
            ConcurrencyGroup = task.ConcurrencyGroup,
            ParentAgent = task.ParentAgent,
            ParentTools = task.ParentTools is null ? null : new Dictionary<string, bool>(task.ParentTools, StringComparer.Ordinal),
            SkillContent = null,
            SessionPermission = null,
            IsUnstableAgent = task.IsUnstableAgent,
            Error = task.Error,
            Category = task.Category,
            RetryNotification = task.RetryNotification,
            Attempts = task.Attempts?.Select(attempt => attempt with { }).ToList(),
            CurrentAttemptId = task.CurrentAttemptId,
            LastMsgCount = task.LastMsgCount,
            StablePolls = task.StablePolls,
            ConsecutiveMissedPolls = task.ConsecutiveMissedPolls,
        };
    }

    public static void RememberBackgroundTask(BackgroundTask task)
    {
        lock (Sync)
        {
            CompletedTasks.Remove(task.Id);
            RemoveCompletedTaskOrder(task.Id);
            ActiveTasks[task.Id] = () => CloneRegisteredTask(task);
        }
    }

    public static void ArchiveBackgroundTask(BackgroundTask task)
    {
        lock (Sync)
        {
            ActiveTasks.Remove(task.Id);
            CompletedTasks.Remove(task.Id);
            RemoveCompletedTaskOrder(task.Id);
            if (string.IsNullOrEmpty(task.SessionId) || !BackgroundTaskStatuses.Terminal.Contains(task.Status, StringComparer.Ordinal))
            {
                return;
            }

            CompletedTasks[task.Id] = CloneRegisteredTask(task);
            CompletedTaskOrder.AddLast(task.Id);
            TrimCompletedTasks();
        }
    }

    public static BackgroundTask? GetRegisteredBackgroundTask(string taskId)
    {
        lock (Sync)
        {
            if (ActiveTasks.TryGetValue(taskId, out var activeTask))
            {
                return activeTask();
            }

            return CompletedTasks.TryGetValue(taskId, out var completedTask)
                ? CloneRegisteredTask(completedTask)
                : null;
        }
    }

    public static void ForgetBackgroundTask(string taskId)
    {
        lock (Sync)
        {
            ActiveTasks.Remove(taskId);
            CompletedTasks.Remove(taskId);
            RemoveCompletedTaskOrder(taskId);
        }
    }

    public static void ClearBackgroundTaskRegistryForTesting()
    {
        lock (Sync)
        {
            ActiveTasks.Clear();
            CompletedTasks.Clear();
            CompletedTaskOrder.Clear();
        }
    }

    private static TaskProgress? CloneProgress(TaskProgress? progress)
    {
        return progress is null
            ? null
            : progress with
            {
                CountedToolPartIds = progress.CountedToolPartIds is null ? null : new HashSet<string>(progress.CountedToolPartIds, StringComparer.Ordinal),
                ToolCallWindow = progress.ToolCallWindow is null ? null : progress.ToolCallWindow with { },
            };
    }

    private static void TrimCompletedTasks()
    {
        while (CompletedTasks.Count > BackgroundTaskRegistryConstants.MaxCompletedTaskRegistrySize)
        {
            var oldestTaskId = CompletedTaskOrder.First?.Value;
            if (oldestTaskId is null)
            {
                return;
            }

            CompletedTaskOrder.RemoveFirst();
            CompletedTasks.Remove(oldestTaskId);
        }
    }

    private static void RemoveCompletedTaskOrder(string taskId)
    {
        var node = CompletedTaskOrder.Find(taskId);
        if (node is not null)
        {
            CompletedTaskOrder.Remove(node);
        }
    }
}
