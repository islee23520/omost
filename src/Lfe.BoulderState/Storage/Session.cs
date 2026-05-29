namespace Lfe.BoulderState.Storage;

public static class SessionStorage
{
    public static BoulderState? AppendSessionId(string directory, string sessionId, BoulderSessionOrigin origin = BoulderSessionOrigin.direct)
    {
        var activeWorkId = ReadState.ReadBoulderState(directory)?.active_work_id;
        if (!string.IsNullOrWhiteSpace(activeWorkId))
        {
            return AppendSessionIdForWork(directory, activeWorkId, sessionId, origin);
        }

        var state = ReadState.ReadBoulderState(directory);
        if (state is null)
        {
            return null;
        }

        state.session_origins ??= [];
        state.session_ids ??= [];

        if (!state.session_ids.Contains(sessionId, StringComparer.Ordinal))
        {
            var originalSessionIds = state.session_ids.ToList();
            var originalSessionOrigins = new Dictionary<string, BoulderSessionOrigin>(state.session_origins, StringComparer.Ordinal);
            state.session_ids.Add(sessionId);
            state.session_origins[sessionId] = origin;
            if (WriteState.WriteBoulderState(directory, state))
            {
                return state;
            }

            state.session_ids = originalSessionIds;
            state.session_origins = originalSessionOrigins;
            return null;
        }

        if (!state.session_origins.ContainsKey(sessionId))
        {
            state.session_origins[sessionId] = origin;
            if (!WriteState.WriteBoulderState(directory, state))
            {
                return null;
            }
        }

        return state;
    }

    public static BoulderState? AppendSessionIdForWork(string directory, string workId, string sessionId, BoulderSessionOrigin origin = BoulderSessionOrigin.direct)
    {
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

        var updatedWork = Shared.CloneWork(targetWork);
        if (!updatedWork.session_ids.Contains(sessionId, StringComparer.Ordinal))
        {
            updatedWork.session_ids.Add(sessionId);
        }

        updatedWork.session_origins ??= [];
        updatedWork.session_origins[sessionId] = origin;
        updatedWork.updated_at = Shared.NowIsoString();

        var nextState = Shared.CloneState(state);
        nextState.schema_version = 2;
        nextState.works = works.ToDictionary(work => work.work_id, Shared.CloneWork, StringComparer.Ordinal);
        nextState.works[workId] = updatedWork;

        if (state.active_work_id == workId)
        {
            Shared.ProjectWorkToMirror(nextState, updatedWork);
        }

        return WriteState.WriteBoulderState(directory, nextState) ? nextState : null;
    }
}
