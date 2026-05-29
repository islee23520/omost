namespace Lfe.BackgroundAgent;

public sealed record Todo
{
    public string Content { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string Priority { get; init; } = string.Empty;
    public string? Id { get; init; }
}

public sealed record DelegatedModelConfig
{
    public string ProviderId { get; init; } = string.Empty;
    public string ModelId { get; init; } = string.Empty;
    public string? Variant { get; init; }
}

public sealed record FallbackEntry
{
    public string Model { get; init; } = string.Empty;
    public IReadOnlyList<string> Providers { get; init; } = Array.Empty<string>();
    public string? Variant { get; init; }
}

public sealed record SessionPermissionRule
{
    public string? Match { get; init; }
    public string? Permission { get; init; }
    public IReadOnlyDictionary<string, object?> AdditionalValues { get; init; } = new Dictionary<string, object?>();
}

public sealed record ToolCallWindow
{
    public string LastSignature { get; init; } = string.Empty;
    public int ConsecutiveCount { get; init; }
    public int Threshold { get; init; }
}

public sealed record TaskProgress
{
    public int ToolCalls { get; set; }
    public string? LastTool { get; set; }
    public ToolCallWindow? ToolCallWindow { get; set; }
    public HashSet<string>? CountedToolPartIds { get; set; }
    public DateTime LastUpdate { get; set; }
    public string? LastMessage { get; set; }
    public DateTime? LastMessageAt { get; set; }
}

public sealed record BackgroundTaskAttempt
{
    public string AttemptId { get; set; } = string.Empty;
    public int AttemptNumber { get; set; }
    public string? SessionId { get; set; }
    public string? ProviderId { get; set; }
    public string? ModelId { get; set; }
    public string? Variant { get; set; }
    public string Status { get; set; } = BackgroundTaskStatuses.Pending;
    public string? Error { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public sealed record RetryNotification
{
    public string? PreviousSessionId { get; init; }
    public string? FailedModel { get; init; }
    public string? FailedError { get; init; }
    public string NextModel { get; init; } = string.Empty;
}

public sealed record ParentModelInfo
{
    public string ProviderId { get; init; } = string.Empty;
    public string ModelId { get; init; } = string.Empty;
}

public sealed record BackgroundTask
{
    public string Id { get; init; } = string.Empty;
    public string? SessionId { get; set; }
    public string? RootSessionId { get; init; }
    public string ParentSessionId { get; init; } = string.Empty;
    public string ParentMessageId { get; init; } = string.Empty;
    public string? TeamRunId { get; init; }
    public string Description { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
    public string Agent { get; init; } = string.Empty;
    public int? SpawnDepth { get; init; }
    public string Status { get; set; } = BackgroundTaskStatuses.Pending;
    public DateTime? QueuedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Result { get; set; }
    public string? Error { get; set; }
    public TaskProgress? Progress { get; set; }
    public ParentModelInfo? ParentModel { get; init; }
    public DelegatedModelConfig? Model { get; set; }
    public IReadOnlyList<FallbackEntry>? FallbackChain { get; init; }
    public int? AttemptCount { get; init; }
    public string? ConcurrencyKey { get; init; }
    public string? ConcurrencyGroup { get; init; }
    public string? ParentAgent { get; init; }
    public IReadOnlyDictionary<string, bool>? ParentTools { get; init; }
    public string? SkillContent { get; init; }
    public IReadOnlyList<SessionPermissionRule>? SessionPermission { get; init; }
    public bool? IsUnstableAgent { get; init; }
    public string? Category { get; init; }
    public Func<string, Task>? OnSessionCreatedAsync { get; init; }
    public RetryNotification? RetryNotification { get; init; }
    public List<BackgroundTaskAttempt>? Attempts { get; set; }
    public string? CurrentAttemptId { get; set; }
    public int? LastMsgCount { get; set; }
    public int? StablePolls { get; set; }
    public int? ConsecutiveMissedPolls { get; set; }
}

public sealed record LaunchInput
{
    public string Description { get; init; } = string.Empty;
    public string Prompt { get; init; } = string.Empty;
    public string Agent { get; init; } = string.Empty;
    public string ParentSessionId { get; init; } = string.Empty;
    public string ParentMessageId { get; init; } = string.Empty;
    public string? TeamRunId { get; init; }
    public bool? SuppressTmuxSpawn { get; init; }
    public ParentModelInfo? ParentModel { get; init; }
    public string? ParentAgent { get; init; }
    public IReadOnlyDictionary<string, bool>? ParentTools { get; init; }
    public DelegatedModelConfig? Model { get; init; }
    public IReadOnlyList<FallbackEntry>? FallbackChain { get; init; }
    public bool? IsUnstableAgent { get; init; }
    public IReadOnlyList<string>? Skills { get; init; }
    public string? SkillContent { get; init; }
    public string? Category { get; init; }
    public IReadOnlyList<SessionPermissionRule>? SessionPermission { get; init; }
    public Func<string, Task>? OnSessionCreatedAsync { get; init; }
}

public sealed record ResumeInput
{
    public string SessionId { get; init; } = string.Empty;
    public string Prompt { get; init; } = string.Empty;
    public string ParentSessionId { get; init; } = string.Empty;
    public string ParentMessageId { get; init; } = string.Empty;
    public ParentModelInfo? ParentModel { get; init; }
    public string? ParentAgent { get; init; }
    public IReadOnlyDictionary<string, bool>? ParentTools { get; init; }
}

public sealed record CircuitBreakerConfig
{
    public bool? Enabled { get; init; }
    public int? MaxToolCalls { get; init; }
    public int? ConsecutiveThreshold { get; init; }
}

public sealed record BackgroundTaskCoreConfig
{
    public int? DefaultConcurrency { get; init; }
    public IReadOnlyDictionary<string, int>? ProviderConcurrency { get; init; }
    public IReadOnlyDictionary<string, int>? ModelConcurrency { get; init; }
    public int? MaxToolCalls { get; init; }
    public int? MaxDepth { get; init; }
    public int? StaleTimeoutMs { get; init; }
    public int? MessageStalenessTimeoutMs { get; init; }
    public int? SessionGoneTimeoutMs { get; init; }
    public int? TaskTtlMs { get; init; }
    public int? TaskCleanupDelayMs { get; init; }
    public CircuitBreakerConfig? CircuitBreaker { get; init; }
}

public sealed record QueueItem
{
    public string AttemptId { get; init; } = string.Empty;
    public BackgroundTask Task { get; init; } = new();
    public LaunchInput Input { get; init; } = new();
}

public sealed record MessagePartInfo
{
    public string? SessionId { get; init; }
    public string? Type { get; init; }
    public string? Tool { get; init; }
}

public sealed record EventProperties
{
    public string? SessionId { get; init; }
    public IReadOnlyDictionary<string, object?> AdditionalValues { get; init; } = new Dictionary<string, object?>();
}

public sealed record BackgroundEvent
{
    public string Type { get; init; } = string.Empty;
    public EventProperties? Properties { get; init; }
}

public sealed record SubagentSessionCreatedEvent
{
    public string SessionId { get; init; } = string.Empty;
    public string ParentId { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
}

public delegate Task OnSubagentSessionCreated(SubagentSessionCreatedEvent value);
