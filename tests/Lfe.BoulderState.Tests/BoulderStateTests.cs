using Lfe.BoulderState;
using Lfe.BoulderState.Storage;

namespace Lfe.BoulderState.Tests;

public sealed class BoulderStateTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), $"lfe-boulder-state-{Guid.NewGuid():N}");

    public BoulderStateTests()
    {
        Directory.CreateDirectory(_tempDirectory);
    }

    [Fact]
    public void Constants_are_stable()
    {
        Assert.Equal(".omo/boulder", Constants.BOULDER_DIR);
        Assert.Equal("state.json", Constants.BOULDER_FILE);
        Assert.Equal(".omo/boulder/state.json", Constants.BOULDER_STATE_PATH);
        Assert.Equal(".omo/plans", Constants.PROMETHEUS_PLANS_DIR);
    }

    [Fact]
    public void Types_can_be_created_and_round_tripped()
    {
        var state = new BoulderState
        {
            schema_version = 2,
            active_work_id = "work-1",
            active_plan = "plan.md",
            started_at = "2026-01-01T00:00:00.000Z",
            status = BoulderWorkStatus.active,
            updated_at = "2026-01-01T00:00:01.000Z",
            session_ids = ["s1"],
            session_origins = new Dictionary<string, BoulderSessionOrigin> { ["s1"] = BoulderSessionOrigin.direct },
            plan_name = "plan",
            works = new Dictionary<string, BoulderWorkState>
            {
                ["work-1"] = new BoulderWorkState
                {
                    work_id = "work-1",
                    active_plan = "plan.md",
                    plan_name = "plan",
                    started_at = "2026-01-01T00:00:00.000Z",
                    session_ids = ["s1"],
                },
            },
        };

        var json = System.Text.Json.JsonSerializer.Serialize(state, Shared.JsonOptions);
        var read = ReadState.ReadBoulderState(CreateStateDirectory(json));

        Assert.NotNull(read);
        Assert.Equal("work-1", read!.active_work_id);
        Assert.Equal("plan", read.plan_name);
        Assert.Equal(BoulderSessionOrigin.direct, read.session_origins!["s1"]);
    }

    [Fact]
    public void ReadCurrentTopLevelTask_parses_first_top_level_task()
    {
        var planPath = Path.Combine(_tempDirectory, "plan.md");
        File.WriteAllText(planPath, "## Intro\n- [ ] 1. ignore\n## TODOs\n  - [ ] 1. nested\n- [x] 1. done\n- [ ] 2. Build feature\n## Final Verification Wave\n- [ ] F1. Verify\n");

        var task = TopLevelTask.ReadCurrentTopLevelTask(planPath);

        Assert.NotNull(task);
        Assert.Equal("todo:2", task!.key);
        Assert.Equal("todo", task.section);
        Assert.Equal("2", task.label);
        Assert.Equal("Build feature", task.title);
    }

    [Fact]
    public void PlanProgress_supports_structured_and_simple_plans()
    {
        var plansDir = Path.Combine(_tempDirectory, Constants.PROMETHEUS_PLANS_DIR.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(plansDir);

        var older = Path.Combine(plansDir, "older.md");
        var newer = Path.Combine(plansDir, "newer.md");
        File.WriteAllText(older, "- [x] done\n- [ ] todo\n");
        File.WriteAllText(newer, "## TODOs\n- [x] 1. First\n- [ ] 2. Second\n## Final Verification Wave\n- [X] F1. Verify\n- [ ] F2. Finish\n");
        File.SetLastWriteTimeUtc(older, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        File.SetLastWriteTimeUtc(newer, new DateTime(2026, 1, 1, 0, 1, 0, DateTimeKind.Utc));

        var plans = PlanProgressHelper.FindPrometheusPlans(_tempDirectory);

        Assert.Equal([newer, older], plans);
        Assert.Equal("newer", PlanProgressHelper.GetPlanName(newer));
        Assert.Equal(new PlanProgress { total = 2, completed = 1, isComplete = false }, PlanProgressHelper.GetPlanProgress(older));
        Assert.Equal(new PlanProgress { total = 4, completed = 2, isComplete = false }, PlanProgressHelper.GetPlanProgress(newer));
    }

    [Fact]
    public void Read_write_and_complete_state_workflow_passes()
    {
        var planPath = CreatePlan("alpha");
        var state = WriteState.CreateBoulderState(planPath, "session-1", "agent-a", "worktree");

        Assert.True(WriteState.WriteBoulderState(_tempDirectory, state));

        var read = ReadState.ReadBoulderState(_tempDirectory);
        Assert.NotNull(read);
        Assert.Equal(state.active_work_id, read!.active_work_id);
        Assert.Equal(planPath, read.active_plan);

        var completed = WriteState.CompleteBoulder(_tempDirectory, state.active_work_id, "2026-01-01T00:00:02.000Z");
        Assert.NotNull(completed);
        Assert.Equal(BoulderWorkStatus.completed, completed!.status);
        Assert.Empty(ReadState.GetActiveWorks(_tempDirectory));
    }

    [Fact]
    public void Session_management_tracks_origins_and_work_lookup()
    {
        var planA = CreatePlan("plan-a");
        var planB = CreatePlan("plan-b");
        var state = WriteState.CreateBoulderState(planA, "session-a");
        Assert.True(WriteState.WriteBoulderState(_tempDirectory, state));
        var firstWorkId = state.active_work_id!;

        var added = WriteState.AddBoulderWork(_tempDirectory, planB, "session-b", worktreePath: _tempDirectory, startedAt: "2026-01-01T00:00:00.000Z");
        Assert.NotNull(added);
        var secondWorkId = added!.active_work_id!;

        var appended = SessionStorage.AppendSessionId(_tempDirectory, "session-c", BoulderSessionOrigin.appended);
        Assert.NotNull(appended);
        Assert.Contains("session-c", appended!.session_ids);
        Assert.Equal(BoulderSessionOrigin.appended, appended.session_origins!["session-c"]);

        Assert.Equal(secondWorkId, ReadState.GetWorkForSession(_tempDirectory, "session-c")!.work_id);
        Assert.Equal(secondWorkId, ReadState.GetWorkByPlanName(_tempDirectory, "plan-b", _tempDirectory)!.work_id);

        var selected = WriteState.SelectActiveWork(_tempDirectory, firstWorkId);
        Assert.NotNull(selected);
        Assert.Equal(firstWorkId, selected!.active_work_id);
    }

    [Fact]
    public void Task_timers_and_reserved_keys_behave_correctly()
    {
        var planPath = CreatePlan("tasks");
        var state = WriteState.CreateBoulderState(planPath, "session-1");
        Assert.True(WriteState.WriteBoulderState(_tempDirectory, state));
        var workId = state.active_work_id!;

        Assert.Null(TaskSessionStorage.UpsertTaskSessionStateForWork(_tempDirectory, workId, "__proto__", "bad", "Bad", "session-task"));

        var upserted = TaskSessionStorage.UpsertTaskSessionState(_tempDirectory, "todo:1", "1", "Build", "session-task", category: "quick");
        Assert.NotNull(upserted);
        Assert.Equal("quick", ReadState.GetTaskSessionState(_tempDirectory, "todo:1")!.category);

        var started = TaskSessionStorage.StartTaskTimer(_tempDirectory, workId, "todo:1", "1", "Build", "session-task", startedAt: "2026-01-01T00:00:00.000Z");
        Assert.NotNull(started);
        Assert.Equal("2026-01-01T00:00:00.000Z", started!.works![workId].task_sessions!["todo:1"].started_at);

        var restarted = TaskSessionStorage.StartTaskTimer(_tempDirectory, workId, "todo:1", "1", "Build", "session-task", startedAt: "2026-01-01T00:00:05.000Z");
        Assert.Equal("2026-01-01T00:00:00.000Z", restarted!.works![workId].task_sessions!["todo:1"].started_at);

        var ended = TaskSessionStorage.EndTaskTimer(_tempDirectory, workId, "todo:1", "2026-01-01T00:00:02.000Z");
        Assert.NotNull(ended);
        Assert.Equal(BoulderTaskStatus.completed, ended!.works![workId].task_sessions!["todo:1"].status);
        Assert.Equal(2000, ended.works[workId].task_sessions!["todo:1"].elapsed_ms);
    }

    [Fact]
    public void GenerateWorkId_returns_slugged_identifier()
    {
        Assert.Matches("^my-plan-[0-9a-f]{8}$", WriteState.GenerateWorkId("My Plan!"));
        Assert.Matches("^work-[0-9a-f]{8}$", WriteState.GenerateWorkId(" !!! "));
    }

    public void Dispose()
    {
        Directory.Delete(_tempDirectory, true);
    }

    private string CreatePlan(string name)
    {
        var plansDir = Path.Combine(_tempDirectory, Constants.PROMETHEUS_PLANS_DIR.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(plansDir);
        var path = Path.Combine(plansDir, name + ".md");
        File.WriteAllText(path, "- [ ] todo\n");
        return path;
    }

    private string CreateStateDirectory(string json)
    {
        var directory = Path.Combine(_tempDirectory, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(directory, Constants.BOULDER_DIR.Replace('/', Path.DirectorySeparatorChar)));
        File.WriteAllText(PathHelper.GetBoulderFilePath(directory), json);
        return directory;
    }
}
