namespace Lfe.AgentOs;

public static class AgentExecutionStatus
{
    public const string Queued = "queued";
    public const string Running = "running";
    public const string Completed = "completed";
    public const string Blocked = "blocked";
    public const string Cancelled = "cancelled";
    public const string Failed = "failed";

    public static bool IsTerminal(string status) =>
        status is Completed or Cancelled or Failed;
}

public sealed record AgentExecutionTask(
    string TaskId,
    string Role,
    bool Hidden,
    string Status,
    IReadOnlyList<string> DependsOn,
    string Summary,
    IReadOnlyList<string> ProducedArtifacts,
    IReadOnlyList<string> ReviewIds);

public sealed record AgentExecutionSnapshot(
    string RunId,
    string PlanId,
    int Version,
    string? CurrentTaskId,
    bool Terminal,
    IReadOnlyList<AgentExecutionTask> Tasks,
    IReadOnlyList<AgentReviewSignal> Reviews,
    DecisionVerdict Decision);

public sealed record AgentExecutionDispatchRequest(
    string SessionId,
    string RunId,
    string TaskId,
    string Role,
    bool Hidden,
    string Message,
    IReadOnlyList<SharedSkillReference> SharedSkills,
    IReadOnlyList<LazyCapabilityRequest> LazyCapabilities,
    string? ContinuationToken = null);

public sealed record AgentExecutionDispatchReceipt(
    bool Accepted,
    string SessionId,
    string DispatchId,
    string TaskId,
    IReadOnlyList<string> ArtifactReferences,
    string? ContinuationToken = null);

public static class AgentExecutionTransitions
{
    public static AgentExecutionSnapshot StartTask(AgentExecutionSnapshot snapshot, string taskId)
    {
        EnsureNotTerminal(snapshot);

        var task = FindTask(snapshot, taskId);
        if (task.Status != AgentExecutionStatus.Queued)
            throw new InvalidOperationException($"Task '{taskId}' cannot start from status '{task.Status}'.");

        var incompleteDependency = task.DependsOn.FirstOrDefault(
            dependencyId => FindTask(snapshot, dependencyId).Status != AgentExecutionStatus.Completed);
        if (incompleteDependency is not null)
            throw new InvalidOperationException($"Task '{taskId}' cannot start before dependency '{incompleteDependency}' is completed.");

        return snapshot with
        {
            Version = snapshot.Version + 1,
            CurrentTaskId = taskId,
            Tasks = ReplaceTask(snapshot.Tasks, task with { Status = AgentExecutionStatus.Running }),
        };
    }

    public static AgentExecutionSnapshot CompleteTask(
        AgentExecutionSnapshot snapshot,
        string taskId,
        IReadOnlyList<string> producedArtifacts)
    {
        EnsureNotTerminal(snapshot);

        var task = FindTask(snapshot, taskId);
        if (task.Status != AgentExecutionStatus.Running)
            throw new InvalidOperationException($"Task '{taskId}' cannot complete from status '{task.Status}'.");

        var tasks = ReplaceTask(snapshot.Tasks, task with
        {
            Status = AgentExecutionStatus.Completed,
            ProducedArtifacts = producedArtifacts,
        });
        var nextTask = FindNextRunnable(tasks);

        return snapshot with
        {
            Version = snapshot.Version + 1,
            CurrentTaskId = nextTask?.TaskId,
            Terminal = nextTask is null && tasks.All(task => task.Status == AgentExecutionStatus.Completed),
            Tasks = tasks,
        };
    }

    public static AgentContinuationState CreateContinuation(AgentExecutionSnapshot snapshot)
    {
        if (snapshot.Terminal)
            throw new InvalidOperationException($"Run '{snapshot.RunId}' is terminal and cannot create a continuation.");

        var nextTask = FindNextRunnable(snapshot.Tasks)
            ?? throw new InvalidOperationException($"Run '{snapshot.RunId}' has no runnable continuation task.");

        return new AgentContinuationState(snapshot.RunId, snapshot.PlanId, nextTask.TaskId, true);
    }

    private static void EnsureNotTerminal(AgentExecutionSnapshot snapshot)
    {
        if (snapshot.Terminal)
            throw new InvalidOperationException($"Run '{snapshot.RunId}' is terminal and cannot transition.");
    }

    private static AgentExecutionTask FindTask(AgentExecutionSnapshot snapshot, string taskId) =>
        FindTask(snapshot.Tasks, taskId);

    private static AgentExecutionTask FindTask(IReadOnlyList<AgentExecutionTask> tasks, string taskId) =>
        tasks.FirstOrDefault(task => task.TaskId == taskId)
        ?? throw new InvalidOperationException($"Task '{taskId}' was not found.");

    private static IReadOnlyList<AgentExecutionTask> ReplaceTask(
        IReadOnlyList<AgentExecutionTask> tasks,
        AgentExecutionTask replacement) =>
        tasks.Select(task => task.TaskId == replacement.TaskId ? replacement : task).ToArray();

    private static AgentExecutionTask? FindNextRunnable(IReadOnlyList<AgentExecutionTask> tasks) =>
        tasks.FirstOrDefault(task =>
            task.Status == AgentExecutionStatus.Queued
            && task.DependsOn.All(dependencyId => FindTask(tasks, dependencyId).Status == AgentExecutionStatus.Completed));
}
