namespace Omodot.BackgroundAgent;

public sealed record BackgroundTaskNotificationTask
{
    public string Id { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Status { get; init; } = BackgroundTaskStatuses.Pending;
    public string? Error { get; init; }
    public IReadOnlyList<BackgroundTaskAttempt>? Attempts { get; init; }
}

public static class BackgroundTaskNotificationTemplate
{
    public static string BuildBackgroundTaskNotificationText(
        BackgroundTaskNotificationTask task,
        string duration,
        string statusText,
        bool allComplete,
        int remainingCount,
        IReadOnlyList<BackgroundTaskNotificationTask> completedTasks)
    {
        var errorInfo = string.IsNullOrEmpty(task.Error) ? string.Empty : $"\n**Error:** {task.Error}";
        if (allComplete)
        {
            var succeeded = completedTasks.Where(item => string.Equals(item.Status, BackgroundTaskStatuses.Completed, StringComparison.Ordinal)).ToArray();
            var failed = completedTasks.Where(item => !string.Equals(item.Status, BackgroundTaskStatuses.Completed, StringComparison.Ordinal)).ToArray();
            var header = failed.Length > 0
                ? $"[ALL BACKGROUND TASKS FINISHED - {failed.Length} FAILED]"
                : "[ALL BACKGROUND TASKS COMPLETE]";

            var body = string.Empty;
            if (succeeded.Length > 0)
            {
                body += $"**Completed:**\n{string.Join("\n", succeeded.Select(FormatTaskSummaryLine))}\n";
            }

            if (failed.Length > 0)
            {
                body += $"\n**Failed:**\n{string.Join("\n", failed.Select(FormatTaskSummaryLine))}\n";
            }

            if (string.IsNullOrEmpty(body))
            {
                body = $"{FormatTaskSummaryLine(task)}\n";
            }

            var actionRequired = failed.Length > 0
                ? $"\n\n**ACTION REQUIRED:** {failed.Length} task(s) failed. Check errors above and decide whether to retry or proceed."
                : string.Empty;

            return $"<system-reminder>\n{header}\n\n{body.Trim()}\n\nUse `background_output(task_id=\"<id>\")` to retrieve each result.{actionRequired}\n</system-reminder>";
        }

        var action = string.Equals(statusText, "COMPLETED", StringComparison.Ordinal)
            ? "Do NOT poll - continue productive work."
            : "**ACTION REQUIRED:** This task failed. Check the error and decide whether to retry, cancel remaining tasks, or continue.";

        return $"<system-reminder>\n[BACKGROUND TASK {statusText}]\n**ID:** `{task.Id}`\n**Description:** {SafeDescription(task)}\n**Duration:** {duration}{errorInfo}\n\n**{remainingCount} task{(remainingCount == 1 ? string.Empty : "s")} still in progress.** You WILL be notified when ALL complete.\n{action}\n\nUse `background_output(task_id=\"{task.Id}\")` to retrieve this result when ready.\n</system-reminder>";
    }

    private static string SafeDescription(BackgroundTaskNotificationTask task)
    {
        return string.IsNullOrEmpty(task.Description) ? task.Id : task.Description;
    }

    private static string FormatTaskSummaryLine(BackgroundTaskNotificationTask task)
    {
        var baseLine = $"- `{task.Id}`: {SafeDescription(task)}";
        var statusSuffix = string.Equals(task.Status, BackgroundTaskStatuses.Completed, StringComparison.Ordinal)
            ? string.Empty
            : $" [{task.Status.ToUpperInvariant()}]{(string.IsNullOrEmpty(task.Error) ? string.Empty : $" - {task.Error}")}";
        var timeline = FormatAttemptTimeline(task);
        return $"{baseLine}{statusSuffix}{(string.IsNullOrEmpty(timeline) ? string.Empty : $"\n{timeline}")}";
    }

    private static string FormatAttemptTimeline(BackgroundTaskNotificationTask task)
    {
        if (task.Attempts is null || task.Attempts.Count <= 1)
        {
            return string.Empty;
        }

        var lines = task.Attempts.Select(attempt =>
        {
            var attemptLines = new List<string>
            {
                $"  - Attempt {attempt.AttemptNumber} — {attempt.Status.ToUpperInvariant()} — {FormatAttemptModel(attempt)} — {attempt.SessionId ?? "unknown"}",
            };

            if (!string.Equals(attempt.Status, BackgroundTaskStatuses.Completed, StringComparison.Ordinal) && !string.IsNullOrEmpty(attempt.Error))
            {
                attemptLines.Add($"    Error: {attempt.Error}");
            }

            return string.Join("\n", attemptLines);
        });

        return $"Background task attempts:\n{string.Join("\n", lines)}";
    }

    private static string FormatAttemptModel(BackgroundTaskAttempt attempt)
    {
        if (!string.IsNullOrEmpty(attempt.ProviderId) && !string.IsNullOrEmpty(attempt.ModelId))
        {
            return $"{attempt.ProviderId}/{attempt.ModelId}";
        }

        if (!string.IsNullOrEmpty(attempt.ModelId))
        {
            return attempt.ModelId;
        }

        if (!string.IsNullOrEmpty(attempt.ProviderId))
        {
            return attempt.ProviderId;
        }

        return "unknown-model";
    }
}
