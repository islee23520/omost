namespace Omodot.BackgroundAgent;

public sealed record SessionStatusEntry
{
    public string Type { get; init; } = string.Empty;
}

public sealed record PruneStaleTasksArgs
{
    public required IDictionary<string, BackgroundTask> Tasks { get; init; }
    public required IDictionary<string, List<BackgroundTask>> Notifications { get; init; }
    public required Action<string, BackgroundTask, string> OnTaskPruned { get; init; }
    public Action<string, BackgroundTask>? OnTerminalTaskRemoved { get; init; }
    public int? TaskTtlMs { get; init; }
    public DateTime? Now { get; init; }
    public IReadOnlyDictionary<string, SessionStatusEntry>? SessionStatuses { get; init; }
}

public static class TaskPoller
{
    public static void PruneStaleTasksAndNotifications(PruneStaleTasksArgs args)
    {
        var effectiveTtl = args.TaskTtlMs ?? BackgroundAgentConstants.TaskTtlMs;
        var now = args.Now ?? DateTime.UtcNow;
        var tasksWithPendingNotifications = args.Notifications.Values
            .SelectMany(task => task)
            .Select(task => task.Id)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var item in args.Tasks.ToArray())
        {
            var taskId = item.Key;
            var task = item.Value;

            if (BackgroundTaskStatuses.Terminal.Contains(task.Status, StringComparer.Ordinal))
            {
                if (tasksWithPendingNotifications.Contains(taskId) || task.CompletedAt is null)
                {
                    continue;
                }

                if ((now - task.CompletedAt.Value).TotalMilliseconds <= BackgroundAgentConstants.TerminalTaskTtlMs)
                {
                    continue;
                }

                args.Tasks.Remove(taskId);
                args.OnTerminalTaskRemoved?.Invoke(taskId, task);
                continue;
            }

            if (!string.IsNullOrEmpty(task.TeamRunId))
            {
                continue;
            }

            var sessionStatus = task.SessionId is not null && args.SessionStatuses is not null && args.SessionStatuses.TryGetValue(task.SessionId, out var entry)
                ? entry.Type
                : null;
            if (string.Equals(task.Status, BackgroundTaskStatuses.Running, StringComparison.Ordinal) &&
                sessionStatus is not null &&
                SessionStatusClassifier.IsActiveSessionStatus(sessionStatus))
            {
                continue;
            }

            var timestamp = string.Equals(task.Status, BackgroundTaskStatuses.Pending, StringComparison.Ordinal)
                ? task.QueuedAt
                : task.Progress?.LastUpdate ?? task.StartedAt;
            if (timestamp is null || (now - timestamp.Value).TotalMilliseconds <= effectiveTtl)
            {
                continue;
            }

            var ttlMinutes = (int)Math.Round(effectiveTtl / 60000d);
            var errorMessage = string.Equals(task.Status, BackgroundTaskStatuses.Pending, StringComparison.Ordinal)
                ? $"Task timed out while queued ({ttlMinutes} minutes)"
                : $"Task timed out after {ttlMinutes} minutes of inactivity";
            args.OnTaskPruned(taskId, task, errorMessage);
        }

        foreach (var notification in args.Notifications.ToArray())
        {
            if (notification.Value.Count == 0)
            {
                args.Notifications.Remove(notification.Key);
                continue;
            }

            var validNotifications = notification.Value
                .Where(task => task.StartedAt is not null && (now - task.StartedAt.Value).TotalMilliseconds <= effectiveTtl)
                .ToList();

            if (validNotifications.Count == 0)
            {
                args.Notifications.Remove(notification.Key);
            }
            else if (validNotifications.Count != notification.Value.Count)
            {
                args.Notifications[notification.Key] = validNotifications;
            }
        }
    }
}
