using System.ComponentModel.DataAnnotations;

namespace Lfe.TeamModeCore;

public interface ITeamMember
{
    string Name { get; }

    string Kind { get; }

    string? Cwd { get; }

    string? WorktreePath { get; }

    List<string>? Subscriptions { get; }

    string BackendType { get; }

    string? Color { get; }

    bool IsActive { get; }

    string? Category { get; }

    string? SubagentType { get; }

    string? Prompt { get; }
}

public sealed record CategoryMember : ITeamMember
{
    [Required, MinLength(1), RegularExpression("^[a-z0-9-]+$")]
    public string Name { get; init; } = string.Empty;

    public string Kind { get; init; } = "category";

    public string? Cwd { get; init; }

    public string? WorktreePath { get; init; }

    public List<string>? Subscriptions { get; init; }

    [AllowedValues("in-process", "tmux")]
    public string BackendType { get; init; } = "in-process";

    public string? Color { get; init; }

    public bool IsActive { get; init; } = true;

    [Required, MinLength(1)]
    public string Category { get; init; } = string.Empty;

    [Required, MinLength(1)]
    public string Prompt { get; init; } = string.Empty;

    string? ITeamMember.SubagentType => null;
}

public sealed record SubagentMember : ITeamMember
{
    [Required, MinLength(1), RegularExpression("^[a-z0-9-]+$")]
    public string Name { get; init; } = string.Empty;

    public string Kind { get; init; } = "subagent_type";

    public string? Cwd { get; init; }

    public string? WorktreePath { get; init; }

    public List<string>? Subscriptions { get; init; }

    [AllowedValues("in-process", "tmux")]
    public string BackendType { get; init; } = "in-process";

    public string? Color { get; init; }

    public bool IsActive { get; init; } = true;

    public string? Category => null;

    [Required, MinLength(1)]
    public string SubagentType { get; init; } = string.Empty;

    public string? Prompt { get; init; }
}

public sealed record TeamReference
{
    [Required]
    public string Path { get; init; } = string.Empty;

    public string? Description { get; init; }
}

public sealed record TeamSpec
{
    public int Version { get; init; } = 1;

    [Required, MinLength(1), RegularExpression("^[a-z0-9-]+$")]
    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    [Range(1, long.MaxValue)]
    public long CreatedAt { get; init; }

    public string? LeadAgentId { get; init; }

    public List<string>? TeamAllowedPaths { get; init; }

    public string? SessionPermission { get; init; }

    [MinLength(1), MaxLength(8)]
    public List<ITeamMember> Members { get; init; } = [];
}

public sealed record Message
{
    public int Version { get; init; } = 1;

    [Required]
    public string MessageId { get; init; } = string.Empty;

    [Required]
    public string From { get; init; } = string.Empty;

    [Required]
    public string To { get; init; } = string.Empty;

    [AllowedValues("message", "shutdown_request", "shutdown_approved", "shutdown_rejected", "announcement")]
    public string Kind { get; init; } = "message";

    [Required, MaxLength(32 * 1024)]
    public string Body { get; init; } = string.Empty;

    public string? Summary { get; init; }

    public List<TeamReference>? References { get; init; }

    [Range(1, long.MaxValue)]
    public long Timestamp { get; init; }

    public string? CorrelationId { get; init; }

    public string? Color { get; init; }
}

public sealed record TaskItem
{
    public int Version { get; init; } = 1;

    [Required]
    public string Id { get; init; } = string.Empty;

    [Required]
    public string Subject { get; init; } = string.Empty;

    [Required]
    public string Description { get; init; } = string.Empty;

    public string? ActiveForm { get; init; }

    [AllowedValues("pending", "claimed", "in_progress", "completed", "deleted")]
    public string Status { get; init; } = "pending";

    public string? Owner { get; init; }

    public List<string> Blocks { get; init; } = [];

    public List<string> BlockedBy { get; init; } = [];

    public Dictionary<string, object?>? Metadata { get; init; }

    [Range(1, long.MaxValue)]
    public long CreatedAt { get; init; }

    [Range(1, long.MaxValue)]
    public long UpdatedAt { get; init; }

    [Range(1, long.MaxValue)]
    public long? ClaimedAt { get; init; }
}

public sealed record RuntimeStateMemberModel
{
    public string ProviderId { get; init; } = string.Empty;

    public string ModelId { get; init; } = string.Empty;

    public string? Variant { get; init; }

    public string? ReasoningEffort { get; init; }

    public double? Temperature { get; init; }

    public double? TopP { get; init; }

    public double? MaxTokens { get; init; }

    public RuntimeStateThinking? Thinking { get; init; }
}

public sealed record RuntimeStateThinking
{
    [AllowedValues("enabled", "disabled")]
    public string Type { get; init; } = "disabled";

    [Range(1, int.MaxValue)]
    public int? BudgetTokens { get; init; }
}

public sealed record RuntimeStateMember
{
    public string Name { get; init; } = string.Empty;

    public string? SessionId { get; init; }

    public string? TmuxPaneId { get; init; }

    public string? TmuxGridPaneId { get; init; }

    [AllowedValues("leader", "general-purpose")]
    public string AgentType { get; init; } = "general-purpose";

    public string? SubagentType { get; init; }

    public string? Category { get; init; }

    public RuntimeStateMemberModel? Model { get; init; }

    [AllowedValues("pending", "running", "idle", "errored", "completed", "shutdown_approved")]
    public string Status { get; init; } = "pending";

    public string? Color { get; init; }

    public string? WorktreePath { get; init; }

    public string? LastInjectedTurnMarker { get; init; }

    public List<string> PendingInjectedMessageIds { get; init; } = [];
}

public sealed record RuntimeBounds
{
    public int MaxMembers { get; init; } = 8;

    public int MaxParallelMembers { get; init; } = 4;

    public int MaxMessagesPerRun { get; init; } = 10000;

    public int MaxWallClockMinutes { get; init; } = 120;

    public int MaxMemberTurns { get; init; } = 500;
}

public sealed record ShutdownRequest
{
    public string MemberId { get; init; } = string.Empty;

    public string RequesterName { get; init; } = string.Empty;

    [Range(1, long.MaxValue)]
    public long RequestedAt { get; init; }

    [Range(1, long.MaxValue)]
    public long? ApprovedAt { get; init; }

    public string? RejectedReason { get; init; }

    [Range(1, long.MaxValue)]
    public long? RejectedAt { get; init; }
}

public sealed record RuntimeStateTmuxLayout
{
    public bool OwnedSession { get; init; }

    public string TargetSessionId { get; init; } = string.Empty;

    public string? FocusWindowId { get; init; }

    public string? GridWindowId { get; init; }
}

public sealed record RuntimeState
{
    public int Version { get; init; } = 1;

    [Required]
    public string TeamRunId { get; init; } = string.Empty;

    [Required]
    public string TeamName { get; init; } = string.Empty;

    [AllowedValues("project", "user")]
    public string SpecSource { get; init; } = "project";

    [Range(1, long.MaxValue)]
    public long CreatedAt { get; init; }

    [AllowedValues("creating", "active", "shutdown_requested", "deleting", "deleted", "failed", "orphaned")]
    public string Status { get; init; } = "creating";

    public string? LeadSessionId { get; init; }

    public RuntimeStateTmuxLayout? TmuxLayout { get; init; }

    [MinLength(0)]
    public List<RuntimeStateMember> Members { get; init; } = [];

    public List<ShutdownRequest> ShutdownRequests { get; init; } = [];

    public RuntimeBounds Bounds { get; init; } = new();
}

public sealed record MailboxEntry
{
    public List<Message> Unread { get; set; } = [];

    public List<Message> Reserved { get; set; } = [];

    public List<Message> Processed { get; set; } = [];
}

public sealed record DeliveryReservation
{
    public string MemberName { get; init; } = string.Empty;

    public string MessageId { get; init; } = string.Empty;
}

public sealed record SendMessageLimits
{
    public int MessagePayloadMaxBytes { get; init; }

    public int RecipientUnreadMaxBytes { get; init; }
}

public sealed record SendMessageResult
{
    public Dictionary<string, MailboxEntry> State { get; init; } = new();

    public string MessageId { get; init; } = string.Empty;

    public List<string> DeliveredTo { get; init; } = [];
}

public record InjectionResult
{
    public bool Injected { get; init; }

    public string? Content { get; init; }

    public List<string> MessageIds { get; init; } = [];

    public string? Reason { get; init; }
}

public sealed record PollInjectionResult : InjectionResult
{
    public RuntimeState RuntimeState { get; init; } = new();
}

public sealed record TaskListState
{
    public List<TaskItem> Tasks { get; init; } = [];

    public int HighWatermark { get; init; }
}

public sealed record CreateTaskInput
{
    [Required]
    public string Subject { get; init; } = string.Empty;

    [Required]
    public string Description { get; init; } = string.Empty;

    public string? ActiveForm { get; init; }

    [AllowedValues("pending", "claimed", "in_progress", "completed", "deleted")]
    public string Status { get; init; } = "pending";

    public string? Owner { get; init; }

    public List<string> Blocks { get; init; } = [];

    public List<string> BlockedBy { get; init; } = [];

    public Dictionary<string, object?>? Metadata { get; init; }

    [Range(1, long.MaxValue)]
    public long? ClaimedAt { get; init; }
}

public sealed record TeamSessionEntry
{
    public string TeamRunId { get; init; } = string.Empty;

    public string MemberName { get; init; } = string.Empty;

    public string Role { get; init; } = string.Empty;
}

public sealed record TeamSpecEntry
{
    public string Name { get; init; } = string.Empty;

    public string Scope { get; init; } = string.Empty;

    public string Path { get; init; } = string.Empty;
}

public sealed record TeamModeCorePathConfig
{
    public string? BaseDir { get; init; }
}

public sealed record CallerTeamLead
{
    public string? AgentTypeId { get; init; }

    public string? DisplayName { get; init; }

    public bool IsEligibleForTeamLead { get; init; }
}

public sealed record NormalizeTeamSpecInputOptions
{
    public CallerTeamLead? CallerTeamLead { get; init; }

    public string? DefaultCategoryName { get; init; }
}

public sealed record ValidationIssue(string Path, string Message);

public sealed record ValidationError(IReadOnlyList<ValidationIssue> Issues, string Message);

public sealed record SafeParseResult<T>(bool Success, T? Data, ValidationError? Error);
