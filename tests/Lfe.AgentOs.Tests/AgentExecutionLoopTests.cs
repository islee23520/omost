using Lfe.AgentOs;

namespace Lfe.AgentOs.Tests;

public sealed class AgentExecutionLoopTests
{
    [Fact]
    public void AgentExecutionLoop_CompletesNextRunnableTaskAndAdvancesContinuation()
    {
        var snapshot = new AgentExecutionSnapshot(
            "run-1",
            "plan-1",
            1,
            "worker-1",
            false,
            [
                new AgentExecutionTask("planner-1", AgentRoleKinds.Planner, true, AgentExecutionStatus.Completed, [], "Plan work", [], []),
                new AgentExecutionTask("worker-1", AgentRoleKinds.Worker, true, AgentExecutionStatus.Running, ["planner-1"], "Execute work", [], []),
            ],
            [],
            new DecisionVerdict(DecisionVerdictStatuses.Review, 70, false, ["worker pending"]));

        var next = AgentExecutionTransitions.CompleteTask(snapshot, "worker-1", ["artifact://result"]);

        Assert.Equal(2, next.Version);
        Assert.True(next.Terminal);
        Assert.Null(next.CurrentTaskId);
        Assert.Equal(AgentExecutionStatus.Completed, next.Tasks.Single(task => task.TaskId == "worker-1").Status);
        Assert.Contains("artifact://result", next.Tasks.Single(task => task.TaskId == "worker-1").ProducedArtifacts);
    }

    [Fact]
    public void AgentExecutionLoop_RejectsBackwardTransition()
    {
        var snapshot = SnapshotWithSingleTask(AgentExecutionStatus.Completed, terminal: true);

        var error = Assert.Throws<InvalidOperationException>(
            () => AgentExecutionTransitions.StartTask(snapshot, "worker-1"));

        Assert.Contains("terminal", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AgentExecutionLoop_RejectsTransitionWhenDependencyIsNotComplete()
    {
        var snapshot = new AgentExecutionSnapshot(
            "run-1",
            "plan-1",
            1,
            "worker-1",
            false,
            [
                new AgentExecutionTask("planner-1", AgentRoleKinds.Planner, true, AgentExecutionStatus.Running, [], "Plan work", [], []),
                new AgentExecutionTask("worker-1", AgentRoleKinds.Worker, true, AgentExecutionStatus.Queued, ["planner-1"], "Execute work", [], []),
            ],
            [],
            new DecisionVerdict(DecisionVerdictStatuses.Review, 70, false, ["planner pending"]));

        var error = Assert.Throws<InvalidOperationException>(
            () => AgentExecutionTransitions.StartTask(snapshot, "worker-1"));

        Assert.Contains("dependency", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("planner-1", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AgentExecutionLoop_ContinuationSkipsCompletedTasks()
    {
        var snapshot = new AgentExecutionSnapshot(
            "run-1",
            "plan-1",
            4,
            "worker-main",
            false,
            [
                new AgentExecutionTask("mitigation-1", AgentRoleKinds.Worker, true, AgentExecutionStatus.Completed, [], "Mitigate before worker", ["artifact://mitigation"], []),
                new AgentExecutionTask("worker-main", AgentRoleKinds.Worker, true, AgentExecutionStatus.Queued, ["mitigation-1"], "Execute main work", [], []),
            ],
            [],
            new DecisionVerdict(DecisionVerdictStatuses.Review, 70, false, ["continue main worker"]));

        var continuation = AgentExecutionTransitions.CreateContinuation(snapshot);

        Assert.Equal("run-1", continuation.RunId);
        Assert.Equal("plan-1", continuation.PlanId);
        Assert.Equal("worker-main", continuation.NextTaskId);
        Assert.True(continuation.Accepted);
    }

    private static AgentExecutionSnapshot SnapshotWithSingleTask(string status, bool terminal) =>
        new(
            "run-1",
            "plan-1",
            1,
            terminal ? null : "worker-1",
            terminal,
            [new AgentExecutionTask("worker-1", AgentRoleKinds.Worker, true, status, [], "Execute work", [], [])],
            [],
            new DecisionVerdict(DecisionVerdictStatuses.Pass, 90, false, []));
}
