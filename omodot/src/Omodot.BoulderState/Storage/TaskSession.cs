namespace Omodot.BoulderState.Storage;

public static class TaskSessionStorage
{
    public static BoulderState? UpsertTaskSessionState(
        string directory,
        string taskKey,
        string taskLabel,
        string taskTitle,
        string sessionId,
        string? agent = null,
        string? category = null)
    {
        var stateForWork = ReadState.ReadBoulderState(directory);
        if (!string.IsNullOrWhiteSpace(stateForWork?.active_work_id))
        {
            return UpsertTaskSessionStateForWork(directory, stateForWork.active_work_id, taskKey, taskLabel, taskTitle, sessionId, agent, category);
        }

        var state = ReadState.ReadBoulderState(directory);
        if (state is null || Shared.RESERVED_KEYS.Contains(taskKey))
        {
            return null;
        }

        state.task_sessions ??= [];
        state.task_sessions[taskKey] = new TaskSessionState
        {
            task_key = taskKey,
            task_label = taskLabel,
            task_title = taskTitle,
            session_id = sessionId,
            agent = agent,
            category = category,
            updated_at = Shared.NowIsoString(),
        };

        return WriteState.WriteBoulderState(directory, state) ? state : null;
    }

    public static BoulderState? UpsertTaskSessionStateForWork(
        string directory,
        string workId,
        string taskKey,
        string taskLabel,
        string taskTitle,
        string sessionId,
        string? agent = null,
        string? category = null)
    {
        if (Shared.RESERVED_KEYS.Contains(taskKey))
        {
            return null;
        }

        var state = ReadState.ReadBoulderState(directory);
        if (state is null)
        {
            return null;
        }

        var works = ReadState.GetBoulderWorks(state);
        var targetWork = works.FirstOrDefault(work => work.work_id == workId);
        if (targetWork is null)
        {
            return null;
        }

        targetWork.task_sessions ??= [];
        targetWork.task_sessions.TryGetValue(taskKey, out var previousTaskSession);
        var nextTaskSession = new TaskSessionState
        {
            task_key = taskKey,
            task_label = taskLabel,
            task_title = taskTitle,
            session_id = sessionId,
            agent = agent,
            category = category,
            started_at = previousTaskSession?.started_at,
            ended_at = previousTaskSession?.ended_at,
            elapsed_ms = previousTaskSession?.elapsed_ms,
            status = previousTaskSession?.status,
            updated_at = Shared.NowIsoString(),
        };

        var nextWork = Shared.CloneWork(targetWork);
        nextWork.task_sessions ??= [];
        nextWork.task_sessions[taskKey] = nextTaskSession;
        nextWork.updated_at = Shared.NowIsoString();

        var nextState = Shared.CloneState(state);
        nextState.schema_version = 2;
        nextState.works = works.ToDictionary(work => work.work_id, Shared.CloneWork, StringComparer.Ordinal);
        nextState.works[workId] = nextWork;

        if (state.active_work_id == workId)
        {
            Shared.ProjectWorkToMirror(nextState, nextWork);
        }

        return WriteState.WriteBoulderState(directory, nextState) ? nextState : null;
    }

    public static BoulderState? StartTaskTimer(
        string directory,
        string workId,
        string taskKey,
        string taskLabel,
        string taskTitle,
        string sessionId,
        string? agent = null,
        string? category = null,
        string? startedAt = null)
    {
        var nextState = UpsertTaskSessionStateForWork(directory, workId, taskKey, taskLabel, taskTitle, sessionId, agent, category);
        if (nextState is null)
        {
            return null;
        }

        if (nextState.works?.TryGetValue(workId, out var work) != true || work is null)
        {
            return null;
        }

        if (work.task_sessions?.TryGetValue(taskKey, out var taskSession) != true || taskSession is null)
        {
            return null;
        }

        taskSession.started_at ??= startedAt ?? Shared.NowIsoString();
        taskSession.status = BoulderTaskStatus.running;
        taskSession.updated_at = Shared.NowIsoString();
        work.updated_at = Shared.NowIsoString();

        if (nextState.active_work_id == workId)
        {
            Shared.ProjectWorkToMirror(nextState, work);
        }

        return WriteState.WriteBoulderState(directory, nextState) ? nextState : null;
    }

    public static BoulderState? EndTaskTimer(string directory, string workId, string taskKey, string? endedAt = null)
    {
        var state = ReadState.ReadBoulderState(directory);
        if (state is null)
        {
            return null;
        }

        var work = state.works?.TryGetValue(workId, out var existingWork) == true
            ? existingWork
            : ReadState.GetBoulderWorks(state).FirstOrDefault(candidate => candidate.work_id == workId);
        if (work is null)
        {
            return null;
        }

        if (work.task_sessions?.TryGetValue(taskKey, out var taskSession) != true || taskSession is null)
        {
            return null;
        }

        var endAtValue = endedAt ?? Shared.NowIsoString();
        taskSession.ended_at = endAtValue;
        taskSession.elapsed_ms = Shared.GetElapsedMs(taskSession.started_at, endAtValue);
        taskSession.status = BoulderTaskStatus.completed;
        taskSession.updated_at = Shared.NowIsoString();
        work.updated_at = Shared.NowIsoString();

        if (state.active_work_id == workId)
        {
            Shared.ProjectWorkToMirror(state, work);
        }

        return WriteState.WriteBoulderState(directory, state) ? state : null;
    }
}
