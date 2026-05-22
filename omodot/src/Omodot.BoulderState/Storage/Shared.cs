using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Omodot.BoulderState.Storage;

public static class Shared
{
    public static readonly HashSet<string> RESERVED_KEYS = new(StringComparer.Ordinal)
    {
        "__proto__",
        "prototype",
        "constructor",
    };

    public static JsonSerializerOptions JsonOptions { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    static Shared()
    {
        JsonOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
    }

    public static string NowIsoString()
    {
        return DateTimeOffset.UtcNow.ToString("O");
    }

    public static long? ParseIsoToMs(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateTimeOffset.TryParse(value, out var parsed) ? parsed.ToUnixTimeMilliseconds() : null;
    }

    public static long? GetElapsedMs(string? startedAt, string? endedAt)
    {
        var startedMs = ParseIsoToMs(startedAt);
        var endedMs = ParseIsoToMs(endedAt);
        if (startedMs is null || endedMs is null)
        {
            return null;
        }

        return endedMs.Value - startedMs.Value;
    }

    public static bool IsValidWorkStatus(string? status)
    {
        return status is "active" or "completed" or "paused" or "abandoned";
    }

    public static BoulderWorkState BuildWorkFromMirror(BoulderState state)
    {
        var planName = string.IsNullOrWhiteSpace(state.plan_name) ? state.active_plan : state.plan_name;
        return new BoulderWorkState
        {
            work_id = $"{planName}-legacy",
            active_plan = state.active_plan,
            plan_name = planName,
            status = state.status,
            started_at = state.started_at,
            ended_at = state.ended_at,
            elapsed_ms = state.elapsed_ms,
            updated_at = state.updated_at,
            session_ids = [.. state.session_ids],
            session_origins = state.session_origins is null ? null : new(state.session_origins, StringComparer.Ordinal),
            agent = state.agent,
            worktree_path = state.worktree_path,
            task_sessions = CloneTaskSessions(state.task_sessions),
        };
    }

    public static void ProjectWorkToMirror(BoulderState state, BoulderWorkState work)
    {
        state.active_plan = work.active_plan;
        state.plan_name = work.plan_name;
        state.status = work.status;
        state.started_at = work.started_at;
        state.ended_at = work.ended_at;
        state.elapsed_ms = work.elapsed_ms;
        state.updated_at = work.updated_at;
        state.session_ids = [.. work.session_ids];
        state.session_origins = work.session_origins is null ? [] : new(work.session_origins, StringComparer.Ordinal);
        state.agent = work.agent;
        state.worktree_path = work.worktree_path;
        state.task_sessions = CloneTaskSessions(work.task_sessions) ?? [];
    }

    public static BoulderWorkState? SelectMirrorWork(BoulderState state)
    {
        var works = state.works?.Values.ToList() ?? [];
        if (works.Count == 0)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(state.active_work_id) && state.works!.TryGetValue(state.active_work_id, out var matched))
        {
            return matched;
        }

        return works
            .OrderByDescending(work => ParseIsoToMs(work.updated_at ?? work.started_at) ?? 0)
            .FirstOrDefault();
    }

    public static BoulderState CloneState(BoulderState state)
    {
        return new BoulderState
        {
            schema_version = state.schema_version,
            active_work_id = state.active_work_id,
            works = state.works?.ToDictionary(pair => pair.Key, pair => CloneWork(pair.Value), StringComparer.Ordinal),
            active_plan = state.active_plan,
            started_at = state.started_at,
            ended_at = state.ended_at,
            elapsed_ms = state.elapsed_ms,
            status = state.status,
            updated_at = state.updated_at,
            session_ids = [.. state.session_ids],
            session_origins = state.session_origins is null ? null : new(state.session_origins, StringComparer.Ordinal),
            plan_name = state.plan_name,
            agent = state.agent,
            worktree_path = state.worktree_path,
            task_sessions = CloneTaskSessions(state.task_sessions),
        };
    }

    public static BoulderWorkState CloneWork(BoulderWorkState work)
    {
        return new BoulderWorkState
        {
            work_id = work.work_id,
            active_plan = work.active_plan,
            plan_name = work.plan_name,
            status = work.status,
            started_at = work.started_at,
            ended_at = work.ended_at,
            elapsed_ms = work.elapsed_ms,
            updated_at = work.updated_at,
            session_ids = [.. work.session_ids],
            session_origins = work.session_origins is null ? null : new(work.session_origins, StringComparer.Ordinal),
            agent = work.agent,
            worktree_path = work.worktree_path,
            task_sessions = CloneTaskSessions(work.task_sessions),
        };
    }

    public static Dictionary<string, TaskSessionState>? CloneTaskSessions(Dictionary<string, TaskSessionState>? source)
    {
        return source?.ToDictionary(pair => pair.Key, pair => CloneTaskSession(pair.Value), StringComparer.Ordinal);
    }

    public static TaskSessionState CloneTaskSession(TaskSessionState taskSession)
    {
        return new TaskSessionState
        {
            task_key = taskSession.task_key,
            task_label = taskSession.task_label,
            task_title = taskSession.task_title,
            session_id = taskSession.session_id,
            agent = taskSession.agent,
            category = taskSession.category,
            started_at = taskSession.started_at,
            ended_at = taskSession.ended_at,
            elapsed_ms = taskSession.elapsed_ms,
            status = taskSession.status,
            updated_at = taskSession.updated_at,
        };
    }

    internal static string? GetString(JsonObject obj, string name)
    {
        return obj[name] is JsonValue value ? value.TryGetValue<string>(out var result) ? result : null : null;
    }

    internal static long? GetInt64(JsonObject obj, string name)
    {
        return obj[name] is JsonValue value ? value.TryGetValue<long>(out var result) ? result : null : null;
    }

    internal static int? GetInt32(JsonObject obj, string name)
    {
        return obj[name] is JsonValue value ? value.TryGetValue<int>(out var result) ? result : null : null;
    }

    internal static TEnum? GetEnum<TEnum>(JsonObject obj, string name) where TEnum : struct, Enum
    {
        var value = GetString(obj, name);
        return Enum.TryParse<TEnum>(value, true, out var parsed) ? parsed : null;
    }

    internal static List<string> GetStringArray(JsonObject obj, string name)
    {
        if (obj[name] is not JsonArray array)
        {
            return [];
        }

        var values = new List<string>(array.Count);
        foreach (var item in array)
        {
            if (item is JsonValue value && value.TryGetValue<string>(out var text))
            {
                values.Add(text);
            }
        }

        return values;
    }

    internal static Dictionary<string, BoulderSessionOrigin> GetSessionOrigins(JsonObject obj, string name)
    {
        var results = new Dictionary<string, BoulderSessionOrigin>(StringComparer.Ordinal);
        if (obj[name] is not JsonObject source)
        {
            return results;
        }

        foreach (var pair in source)
        {
            if (pair.Value is JsonValue value && value.TryGetValue<string>(out var text) && Enum.TryParse<BoulderSessionOrigin>(text, true, out var parsed))
            {
                results[pair.Key] = parsed;
            }
        }

        return results;
    }
}
