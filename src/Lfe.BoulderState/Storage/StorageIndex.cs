namespace Lfe.BoulderState.Storage;

public static class StorageIndex
{
    public static string GetBoulderFilePath(string directory) => PathHelper.GetBoulderFilePath(directory);

    public static string ResolveBoulderPlanPath(string directory, BoulderState state) => PathHelper.ResolveBoulderPlanPath(directory, state);

    public static string ResolveBoulderPlanPathForWork(string directory, BoulderWorkState work) => PathHelper.ResolveBoulderPlanPathForWork(directory, work);

    public static List<string> FindPrometheusPlans(string directory) => PlanProgressHelper.FindPrometheusPlans(directory);

    public static string GetPlanName(string planPath) => PlanProgressHelper.GetPlanName(planPath);

    public static PlanProgress GetPlanProgress(string planPath) => PlanProgressHelper.GetPlanProgress(planPath);

    public static BoulderState? ReadBoulderState(string directory) => ReadState.ReadBoulderState(directory);

    public static List<BoulderWorkState> GetBoulderWorks(BoulderState state) => ReadState.GetBoulderWorks(state);

    public static List<BoulderWorkState> GetActiveWorks(string directory) => ReadState.GetActiveWorks(directory);

    public static BoulderWorkState? GetWorkById(string directory, string workId) => ReadState.GetWorkById(directory, workId);

    public static BoulderWorkState? GetWorkByPlanName(string directory, string planName, string? worktreePath = null) => ReadState.GetWorkByPlanName(directory, planName, worktreePath);

    public static BoulderWorkState? GetWorkForSession(string directory, string sessionId) => ReadState.GetWorkForSession(directory, sessionId);

    public static List<BoulderWorkResumeOption> GetWorkResumeOptions(string directory) => ReadState.GetWorkResumeOptions(directory);

    public static TaskSessionState? GetTaskSessionState(string directory, string taskKey) => ReadState.GetTaskSessionState(directory, taskKey);

    public static BoulderState? AppendSessionId(string directory, string sessionId, BoulderSessionOrigin origin = BoulderSessionOrigin.direct) => SessionStorage.AppendSessionId(directory, sessionId, origin);

    public static BoulderState? AppendSessionIdForWork(string directory, string workId, string sessionId, BoulderSessionOrigin origin = BoulderSessionOrigin.direct) => SessionStorage.AppendSessionIdForWork(directory, workId, sessionId, origin);

    public static BoulderState? UpsertTaskSessionState(string directory, string taskKey, string taskLabel, string taskTitle, string sessionId, string? agent = null, string? category = null)
        => TaskSessionStorage.UpsertTaskSessionState(directory, taskKey, taskLabel, taskTitle, sessionId, agent, category);

    public static BoulderState? UpsertTaskSessionStateForWork(string directory, string workId, string taskKey, string taskLabel, string taskTitle, string sessionId, string? agent = null, string? category = null)
        => TaskSessionStorage.UpsertTaskSessionStateForWork(directory, workId, taskKey, taskLabel, taskTitle, sessionId, agent, category);

    public static BoulderState? StartTaskTimer(string directory, string workId, string taskKey, string taskLabel, string taskTitle, string sessionId, string? agent = null, string? category = null, string? startedAt = null)
        => TaskSessionStorage.StartTaskTimer(directory, workId, taskKey, taskLabel, taskTitle, sessionId, agent, category, startedAt);

    public static BoulderState? EndTaskTimer(string directory, string workId, string taskKey, string? endedAt = null)
        => TaskSessionStorage.EndTaskTimer(directory, workId, taskKey, endedAt);

    public static bool WriteBoulderState(string directory, BoulderState state) => WriteState.WriteBoulderState(directory, state);

    public static bool ClearBoulderState(string directory) => WriteState.ClearBoulderState(directory);

    public static string GenerateWorkId(string planName) => WriteState.GenerateWorkId(planName);

    public static BoulderState CreateBoulderState(string planPath, string sessionId, string? agent = null, string? worktreePath = null)
        => WriteState.CreateBoulderState(planPath, sessionId, agent, worktreePath);

    public static BoulderState? SelectActiveWork(string directory, string workId) => WriteState.SelectActiveWork(directory, workId);

    public static BoulderState? AddBoulderWork(string directory, string planPath, string sessionId, string? agent = null, string? worktreePath = null, string? startedAt = null)
        => WriteState.AddBoulderWork(directory, planPath, sessionId, agent, worktreePath, startedAt);

    public static BoulderState? CompleteBoulder(string directory, string? workId = null, string? endedAt = null)
        => WriteState.CompleteBoulder(directory, workId, endedAt);
}
