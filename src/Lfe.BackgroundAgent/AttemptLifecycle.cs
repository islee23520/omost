namespace Lfe.BackgroundAgent;

public static class AttemptLifecycle
{
    public static BackgroundTaskAttempt? GetCurrentAttempt(BackgroundTask task)
    {
        return task.CurrentAttemptId is null ? null : GetAttempt(task, task.CurrentAttemptId);
    }

    public static BackgroundTaskAttempt EnsureCurrentAttempt(BackgroundTask task, DelegatedModelConfig? model = null)
    {
        model ??= task.Model;
        var existing = GetCurrentAttempt(task);
        if (existing is not null)
        {
            return existing;
        }

        var attempt = new BackgroundTaskAttempt
        {
            AttemptId = CreateAttemptId(),
            AttemptNumber = (task.Attempts?.Count ?? 0) + 1,
            SessionId = task.SessionId,
            ProviderId = model?.ProviderId,
            ModelId = model?.ModelId,
            Variant = model?.Variant,
            Status = task.Status,
            Error = task.Error,
            StartedAt = task.StartedAt,
            CompletedAt = task.CompletedAt,
        };

        task.Attempts ??= [];
        task.Attempts.Add(attempt);
        task.CurrentAttemptId = attempt.AttemptId;
        return attempt;
    }

    public static BackgroundTask ProjectTaskFromCurrentAttempt(BackgroundTask task)
    {
        var attempt = GetCurrentAttempt(task);
        if (attempt is null)
        {
            return task;
        }

        task.Status = attempt.Status;
        task.SessionId = attempt.SessionId;
        task.StartedAt = attempt.StartedAt;
        task.CompletedAt = attempt.CompletedAt;
        task.Error = attempt.Error;
        task.Model = attempt.ProviderId is not null && attempt.ModelId is not null
            ? new DelegatedModelConfig { ProviderId = attempt.ProviderId, ModelId = attempt.ModelId, Variant = attempt.Variant }
            : null;
        return task;
    }

    public static BackgroundTaskAttempt StartAttempt(BackgroundTask task, DelegatedModelConfig? model)
    {
        var attempt = new BackgroundTaskAttempt
        {
            AttemptId = CreateAttemptId(),
            AttemptNumber = (task.Attempts?.Count ?? 0) + 1,
            ProviderId = model?.ProviderId,
            ModelId = model?.ModelId,
            Variant = model?.Variant,
            Status = BackgroundTaskStatuses.Pending,
        };

        task.Attempts ??= [];
        task.Attempts.Add(attempt);
        task.CurrentAttemptId = attempt.AttemptId;
        task.Status = BackgroundTaskStatuses.Pending;
        task.SessionId = null;
        task.StartedAt = null;
        task.CompletedAt = null;
        task.Error = null;
        task.Model = model;
        return attempt;
    }

    public static BackgroundTaskAttempt? BindAttemptSession(BackgroundTask task, string attemptId, string sessionId, DelegatedModelConfig? model)
    {
        EnsureCurrentAttempt(task, model);
        if (!string.Equals(task.CurrentAttemptId, attemptId, StringComparison.Ordinal))
        {
            return null;
        }

        var attempt = GetAttempt(task, attemptId);
        if (attempt is null || BackgroundTaskStatuses.Terminal.Contains(attempt.Status, StringComparer.Ordinal))
        {
            return null;
        }

        attempt.SessionId = sessionId;
        attempt.Status = BackgroundTaskStatuses.Running;
        attempt.StartedAt = DateTime.UtcNow;
        attempt.CompletedAt = null;
        attempt.Error = null;
        attempt.ProviderId = model?.ProviderId ?? attempt.ProviderId;
        attempt.ModelId = model?.ModelId ?? attempt.ModelId;
        attempt.Variant = model?.Variant ?? attempt.Variant;

        ProjectTaskFromCurrentAttempt(task);
        return GetCurrentAttempt(task);
    }

    public static BackgroundTaskAttempt? FinalizeAttempt(BackgroundTask task, string attemptId, string status, string? error = null)
    {
        var attempt = GetAttempt(task, attemptId);
        if (attempt is null)
        {
            return null;
        }

        attempt.Status = status;
        attempt.CompletedAt = DateTime.UtcNow;
        attempt.Error = error;
        if (string.Equals(task.CurrentAttemptId, attemptId, StringComparison.Ordinal))
        {
            ProjectTaskFromCurrentAttempt(task);
        }

        return attempt;
    }

    public static BackgroundTaskAttempt? ScheduleRetryAttempt(BackgroundTask task, string failedAttemptId, DelegatedModelConfig nextModel, string? error = null)
    {
        var failedAttempt = FinalizeAttempt(task, failedAttemptId, BackgroundTaskStatuses.Error, error);
        if (failedAttempt is null || !string.Equals(task.CurrentAttemptId, failedAttemptId, StringComparison.Ordinal))
        {
            return null;
        }

        return StartAttempt(task, nextModel);
    }

    public static BackgroundTaskAttempt? FindAttemptBySession(BackgroundTask task, string sessionId)
    {
        return task.Attempts?.FirstOrDefault(attempt => string.Equals(attempt.SessionId, sessionId, StringComparison.Ordinal));
    }

    private static BackgroundTaskAttempt? GetAttempt(BackgroundTask task, string attemptId)
    {
        return task.Attempts?.FirstOrDefault(attempt => string.Equals(attempt.AttemptId, attemptId, StringComparison.Ordinal));
    }

    private static string CreateAttemptId()
    {
        return $"att_{Guid.NewGuid():N}"[..12];
    }
}
