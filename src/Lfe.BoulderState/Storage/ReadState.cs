using System.Text.Json.Nodes;

namespace Lfe.BoulderState.Storage;

public static class ReadState
{
    public static BoulderState? ReadBoulderState(string directory)
    {
        var filePath = PathHelper.GetBoulderFilePath(directory);
        if (!File.Exists(filePath))
        {
            return null;
        }

        try
        {
            var parsed = JsonNode.Parse(File.ReadAllText(filePath)) as JsonObject;
            if (parsed is null)
            {
                return null;
            }

            var state = ParseState(parsed);
            NormalizeState(state);
            var mirrorWork = Shared.SelectMirrorWork(state);
            if (mirrorWork is not null)
            {
                state.active_work_id = mirrorWork.work_id;
                Shared.ProjectWorkToMirror(state, mirrorWork);
            }

            return state;
        }
        catch
        {
            return null;
        }
    }

    public static List<BoulderWorkState> GetBoulderWorks(BoulderState state)
    {
        if (state.works is { Count: > 0 })
        {
            return [.. state.works.Values];
        }

        if (string.IsNullOrWhiteSpace(state.active_plan) || string.IsNullOrWhiteSpace(state.plan_name) || string.IsNullOrWhiteSpace(state.started_at))
        {
            return [];
        }

        return [Shared.BuildWorkFromMirror(state)];
    }

    public static List<BoulderWorkState> GetActiveWorks(string directory)
    {
        var state = ReadBoulderState(directory);
        return state is null
            ? []
            : GetBoulderWorks(state).Where(work => work.status is not (BoulderWorkStatus.completed or BoulderWorkStatus.abandoned)).ToList();
    }

    public static BoulderWorkState? GetWorkById(string directory, string workId)
    {
        var state = ReadBoulderState(directory);
        return state is null ? null : GetBoulderWorks(state).FirstOrDefault(work => work.work_id == workId);
    }

    public static BoulderWorkState? GetWorkByPlanName(string directory, string planName, string? worktreePath = null)
    {
        var state = ReadBoulderState(directory);
        if (state is null)
        {
            return null;
        }

        return GetBoulderWorks(state).FirstOrDefault(work =>
            work.plan_name == planName && (worktreePath is null || work.worktree_path == worktreePath));
    }

    public static BoulderWorkState? GetWorkForSession(string directory, string sessionId)
    {
        var state = ReadBoulderState(directory);
        if (state is null)
        {
            return null;
        }

        var works = GetBoulderWorks(state)
            .Where(work => work.session_ids.Contains(sessionId, StringComparer.Ordinal))
            .OrderByDescending(work => Shared.ParseIsoToMs(work.updated_at ?? work.started_at) ?? 0)
            .ToList();

        if (works.Count > 0)
        {
            return works[0];
        }

        return state.session_ids.Contains(sessionId, StringComparer.Ordinal) ? Shared.BuildWorkFromMirror(state) : null;
    }

    public static List<BoulderWorkResumeOption> GetWorkResumeOptions(string directory)
    {
        var state = ReadBoulderState(directory);
        if (state is null)
        {
            return [];
        }

        return GetBoulderWorks(state)
            .Where(work => work.status is not (BoulderWorkStatus.completed or BoulderWorkStatus.abandoned))
            .Select(work => new BoulderWorkResumeOption
            {
                work_id = work.work_id,
                plan_name = work.plan_name,
                active_plan = work.active_plan,
                worktree_path = work.worktree_path,
                status = work.status ?? BoulderWorkStatus.active,
                started_at = work.started_at,
                updated_at = work.updated_at ?? work.started_at,
                ended_at = work.ended_at,
                elapsed_ms = work.elapsed_ms,
                session_count = work.session_ids.Count,
                progress = PlanProgressHelper.GetPlanProgress(PathHelper.ResolveBoulderPlanPathForWork(directory, work)),
                is_current_mirror = state.active_work_id == work.work_id,
            })
            .ToList();
    }

    public static TaskSessionState? GetTaskSessionState(string directory, string taskKey)
    {
        var state = ReadBoulderState(directory);
        if (state?.active_work_id is not null && state.works?.TryGetValue(state.active_work_id, out var work) == true && work.task_sessions?.TryGetValue(taskKey, out var taskSession) == true)
        {
            return taskSession;
        }

        return state?.task_sessions?.TryGetValue(taskKey, out var rootTaskSession) == true ? rootTaskSession : null;
    }

    private static BoulderState ParseState(JsonObject obj)
    {
        return new BoulderState
        {
            schema_version = Shared.GetInt32(obj, nameof(BoulderState.schema_version)),
            active_work_id = Shared.GetString(obj, nameof(BoulderState.active_work_id)),
            works = ParseWorks(obj[nameof(BoulderState.works)] as JsonObject),
            active_plan = Shared.GetString(obj, nameof(BoulderState.active_plan)) ?? string.Empty,
            started_at = Shared.GetString(obj, nameof(BoulderState.started_at)) ?? string.Empty,
            ended_at = Shared.GetString(obj, nameof(BoulderState.ended_at)),
            elapsed_ms = Shared.GetInt64(obj, nameof(BoulderState.elapsed_ms)),
            status = Shared.GetEnum<BoulderWorkStatus>(obj, nameof(BoulderState.status)),
            updated_at = Shared.GetString(obj, nameof(BoulderState.updated_at)),
            session_ids = Shared.GetStringArray(obj, nameof(BoulderState.session_ids)),
            session_origins = Shared.GetSessionOrigins(obj, nameof(BoulderState.session_origins)),
            plan_name = Shared.GetString(obj, nameof(BoulderState.plan_name)) ?? string.Empty,
            agent = Shared.GetString(obj, nameof(BoulderState.agent)),
            worktree_path = Shared.GetString(obj, nameof(BoulderState.worktree_path)),
            task_sessions = ParseTaskSessions(obj[nameof(BoulderState.task_sessions)] as JsonObject),
        };
    }

    private static Dictionary<string, BoulderWorkState>? ParseWorks(JsonObject? works)
    {
        if (works is null)
        {
            return null;
        }

        var result = new Dictionary<string, BoulderWorkState>(StringComparer.Ordinal);
        foreach (var pair in works)
        {
            if (pair.Value is JsonObject workObject)
            {
                result[pair.Key] = ParseWork(workObject);
            }
        }

        return result;
    }

    private static BoulderWorkState ParseWork(JsonObject obj)
    {
        return new BoulderWorkState
        {
            work_id = Shared.GetString(obj, nameof(BoulderWorkState.work_id)) ?? string.Empty,
            active_plan = Shared.GetString(obj, nameof(BoulderWorkState.active_plan)) ?? string.Empty,
            plan_name = Shared.GetString(obj, nameof(BoulderWorkState.plan_name)) ?? string.Empty,
            status = Shared.GetEnum<BoulderWorkStatus>(obj, nameof(BoulderWorkState.status)),
            started_at = Shared.GetString(obj, nameof(BoulderWorkState.started_at)) ?? string.Empty,
            ended_at = Shared.GetString(obj, nameof(BoulderWorkState.ended_at)),
            elapsed_ms = Shared.GetInt64(obj, nameof(BoulderWorkState.elapsed_ms)),
            updated_at = Shared.GetString(obj, nameof(BoulderWorkState.updated_at)),
            session_ids = Shared.GetStringArray(obj, nameof(BoulderWorkState.session_ids)),
            session_origins = Shared.GetSessionOrigins(obj, nameof(BoulderWorkState.session_origins)),
            agent = Shared.GetString(obj, nameof(BoulderWorkState.agent)),
            worktree_path = Shared.GetString(obj, nameof(BoulderWorkState.worktree_path)),
            task_sessions = ParseTaskSessions(obj[nameof(BoulderWorkState.task_sessions)] as JsonObject),
        };
    }

    private static Dictionary<string, TaskSessionState> ParseTaskSessions(JsonObject? sessions)
    {
        var result = new Dictionary<string, TaskSessionState>(StringComparer.Ordinal);
        if (sessions is null)
        {
            return result;
        }

        foreach (var pair in sessions)
        {
            if (pair.Value is not JsonObject taskSessionObject)
            {
                continue;
            }

            result[pair.Key] = new TaskSessionState
            {
                task_key = Shared.GetString(taskSessionObject, nameof(TaskSessionState.task_key)) ?? string.Empty,
                task_label = Shared.GetString(taskSessionObject, nameof(TaskSessionState.task_label)) ?? string.Empty,
                task_title = Shared.GetString(taskSessionObject, nameof(TaskSessionState.task_title)) ?? string.Empty,
                session_id = Shared.GetString(taskSessionObject, nameof(TaskSessionState.session_id)) ?? string.Empty,
                agent = Shared.GetString(taskSessionObject, nameof(TaskSessionState.agent)),
                category = Shared.GetString(taskSessionObject, nameof(TaskSessionState.category)),
                started_at = Shared.GetString(taskSessionObject, nameof(TaskSessionState.started_at)),
                ended_at = Shared.GetString(taskSessionObject, nameof(TaskSessionState.ended_at)),
                elapsed_ms = Shared.GetInt64(taskSessionObject, nameof(TaskSessionState.elapsed_ms)),
                status = Shared.GetEnum<BoulderTaskStatus>(taskSessionObject, nameof(TaskSessionState.status)),
                updated_at = Shared.GetString(taskSessionObject, nameof(TaskSessionState.updated_at)) ?? string.Empty,
            };
        }

        return result;
    }

    private static void NormalizeState(BoulderState state)
    {
        state.session_ids ??= [];
        state.session_origins ??= [];

        if (state.session_ids.Count == 1)
        {
            var soleSessionId = state.session_ids[0];
            if (!state.session_origins.TryGetValue(soleSessionId, out var origin))
            {
                state.session_origins[soleSessionId] = BoulderSessionOrigin.direct;
            }
            else if (origin is not (BoulderSessionOrigin.direct or BoulderSessionOrigin.appended))
            {
                state.session_origins[soleSessionId] = BoulderSessionOrigin.direct;
            }
        }

        state.task_sessions ??= [];

        if (state.works is null)
        {
            return;
        }

        foreach (var work in state.works.Values)
        {
            work.session_ids ??= [];
            work.session_origins ??= [];
            work.task_sessions ??= [];

            if (work.session_ids.Count == 1)
            {
                var soleSessionId = work.session_ids[0];
                if (!work.session_origins.ContainsKey(soleSessionId))
                {
                    work.session_origins[soleSessionId] = BoulderSessionOrigin.direct;
                }
            }
        }
    }
}
