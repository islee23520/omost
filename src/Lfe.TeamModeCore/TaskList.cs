using System.Text.Json;

namespace Lfe.TeamModeCore;

public sealed class TaskNotFoundError(string taskId) : Exception($"task '{taskId}' not found");

public sealed class AlreadyClaimedError(string message = "already_claimed") : Exception(message);

public sealed class BlockedByError(IReadOnlyList<string> blockers) : Exception($"blocked by {string.Join(",", blockers)}")
{
    public IReadOnlyList<string> Blockers { get; } = blockers;
}

public sealed class InvalidTaskTransitionError(string currentStatus, string nextStatus) : Exception($"no reverse transitions from {currentStatus} to {nextStatus}");

public sealed class CrossOwnerUpdateError(string message = "cross-owner updates are not allowed") : Exception(message);

public static class TaskList
{
    private static readonly IReadOnlyDictionary<string, string[]> AllowedTransitions = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        ["pending"] = ["claimed", "deleted"],
        ["claimed"] = ["in_progress", "deleted"],
        ["in_progress"] = ["completed", "deleted"],
        ["completed"] = ["deleted"],
        ["deleted"] = [],
    };

    public static TaskListState CreateEmptyTaskListState() => new();

    private static int NextHighWatermark(TaskListState state)
    {
        var maxExistingTaskId = state.Tasks.Aggregate(0, (maxId, task) => int.TryParse(task.Id, out var parsedId) && parsedId > maxId ? parsedId : maxId);
        return Math.Max(state.HighWatermark, maxExistingTaskId) + 1;
    }

    public static TaskMutationResult CreateTaskInState(TaskListState state, CreateTaskInput taskInput, long? now = null)
    {
        var nextTaskId = NextHighWatermark(state);
        var task = TaskSchema.Parse(new TaskItem
        {
            Version = 1,
            Id = nextTaskId.ToString(),
            Subject = taskInput.Subject,
            Description = taskInput.Description,
            ActiveForm = taskInput.ActiveForm,
            Status = taskInput.Status,
            Owner = taskInput.Owner,
            Blocks = taskInput.Blocks,
            BlockedBy = taskInput.BlockedBy,
            Metadata = taskInput.Metadata,
            CreatedAt = now ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            UpdatedAt = now ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            ClaimedAt = taskInput.ClaimedAt,
        });

        return new TaskMutationResult(new TaskListState
        {
            HighWatermark = nextTaskId,
            Tasks = [.. state.Tasks, task],
        }, task);
    }

    public static List<TaskItem> ListTasksInState(TaskListState state, TaskListFilter? filter = null)
    {
        return state.Tasks
            .Where(task => filter?.Status is null || task.Status == filter.Status)
            .Where(task => filter?.Owner is null || task.Owner == filter.Owner)
            .OrderBy(task => int.Parse(task.Id, System.Globalization.CultureInfo.InvariantCulture))
            .ToList();
    }

    public static TaskItem GetTaskFromState(TaskListState state, string taskId)
    {
        return state.Tasks.FirstOrDefault(candidateTask => candidateTask.Id == taskId) ?? throw new TaskNotFoundError(taskId);
    }

    public static bool CanClaim(TaskItem task, IReadOnlyList<TaskItem> allTasks)
    {
        return task.BlockedBy.All(blockerId =>
        {
            var blockerTask = allTasks.FirstOrDefault(candidateTask => candidateTask.Id == blockerId);
            return blockerTask is null || blockerTask.Status == "completed";
        });
    }

    public static List<string> GetBlockingTaskIds(TaskItem task, IReadOnlyList<TaskItem> allTasks)
    {
        return task.BlockedBy.Where(blockerId =>
        {
            var blockerTask = allTasks.FirstOrDefault(candidateTask => candidateTask.Id == blockerId);
            return blockerTask is not null && blockerTask.Status != "completed";
        }).ToList();
    }

    private static TaskListState ReplaceTask(TaskListState state, TaskItem updatedTask)
    {
        return new TaskListState
        {
            HighWatermark = state.HighWatermark,
            Tasks = state.Tasks.Select(task => task.Id == updatedTask.Id ? updatedTask : task).ToList(),
        };
    }

    public static TaskMutationResult ClaimTaskInState(TaskListState state, string taskId, string memberName, long? now = null)
    {
        var task = GetTaskFromState(state, taskId);
        if (task.Status != "pending")
        {
            throw new AlreadyClaimedError();
        }

        if (!CanClaim(task, state.Tasks))
        {
            throw new BlockedByError(GetBlockingTaskIds(task, state.Tasks));
        }

        var timestamp = now ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var updatedTask = TaskSchema.Parse(task with { Status = "claimed", Owner = memberName, ClaimedAt = timestamp, UpdatedAt = timestamp });
        return new TaskMutationResult(ReplaceTask(state, updatedTask), updatedTask);
    }

    private static bool IsValidTransition(string currentStatus, string nextStatus)
    {
        return currentStatus == nextStatus || AllowedTransitions[currentStatus].Contains(nextStatus, StringComparer.Ordinal);
    }

    public static TaskMutationResult UpdateTaskStatusInState(TaskListState state, string taskId, string newStatus, string memberName, long? now = null)
    {
        var task = GetTaskFromState(state, taskId);

        if (task.Status == newStatus)
        {
            return new TaskMutationResult(state, task);
        }

        if (task.Status == "pending" && newStatus == "in_progress")
        {
            var claimed = ClaimTaskInState(state, taskId, memberName, now);
            return UpdateTaskStatusInState(claimed.State, taskId, newStatus, memberName, now);
        }

        if (!IsValidTransition(task.Status, newStatus))
        {
            throw new InvalidTaskTransitionError(task.Status, newStatus);
        }

        if (newStatus != "deleted" && task.Owner != memberName)
        {
            throw new CrossOwnerUpdateError();
        }

        var timestamp = now ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var updatedTask = TaskSchema.Parse(task with { Status = newStatus, UpdatedAt = timestamp });
        return new TaskMutationResult(ReplaceTask(state, updatedTask), updatedTask);
    }
}

public sealed record TaskListFilter
{
    public string? Status { get; init; }

    public string? Owner { get; init; }
}

public sealed record TaskMutationResult(TaskListState State, TaskItem Task);

public static class TaskSchema
{
    public static TaskItem Parse(object? input)
    {
        var result = SafeParse(input);
        if (!result.Success || result.Data is null)
        {
            throw new SchemaValidationException(result.Error?.Message ?? "Invalid task.", result.Error?.Issues ?? []);
        }

        return result.Data;
    }

    public static SafeParseResult<TaskItem> SafeParse(object? input)
    {
        try
        {
            return new(true, ParseTaskDto(input), null);
        }
        catch (SchemaValidationException exception)
        {
            return new(false, default, new ValidationError(exception.Issues, exception.Message));
        }
    }

    private static TaskItem ParseTaskDto(object? input)
    {
        var element = JsonHelpers.ToElement(input);
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new SchemaValidationException("Task must be an object", [new ValidationIssue("<root>", "Task must be an object")]);
        }

        var issues = new List<ValidationIssue>();
        var id = JsonHelpers.GetString(element, "id");
        var subject = JsonHelpers.GetString(element, "subject");
        var description = JsonHelpers.GetString(element, "description");
        var status = JsonHelpers.GetString(element, "status");
        var createdAt = JsonHelpers.GetLong(element, "createdAt");
        var updatedAt = JsonHelpers.GetLong(element, "updatedAt");

        if (string.IsNullOrWhiteSpace(id)) issues.Add(new ValidationIssue("id", "Task id is required."));
        if (string.IsNullOrWhiteSpace(subject)) issues.Add(new ValidationIssue("subject", "Task subject is required."));
        if (string.IsNullOrWhiteSpace(description)) issues.Add(new ValidationIssue("description", "Task description is required."));
        if (string.IsNullOrWhiteSpace(status) || !new[] { "pending", "claimed", "in_progress", "completed", "deleted" }.Contains(status, StringComparer.Ordinal)) issues.Add(new ValidationIssue("status", "Invalid task status."));
        if (createdAt is null or <= 0) issues.Add(new ValidationIssue("createdAt", "createdAt must be positive."));
        if (updatedAt is null or <= 0) issues.Add(new ValidationIssue("updatedAt", "updatedAt must be positive."));

        if (issues.Count > 0)
        {
            throw new SchemaValidationException($"Task validation failed: {issues[0].Message}", issues);
        }

        return new TaskItem
        {
            Version = (int)(JsonHelpers.GetLong(element, "version") ?? 1),
            Id = id!,
            Subject = subject!,
            Description = description!,
            ActiveForm = JsonHelpers.GetString(element, "activeForm"),
            Status = status!,
            Owner = JsonHelpers.GetString(element, "owner"),
            Blocks = JsonHelpers.GetStringList(element, "blocks") ?? [],
            BlockedBy = JsonHelpers.GetStringList(element, "blockedBy") ?? [],
            Metadata = JsonHelpers.GetObjectDictionary(element, "metadata"),
            CreatedAt = createdAt!.Value,
            UpdatedAt = updatedAt!.Value,
            ClaimedAt = JsonHelpers.GetLong(element, "claimedAt"),
        };
    }
}
