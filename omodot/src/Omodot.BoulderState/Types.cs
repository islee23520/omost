using System.Text.Json.Serialization;

namespace Omodot.BoulderState;

[JsonConverter(typeof(JsonStringEnumConverter<BoulderSessionOrigin>))]
public enum BoulderSessionOrigin
{
    direct,
    appended,
}

[JsonConverter(typeof(JsonStringEnumConverter<BoulderWorkStatus>))]
public enum BoulderWorkStatus
{
    active,
    completed,
    paused,
    abandoned,
}

[JsonConverter(typeof(JsonStringEnumConverter<BoulderTaskStatus>))]
public enum BoulderTaskStatus
{
    running,
    completed,
    cancelled,
}

public sealed record class BoulderState
{
    public int? schema_version { get; set; }

    public string? active_work_id { get; set; }

    public Dictionary<string, BoulderWorkState>? works { get; set; }

    public string active_plan { get; set; } = string.Empty;

    public string started_at { get; set; } = string.Empty;

    public string? ended_at { get; set; }

    public long? elapsed_ms { get; set; }

    public BoulderWorkStatus? status { get; set; }

    public string? updated_at { get; set; }

    public List<string> session_ids { get; set; } = [];

    public Dictionary<string, BoulderSessionOrigin>? session_origins { get; set; }

    public string plan_name { get; set; } = string.Empty;

    public string? agent { get; set; }

    public string? worktree_path { get; set; }

    public Dictionary<string, TaskSessionState>? task_sessions { get; set; }
}

public sealed record class BoulderWorkState
{
    public string work_id { get; set; } = string.Empty;

    public string active_plan { get; set; } = string.Empty;

    public string plan_name { get; set; } = string.Empty;

    public BoulderWorkStatus? status { get; set; }

    public string started_at { get; set; } = string.Empty;

    public string? ended_at { get; set; }

    public long? elapsed_ms { get; set; }

    public string? updated_at { get; set; }

    public List<string> session_ids { get; set; } = [];

    public Dictionary<string, BoulderSessionOrigin>? session_origins { get; set; }

    public string? agent { get; set; }

    public string? worktree_path { get; set; }

    public Dictionary<string, TaskSessionState>? task_sessions { get; set; }
}

public sealed record class PlanProgress
{
    public int total { get; set; }

    public int completed { get; set; }

    public bool isComplete { get; set; }
}

public sealed record class TaskSessionState
{
    public string task_key { get; set; } = string.Empty;

    public string task_label { get; set; } = string.Empty;

    public string task_title { get; set; } = string.Empty;

    public string session_id { get; set; } = string.Empty;

    public string? agent { get; set; }

    public string? category { get; set; }

    public string? started_at { get; set; }

    public string? ended_at { get; set; }

    public long? elapsed_ms { get; set; }

    public BoulderTaskStatus? status { get; set; }

    public string updated_at { get; set; } = string.Empty;
}

public sealed record class BoulderWorkResumeOption
{
    public string work_id { get; set; } = string.Empty;

    public string plan_name { get; set; } = string.Empty;

    public string active_plan { get; set; } = string.Empty;

    public string? worktree_path { get; set; }

    public BoulderWorkStatus status { get; set; }

    public string started_at { get; set; } = string.Empty;

    public string updated_at { get; set; } = string.Empty;

    public string? ended_at { get; set; }

    public long? elapsed_ms { get; set; }

    public int session_count { get; set; }

    public PlanProgress progress { get; set; } = new();

    public bool is_current_mirror { get; set; }
}

public sealed record class TopLevelTaskRef
{
    public string key { get; set; } = string.Empty;

    public string section { get; set; } = string.Empty;

    public string label { get; set; } = string.Empty;

    public string title { get; set; } = string.Empty;
}
