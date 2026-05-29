namespace Lfe.Hooks;

#region Hook Definition Types

public enum LfeHookStatus { BehaviorMapped, AdapterBound, Missing }

public enum LfeHookExitPath { PureDomainPort, AdapterBound, LimitedRedesign, ExplicitExclusion, Unclassified }

public enum LfeHookWave { Phase1Safety, Phase2Recovery, Phase3Orchestration, Phase4Host, Phase5AdapterConvergence }

public enum LfeHookTestType { Unit, Parity, Adapter, Integration, ManualQa }

public enum LfeHookAdapterImpact { None, Low, Medium, High }

public sealed record LfeHookDefinition(
    string Name,
    string OriginalExport,
    string Domain,
    LfeHookStatus Status,
    string? StandalonePackage,
    string OriginalSource,
    LfeHookExitPath ExitPath,
    string TargetPackage,
    LfeHookWave Wave,
    List<LfeHookTestType> TestTypes,
    LfeHookAdapterImpact AdapterImpact
);

#endregion

#region Question Types

public sealed record QuestionOption(string Label, string? Description = null);

public sealed record Question(
    string QuestionText,
    string? Header = null,
    List<QuestionOption>? Options = null,
    bool MultiSelect = false
);

public sealed record AskUserQuestionArgs(List<Question>? Questions = null);

#endregion

#region Hook I/O Types

public sealed record ToolBeforeHookInput(string? Tool = null);

public sealed record ToolBeforeHookOutput(Dictionary<string, object> Args);

public sealed record ChatMessageHookInput(
    string SessionID,
    string? Agent = null,
    ModelRef? Model = null
);

public sealed record ChatMessageHookOutput(
    ChatMessageInner? Message = null
);

public sealed record ChatMessageInner(
    string? Agent = null,
    string? Variant = null
);

#endregion

#region Model Agent Guard Types

public sealed record ModelRef(string ProviderID, string ModelID);

public sealed record ToastInfo(string Title, string Message, string Variant);

public sealed record ModelAgentGuardDecision(
    string? Agent = null,
    string? OutputAgent = null,
    string? Variant = null,
    string? SessionAgent = null,
    ToastInfo? Toast = null
);

public sealed record ModelAgentGuardOptions(
    string? SessionAgent = null,
    bool AllowHephaestusNonGptModel = false
)
{
    public static readonly ModelAgentGuardOptions Empty = new();
}

#endregion

#region Anthropic Effort Types

public sealed record AnthropicEffortInput(
    string SessionID,
    AgentRef? Agent = null,
    ModelRef? Model = null,
    VariantHolder Message = null!
)
{
    public AnthropicEffortInput() : this(string.Empty) { }
}

public sealed record AgentRef(string? Name = null);

public sealed record VariantHolder(string? Variant = null);

public sealed record AnthropicEffortOutput(Dictionary<string, object> Options);

public enum AnthropicEffortReason
{
    NotClaude, UnsupportedModel, InternalAgent, ExistingEffort,
    VariantNotMax, Injected, ClampedExisting, ClampedVariant
}

public sealed record AnthropicEffortDecision(
    string? Effort = null,
    string? Variant = null,
    AnthropicEffortReason? Reason = null
);

#endregion

#region Message Types

public sealed record MessagePart(string Type, Dictionary<string, object>? Extra = null)
{
    public string? Text => Extra?.TryGetValue("text", out var v) == true ? v as string : null;
    public string? Thinking => Extra?.TryGetValue("thinking", out var v) == true ? v as string : null;
    public string? Signature => Extra?.TryGetValue("signature", out var v) == true ? v as string : null;
    public string? Id => Extra?.TryGetValue("id", out var v) == true ? v as string : null;
    public string? CallID => Extra?.TryGetValue("callID", out var v) == true ? v as string : null;
    public string? ToolUseId => Extra?.TryGetValue("toolUseId", out var v) == true ? v as string : null;
    public bool? Synthetic => Extra?.TryGetValue("synthetic", out var v) == true ? v as bool? : null;
    public bool? IsError => Extra?.TryGetValue("isError", out var v) == true ? v as bool? : null;
}

public sealed record MessageInfo(string Role, Dictionary<string, object>? Extra = null)
{
    public string? SessionID => Extra?.TryGetValue("sessionID", out var v) == true ? v as string : null;
    public string? Agent => Extra?.TryGetValue("agent", out var v) == true ? v as string : null;
}

public sealed record MessageWithParts(MessageInfo Info, List<MessagePart> Parts);

public sealed record ToolAfterHookOutput(string Output, string? Title = null, object? Metadata = null);

#endregion

#region Domain Types

public sealed record TodoLike(string Status);

public sealed record AvailableSkillLike(string Name, string? Location = null);

public enum PathClassification { Icloud, Onedrive, DesktopSync, NetworkDrive, Unknown }

public sealed record FsyncSkipEntry(string FilePath, string ErrorCode, PathClassification PathClassification);

public enum ShellType { Posix, Cmd, PowerShell }

public sealed class AgentUsageState
{
    public string SessionID { get; }
    public bool AgentUsed { get; set; }
    public int ReminderCount { get; set; }
    public long UpdatedAt { get; set; }

    public AgentUsageState(string sessionID, bool agentUsed, int reminderCount, long updatedAt)
    {
        SessionID = sessionID;
        AgentUsed = agentUsed;
        ReminderCount = reminderCount;
        UpdatedAt = updatedAt;
    }
}

public enum NotificationPlatform { Darwin, Linux, Win32, Unsupported }

public enum KeywordType { Ultrawork, Search, Analyze, Team, Hyperplan, HyperplanUltrawork }

public sealed record DetectedKeyword(KeywordType Type, string Message);

public sealed record ParsedSlashCommand(string Command, string Args, string Raw);

public sealed record SlashCommandInfo(
    string Name, string Scope,
    string? Content = null, string? Description = null,
    string? Model = null, string? Agent = null
);

public sealed record RecoveryErrorType
{
    public static readonly string ToolResultMissing = "tool_result_missing";
    public static readonly string ThinkingBlockOrder = "thinking_block_order";
    public static readonly string ThinkingDisabledViolation = "thinking_disabled_violation";
    public static readonly string ThinkingBlockModified = "thinking_block_modified";
    public static readonly string AssistantPrefillUnsupported = "assistant_prefill_unsupported";
    public static readonly string UnavailableTool = "unavailable_tool";
}

public sealed record ParsedTokenLimitError(
    int CurrentTokens, int MaxTokens, string ErrorType,
    string? RequestId = null, int? MessageIndex = null
);

public enum IdleNotificationDecision
{
    Scheduled, IgnoredAlreadyNotified, IgnoredPending,
    IgnoredExecuting, CancelledByActivity, Deleted
}

public sealed record TodoSnapshot(
    string? Id, string Content,
    string? Status = null, string? Priority = null
);

public sealed class TailMonitorState
{
    public string? CurrentMessageID { get; set; }
    public bool CurrentHasOutput { get; set; }
    public int ConsecutiveNoTextMessages { get; set; }
    public long? LastCompactedAt { get; set; }
    public long? LastRecoveryAt { get; set; }
}

public sealed record BackgroundTaskLike(
    string Id, string Description, string Agent, string Status,
    string? SessionId = null, bool IsUnstableAgent = false,
    ModelRef? Model = null
);

public sealed record ContextTokenInfo(
    int? Input, int? Output = null, int? Reasoning = null,
    CacheInfo? Cache = null
);

public sealed record CacheInfo(int? Read = null, int? Write = null);

public sealed record ExistingFileGuardArgs(
    string? FilePath = null, string? Path = null,
    string? FilePathSnake = null, object? Overwrite = null
);

public sealed record ImageDimensions(int Width, int Height);

public sealed record ThinkModeState(
    bool Requested, bool ModelSwitched, bool VariantSet,
    string? ProviderID = null, string? ModelID = null
);

public sealed class ContinuationState
{
    public int StagnationCount { get; set; }
    public int? LastIncompleteCount { get; set; }
    public bool? AwaitingPostInjectionProgressCheck { get; set; }
}

public sealed record WorktreeEntry(string Path, string? Branch = null, bool Bare = false);

public sealed record ParsedUserRequest(string? PlanName, string? ExplicitWorktreePath);

public sealed record TrackedTaskRef(string Key, string Label, string Title);

public sealed record PlanFormatValidationResult(int RawCount, int ParsedCount, string? Warning = null);

public sealed record TeamParticipant
{
    public string Role { get; init; } = "neither";
    public string? TeamRunId { get; init; }
    public string? MemberName { get; init; }

    public static TeamParticipant Neither() => new() { Role = "neither" };
    public static TeamParticipant Lead(string teamRunId) => new() { Role = "lead", TeamRunId = teamRunId };
    public static TeamParticipant Member(string teamRunId, string memberName) =>
        new() { Role = "member", TeamRunId = teamRunId, MemberName = memberName };
}

public sealed record DelegateTaskErrorInfo(string ErrorType, string OriginalOutput);

public sealed record WriteExistingFileGuardDecision(string Value)
{
    public static readonly WriteExistingFileGuardDecision Allow = new("allow");
    public static readonly WriteExistingFileGuardDecision RegisterRead = new("register-read");
    public static readonly WriteExistingFileGuardDecision Block = new("block");

    public override string ToString() => Value;
}

public sealed record RuntimeFallbackConfig(int[]? RetryOnErrors = null);

public sealed record InteractiveBashSessionState(
    string SessionID, HashSet<string> TmuxSessions, long UpdatedAt
);

public sealed record SessionNotificationMessage(
    NotificationMessageInfo? Info = null,
    List<SessionNotificationPart>? Parts = null
);

public sealed record NotificationMessageInfo(string? Role = null, object? Error = null);

public sealed record SessionNotificationPart(string? Type = null, string? Text = null);

public sealed record LegacyPluginToastInput(
    bool HasLegacyEntry, string[]? LegacyEntries = null,
    LegacyPluginMigration? Migration = null
);

public sealed record LegacyPluginMigration(bool Migrated, string? From = null, string? To = null);

public sealed record LegacyPluginToastDecision(
    string Title, string Message, string Variant, int Duration
);

#endregion
