using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace Lfe.BoulderState.Storage;

public static partial class WriteState
{
    [GeneratedRegex("[^a-z0-9]+", RegexOptions.IgnoreCase)]
    private static partial Regex NonSlugPattern();

    [GeneratedRegex("^-+|-+$")]
    private static partial Regex TrimHyphenPattern();

    public static bool WriteBoulderState(string directory, BoulderState state)
    {
        var filePath = PathHelper.GetBoulderFilePath(directory);

        try
        {
            var parent = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(parent) && !Directory.Exists(parent))
            {
                Directory.CreateDirectory(parent);
            }

            var stateToWrite = Shared.CloneState(state);
            if (stateToWrite.works is not null && !string.IsNullOrWhiteSpace(stateToWrite.active_work_id) && stateToWrite.works.TryGetValue(stateToWrite.active_work_id, out var activeWork))
            {
                stateToWrite.works[stateToWrite.active_work_id] = new BoulderWorkState
                {
                    work_id = activeWork.work_id,
                    active_plan = stateToWrite.active_plan,
                    plan_name = stateToWrite.plan_name,
                    status = stateToWrite.status,
                    started_at = stateToWrite.started_at,
                    ended_at = stateToWrite.ended_at,
                    elapsed_ms = stateToWrite.elapsed_ms,
                    updated_at = stateToWrite.updated_at,
                    session_ids = [.. stateToWrite.session_ids],
                    session_origins = stateToWrite.session_origins is null ? [] : new(stateToWrite.session_origins, StringComparer.Ordinal),
                    agent = stateToWrite.agent,
                    worktree_path = stateToWrite.worktree_path,
                    task_sessions = Shared.CloneTaskSessions(stateToWrite.task_sessions) ?? [],
                };
            }

            File.WriteAllText(filePath, System.Text.Json.JsonSerializer.Serialize(stateToWrite, Shared.JsonOptions));
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool ClearBoulderState(string directory)
    {
        var filePath = PathHelper.GetBoulderFilePath(directory);

        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    public static string GenerateWorkId(string planName)
    {
        var slug = TrimHyphenPattern().Replace(NonSlugPattern().Replace(planName.Trim().ToLowerInvariant(), "-"), string.Empty);
        var randomHex = RandomNumberGenerator.GetHexString(8).ToLowerInvariant();
        return $"{(slug.Length > 0 ? slug : "work")}-{randomHex}";
    }

    public static BoulderState CreateBoulderState(string planPath, string sessionId, string? agent = null, string? worktreePath = null)
    {
        var startedAt = Shared.NowIsoString();
        var planName = PlanProgressHelper.GetPlanName(planPath);
        var workId = GenerateWorkId(planName);

        var work = new BoulderWorkState
        {
            work_id = workId,
            active_plan = planPath,
            plan_name = planName,
            status = BoulderWorkStatus.active,
            started_at = startedAt,
            updated_at = startedAt,
            session_ids = [sessionId],
            session_origins = new Dictionary<string, BoulderSessionOrigin>(StringComparer.Ordinal) { [sessionId] = BoulderSessionOrigin.direct },
            agent = agent,
            worktree_path = worktreePath,
            task_sessions = [],
        };

        return new BoulderState
        {
            schema_version = 2,
            active_work_id = workId,
            works = new Dictionary<string, BoulderWorkState>(StringComparer.Ordinal) { [workId] = work },
            active_plan = planPath,
            started_at = startedAt,
            status = BoulderWorkStatus.active,
            updated_at = startedAt,
            session_ids = [sessionId],
            session_origins = new Dictionary<string, BoulderSessionOrigin>(StringComparer.Ordinal) { [sessionId] = BoulderSessionOrigin.direct },
            plan_name = planName,
            task_sessions = [],
            agent = agent,
            worktree_path = worktreePath,
        };
    }

    public static BoulderState? SelectActiveWork(string directory, string workId)
    {
        var state = ReadState.ReadBoulderState(directory);
        if (state is null)
        {
            return null;
        }

        var works = ReadState.GetBoulderWorks(state);
        var nextWork = works.FirstOrDefault(work => work.work_id == workId);
        if (nextWork is null)
        {
            return null;
        }

        var nextState = Shared.CloneState(state);
        nextState.schema_version = 2;
        nextState.active_work_id = workId;
        nextState.works ??= works.ToDictionary(work => work.work_id, Shared.CloneWork, StringComparer.Ordinal);
        Shared.ProjectWorkToMirror(nextState, nextWork);
        return WriteBoulderState(directory, nextState) ? nextState : null;
    }

    public static BoulderState? AddBoulderWork(string directory, string planPath, string sessionId, string? agent = null, string? worktreePath = null, string? startedAt = null)
    {
        var state = ReadState.ReadBoulderState(directory);
        if (state is null)
        {
            return null;
        }

        var planName = PlanProgressHelper.GetPlanName(planPath);
        var workId = GenerateWorkId(planName);
        var nextStartedAt = startedAt ?? Shared.NowIsoString();
        var nextWork = new BoulderWorkState
        {
            work_id = workId,
            active_plan = planPath,
            plan_name = planName,
            status = BoulderWorkStatus.active,
            started_at = nextStartedAt,
            updated_at = nextStartedAt,
            session_ids = [sessionId],
            session_origins = new Dictionary<string, BoulderSessionOrigin>(StringComparer.Ordinal) { [sessionId] = BoulderSessionOrigin.direct },
            agent = agent,
            worktree_path = worktreePath,
            task_sessions = [],
        };

        var nextState = Shared.CloneState(state);
        nextState.schema_version = 2;
        nextState.works = ReadState.GetBoulderWorks(state).ToDictionary(work => work.work_id, Shared.CloneWork, StringComparer.Ordinal);
        nextState.works[workId] = nextWork;
        nextState.active_work_id = workId;
        Shared.ProjectWorkToMirror(nextState, nextWork);
        return WriteBoulderState(directory, nextState) ? nextState : null;
    }

    public static BoulderState? CompleteBoulder(string directory, string? workId = null, string? endedAt = null)
    {
        var state = ReadState.ReadBoulderState(directory);
        if (state is null)
        {
            return null;
        }

        var targetWorkId = workId ?? state.active_work_id;
        if (string.IsNullOrWhiteSpace(targetWorkId))
        {
            return null;
        }

        var work = state.works?.TryGetValue(targetWorkId, out var existingWork) == true
            ? existingWork
            : ReadState.GetBoulderWorks(state).FirstOrDefault(candidate => candidate.work_id == targetWorkId);
        if (work is null)
        {
            return null;
        }

        if (work.status == BoulderWorkStatus.completed && work.ended_at is not null && work.elapsed_ms is not null)
        {
            return state;
        }

        var endAtValue = endedAt ?? Shared.NowIsoString();
        work.ended_at = endAtValue;
        work.elapsed_ms = Shared.GetElapsedMs(work.started_at, endAtValue);
        work.status = BoulderWorkStatus.completed;
        work.updated_at = Shared.NowIsoString();

        if (state.active_work_id == targetWorkId)
        {
            Shared.ProjectWorkToMirror(state, work);
        }

        return WriteBoulderState(directory, state) ? state : null;
    }
}
