using System.Text.Json;

namespace Omodot.TeamModeCore;

public sealed class RuntimeStateError(string message, string code) : Exception(message)
{
    public string Code { get; } = code;
}

public sealed class InvalidTransitionError(string from, string to) : Exception($"invalid transition {from} -> {to}");

public static class RuntimeStateManager
{
    private static readonly IReadOnlyDictionary<string, HashSet<string>> AllowedRuntimeTransitions = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
    {
        ["creating"] = ["active", "failed"],
        ["active"] = ["shutdown_requested", "deleting"],
        ["shutdown_requested"] = ["deleting"],
        ["deleting"] = ["deleted"],
        ["deleted"] = [],
        ["failed"] = [],
        ["orphaned"] = [],
    };

    public static object? StripLegacyRuntimeStateMemberFields(object? member)
    {
        if (member is not Dictionary<string, object?> dictionary)
        {
            return member;
        }

        var clone = new Dictionary<string, object?>(dictionary, StringComparer.Ordinal);
        clone.Remove("delegateTaskCallsUsed");
        return clone;
    }

    public static object? StripLegacyRuntimeStateFields(object? rawState)
    {
        if (rawState is not Dictionary<string, object?> dictionary)
        {
            return rawState;
        }

        if (dictionary.TryGetValue("members", out var members) && members is List<object?> list)
        {
            return new Dictionary<string, object?>(dictionary, StringComparer.Ordinal)
            {
                ["members"] = list.Select(StripLegacyRuntimeStateMemberFields).ToList(),
            };
        }

        return rawState;
    }

    public static RuntimeState ValidateRuntimeState(object? rawState, string teamRunId = "<unknown>")
    {
        var parsedRuntimeState = RuntimeStateSchema.SafeParse(StripLegacyRuntimeStateFields(rawState));
        if (!parsedRuntimeState.Success || parsedRuntimeState.Data is null)
        {
            throw new RuntimeStateError($"runtime state invalid for {teamRunId}: {parsedRuntimeState.Error?.Message}", "invalid_runtime_state");
        }

        return parsedRuntimeState.Data;
    }

    public static bool IsValidRuntimeTransition(string fromStatus, string toStatus)
    {
        return fromStatus == toStatus || toStatus == "orphaned" || AllowedRuntimeTransitions[fromStatus].Contains(toStatus);
    }

    public static RuntimeState CreateRuntimeStateFromSpec(TeamSpec spec, CreateRuntimeStateOptions options)
    {
        return ValidateRuntimeState(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["version"] = 1,
            ["teamRunId"] = options.TeamRunId,
            ["teamName"] = spec.Name,
            ["specSource"] = options.SpecSource,
            ["createdAt"] = options.CreatedAt ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            ["status"] = "creating",
            ["leadSessionId"] = options.LeadSessionId,
            ["members"] = spec.Members.Select(member => (object?)new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["name"] = member.Name,
                ["agentType"] = spec.LeadAgentId == member.Name ? "leader" : "general-purpose",
                ["status"] = "pending",
                ["color"] = member.Color,
                ["worktreePath"] = member.WorktreePath,
                ["lastInjectedTurnMarker"] = null,
                ["pendingInjectedMessageIds"] = new List<string>(),
            }).ToList(),
            ["shutdownRequests"] = new List<object?>(),
            ["bounds"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["maxMembers"] = new RuntimeBounds().MaxMembers,
                ["maxParallelMembers"] = options.Bounds?.MaxParallelMembers ?? new RuntimeBounds().MaxParallelMembers,
                ["maxMessagesPerRun"] = options.Bounds?.MaxMessagesPerRun ?? new RuntimeBounds().MaxMessagesPerRun,
                ["maxWallClockMinutes"] = options.Bounds?.MaxWallClockMinutes ?? new RuntimeBounds().MaxWallClockMinutes,
                ["maxMemberTurns"] = options.Bounds?.MaxMemberTurns ?? new RuntimeBounds().MaxMemberTurns,
            },
        }, options.TeamRunId);
    }

    public static RuntimeState TransitionRuntimeStatePure(RuntimeState runtimeState, Func<RuntimeState, RuntimeState> transition)
    {
        var nextRuntimeState = ValidateRuntimeState(transition(runtimeState), runtimeState.TeamRunId);
        if (!IsValidRuntimeTransition(runtimeState.Status, nextRuntimeState.Status))
        {
            throw new InvalidTransitionError(runtimeState.Status, nextRuntimeState.Status);
        }

        return nextRuntimeState;
    }

    public static readonly HashSet<string> DeletableMemberStatuses = ["completed", "shutdown_approved", "errored"];

    public static RuntimeStateMember GetRuntimeMember(RuntimeState runtimeState, string memberName)
    {
        return runtimeState.Members.FirstOrDefault(candidate => candidate.Name == memberName) ?? throw new InvalidOperationException($"unknown member '{memberName}'");
    }

    public static string GetLeadMemberName(RuntimeState runtimeState)
    {
        return runtimeState.Members.FirstOrDefault(member => member.AgentType == "leader")?.Name ?? throw new InvalidOperationException($"team '{runtimeState.TeamRunId}' is missing a lead member");
    }

    public static SendContext CreateSendContext(RuntimeState runtimeState, string senderName)
    {
        _ = GetRuntimeMember(runtimeState, senderName);
        return new SendContext(runtimeState.Members.Select(member => member.Name).ToList(), runtimeState.Members.First(member => member.Name == senderName).AgentType == "leader");
    }

    public static int FindLatestShutdownRequestIndex(RuntimeState runtimeState, string memberName, string? requesterName = null)
    {
        for (var index = runtimeState.ShutdownRequests.Count - 1; index >= 0; index -= 1)
        {
            var shutdownRequest = runtimeState.ShutdownRequests[index];
            if (shutdownRequest.MemberId != memberName) continue;
            if (requesterName is not null && shutdownRequest.RequesterName != requesterName) continue;
            return index;
        }

        return -1;
    }

    public static Message CreateShutdownMessage(CreateShutdownMessageInput input)
    {
        return new Message
        {
            Version = 1,
            MessageId = input.MessageId,
            From = input.From,
            To = input.To,
            Kind = input.Kind,
            Body = input.Body,
            Timestamp = input.Timestamp ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        };
    }

    public static RuntimeState MarkMessagesPendingForMember(RuntimeState runtimeState, string memberName, IReadOnlyList<string> messageIds, string? turnMarker = null)
    {
        return ValidateRuntimeState(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["version"] = runtimeState.Version,
            ["teamRunId"] = runtimeState.TeamRunId,
            ["teamName"] = runtimeState.TeamName,
            ["specSource"] = runtimeState.SpecSource,
            ["createdAt"] = runtimeState.CreatedAt,
            ["status"] = runtimeState.Status,
            ["leadSessionId"] = runtimeState.LeadSessionId,
            ["tmuxLayout"] = runtimeState.TmuxLayout is null ? null : new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["ownedSession"] = runtimeState.TmuxLayout.OwnedSession,
                ["targetSessionId"] = runtimeState.TmuxLayout.TargetSessionId,
                ["focusWindowId"] = runtimeState.TmuxLayout.FocusWindowId,
                ["gridWindowId"] = runtimeState.TmuxLayout.GridWindowId,
            },
            ["members"] = runtimeState.Members.Select(member => (object?)(member.Name == memberName
                ? new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["name"] = member.Name,
                    ["sessionId"] = member.SessionId,
                    ["tmuxPaneId"] = member.TmuxPaneId,
                    ["tmuxGridPaneId"] = member.TmuxGridPaneId,
                    ["agentType"] = member.AgentType,
                    ["subagent_type"] = member.SubagentType,
                    ["category"] = member.Category,
                    ["model"] = member.Model is null ? null : new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["providerID"] = member.Model.ProviderId,
                        ["modelID"] = member.Model.ModelId,
                        ["variant"] = member.Model.Variant,
                        ["reasoningEffort"] = member.Model.ReasoningEffort,
                        ["temperature"] = member.Model.Temperature,
                        ["top_p"] = member.Model.TopP,
                        ["maxTokens"] = member.Model.MaxTokens,
                        ["thinking"] = member.Model.Thinking is null ? null : new Dictionary<string, object?>(StringComparer.Ordinal)
                        {
                            ["type"] = member.Model.Thinking.Type,
                            ["budgetTokens"] = member.Model.Thinking.BudgetTokens,
                        },
                    },
                    ["status"] = member.Status,
                    ["color"] = member.Color,
                    ["worktreePath"] = member.WorktreePath,
                    ["lastInjectedTurnMarker"] = turnMarker ?? member.LastInjectedTurnMarker,
                    ["pendingInjectedMessageIds"] = member.PendingInjectedMessageIds.Concat(messageIds).Distinct(StringComparer.Ordinal).ToList(),
                }
                : new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["name"] = member.Name,
                    ["sessionId"] = member.SessionId,
                    ["tmuxPaneId"] = member.TmuxPaneId,
                    ["tmuxGridPaneId"] = member.TmuxGridPaneId,
                    ["agentType"] = member.AgentType,
                    ["subagent_type"] = member.SubagentType,
                    ["category"] = member.Category,
                    ["model"] = member.Model is null ? null : new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["providerID"] = member.Model.ProviderId,
                        ["modelID"] = member.Model.ModelId,
                        ["variant"] = member.Model.Variant,
                        ["reasoningEffort"] = member.Model.ReasoningEffort,
                        ["temperature"] = member.Model.Temperature,
                        ["top_p"] = member.Model.TopP,
                        ["maxTokens"] = member.Model.MaxTokens,
                        ["thinking"] = member.Model.Thinking is null ? null : new Dictionary<string, object?>(StringComparer.Ordinal)
                        {
                            ["type"] = member.Model.Thinking.Type,
                            ["budgetTokens"] = member.Model.Thinking.BudgetTokens,
                        },
                    },
                    ["status"] = member.Status,
                    ["color"] = member.Color,
                    ["worktreePath"] = member.WorktreePath,
                    ["lastInjectedTurnMarker"] = member.LastInjectedTurnMarker,
                    ["pendingInjectedMessageIds"] = member.PendingInjectedMessageIds,
                })).ToList(),
            ["shutdownRequests"] = runtimeState.ShutdownRequests.Select(request => (object?)new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["memberId"] = request.MemberId,
                ["requesterName"] = request.RequesterName,
                ["requestedAt"] = request.RequestedAt,
                ["approvedAt"] = request.ApprovedAt,
                ["rejectedReason"] = request.RejectedReason,
                ["rejectedAt"] = request.RejectedAt,
            }).ToList(),
            ["bounds"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["maxMembers"] = runtimeState.Bounds.MaxMembers,
                ["maxParallelMembers"] = runtimeState.Bounds.MaxParallelMembers,
                ["maxMessagesPerRun"] = runtimeState.Bounds.MaxMessagesPerRun,
                ["maxWallClockMinutes"] = runtimeState.Bounds.MaxWallClockMinutes,
                ["maxMemberTurns"] = runtimeState.Bounds.MaxMemberTurns,
            },
        }, runtimeState.TeamRunId);
    }

    public static RuntimeState ClearPendingMessagesForMember(RuntimeState runtimeState, string memberName, IReadOnlyList<string> messageIds)
    {
        var messageIdSet = new HashSet<string>(messageIds, StringComparer.Ordinal);
        return ValidateRuntimeState(runtimeState with
        {
            Members = runtimeState.Members.Select(member => member.Name == memberName
                ? member with { PendingInjectedMessageIds = member.PendingInjectedMessageIds.Where(messageId => !messageIdSet.Contains(messageId)).ToList() }
                : member).ToList(),
        }, runtimeState.TeamRunId);
    }
}

public sealed record CreateRuntimeStateOptions
{
    public required string TeamRunId { get; init; }

    public long? CreatedAt { get; init; }

    public string? LeadSessionId { get; init; }

    public required string SpecSource { get; init; }

    public RuntimeBounds? Bounds { get; init; }
}

public sealed record SendContext
{
    public SendContext(List<string> activeMembers, bool isLead)
    {
        ActiveMembers = activeMembers;
        IsLead = isLead;
    }

    public bool IsLead { get; }

    public List<string> ActiveMembers { get; }

    public HashSet<string>? ReservedRecipients { get; init; }
}

public sealed record CreateShutdownMessageInput
{
    public required string MessageId { get; init; }

    public required string From { get; init; }

    public required string To { get; init; }

    public required string Kind { get; init; }

    public required string Body { get; init; }

    public long? Timestamp { get; init; }
}

public static class RuntimeStateSchema
{
    public static RuntimeState Parse(object? input)
    {
        var result = SafeParse(input);
        if (!result.Success || result.Data is null)
        {
            throw new SchemaValidationException(result.Error?.Message ?? "Invalid runtime state.", result.Error?.Issues ?? []);
        }

        return result.Data;
    }

    public static SafeParseResult<RuntimeState> SafeParse(object? input)
    {
        try
        {
            return new(true, ParseRuntimeStateDto(input), null);
        }
        catch (SchemaValidationException exception)
        {
            return new(false, default, new ValidationError(exception.Issues, exception.Message));
        }
    }

    private static RuntimeState ParseRuntimeStateDto(object? input)
    {
        var element = JsonHelpers.ToElement(input);
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new SchemaValidationException("Runtime state must be an object", [new ValidationIssue("<root>", "Runtime state must be an object")]);
        }

        var issues = new List<ValidationIssue>();
        var teamRunId = JsonHelpers.GetString(element, "teamRunId");
        var teamName = JsonHelpers.GetString(element, "teamName");
        var specSource = JsonHelpers.GetString(element, "specSource");
        var createdAt = JsonHelpers.GetLong(element, "createdAt");
        var status = JsonHelpers.GetString(element, "status");

        if (string.IsNullOrWhiteSpace(teamRunId)) issues.Add(new ValidationIssue("teamRunId", "teamRunId is required."));
        if (string.IsNullOrWhiteSpace(teamName)) issues.Add(new ValidationIssue("teamName", "teamName is required."));
        if (string.IsNullOrWhiteSpace(specSource) || !new[] { "project", "user" }.Contains(specSource, StringComparer.Ordinal)) issues.Add(new ValidationIssue("specSource", "specSource must be 'project' or 'user'."));
        if (createdAt is null or <= 0) issues.Add(new ValidationIssue("createdAt", "createdAt must be positive."));
        if (string.IsNullOrWhiteSpace(status) || !new[] { "creating", "active", "shutdown_requested", "deleting", "deleted", "failed", "orphaned" }.Contains(status, StringComparer.Ordinal)) issues.Add(new ValidationIssue("status", "Invalid runtime state status."));

        var membersValue = JsonHelpers.GetObjectList(element, "members");
        if (membersValue is null)
        {
            issues.Add(new ValidationIssue("members", "members are required."));
        }

        if (issues.Count > 0)
        {
            throw new SchemaValidationException($"Runtime state validation failed: {issues[0].Message}", issues);
        }

        var members = new List<RuntimeStateMember>();
        foreach (var member in membersValue ?? new List<object?>())
        {
            members.Add(ParseRuntimeMember(JsonHelpers.ToElement(member)));
        }

        var shutdownRequests = new List<ShutdownRequest>();
        if (JsonHelpers.TryGetProperty(element, "shutdownRequests", out var shutdownElement) && shutdownElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var request in shutdownElement.EnumerateArray())
            {
                shutdownRequests.Add(ParseShutdownRequest(request));
            }
        }

        var tmuxLayout = JsonHelpers.GetObjectDictionary(element, "tmuxLayout") is { } layoutDict
            ? new RuntimeStateTmuxLayout
            {
                OwnedSession = layoutDict.TryGetValue("ownedSession", out var ownedSession) && ownedSession is bool owned,
                TargetSessionId = layoutDict.TryGetValue("targetSessionId", out var targetSessionId) && targetSessionId is string target ? target : string.Empty,
                FocusWindowId = layoutDict.TryGetValue("focusWindowId", out var focusWindowId) && focusWindowId is string focus ? focus : null,
                GridWindowId = layoutDict.TryGetValue("gridWindowId", out var gridWindowId) && gridWindowId is string grid ? grid : null,
            }
            : null;

        var bounds = JsonHelpers.GetObjectDictionary(element, "bounds") is { } boundsDict
            ? new RuntimeBounds
            {
                MaxMembers = Convert.ToInt32(boundsDict.GetValueOrDefault("maxMembers") ?? new RuntimeBounds().MaxMembers),
                MaxParallelMembers = Convert.ToInt32(boundsDict.GetValueOrDefault("maxParallelMembers") ?? new RuntimeBounds().MaxParallelMembers),
                MaxMessagesPerRun = Convert.ToInt32(boundsDict.GetValueOrDefault("maxMessagesPerRun") ?? new RuntimeBounds().MaxMessagesPerRun),
                MaxWallClockMinutes = Convert.ToInt32(boundsDict.GetValueOrDefault("maxWallClockMinutes") ?? new RuntimeBounds().MaxWallClockMinutes),
                MaxMemberTurns = Convert.ToInt32(boundsDict.GetValueOrDefault("maxMemberTurns") ?? new RuntimeBounds().MaxMemberTurns),
            }
            : new RuntimeBounds();

        return new RuntimeState
        {
            Version = (int)(JsonHelpers.GetLong(element, "version") ?? 1),
            TeamRunId = teamRunId!,
            TeamName = teamName!,
            SpecSource = specSource!,
            CreatedAt = createdAt!.Value,
            Status = status!,
            LeadSessionId = JsonHelpers.GetString(element, "leadSessionId"),
            TmuxLayout = tmuxLayout,
            Members = members,
            ShutdownRequests = shutdownRequests,
            Bounds = bounds,
        };
    }

    private static RuntimeStateMember ParseRuntimeMember(JsonElement member)
    {
        var issues = new List<ValidationIssue>();
        var name = JsonHelpers.GetString(member, "name");
        var agentType = JsonHelpers.GetString(member, "agentType");
        var status = JsonHelpers.GetString(member, "status");
        if (string.IsNullOrWhiteSpace(name)) issues.Add(new ValidationIssue("name", "member name is required."));
        if (string.IsNullOrWhiteSpace(agentType) || !new[] { "leader", "general-purpose" }.Contains(agentType, StringComparer.Ordinal)) issues.Add(new ValidationIssue("agentType", "agentType is invalid."));
        if (string.IsNullOrWhiteSpace(status) || !new[] { "pending", "running", "idle", "errored", "completed", "shutdown_approved" }.Contains(status, StringComparer.Ordinal)) issues.Add(new ValidationIssue("status", "member status is invalid."));
        if (issues.Count > 0) throw new SchemaValidationException("Invalid runtime member.", issues);

        return new RuntimeStateMember
        {
            Name = name!,
            SessionId = JsonHelpers.GetString(member, "sessionId"),
            TmuxPaneId = JsonHelpers.GetString(member, "tmuxPaneId"),
            TmuxGridPaneId = JsonHelpers.GetString(member, "tmuxGridPaneId"),
            AgentType = agentType!,
            SubagentType = JsonHelpers.GetString(member, "subagent_type") ?? JsonHelpers.GetString(member, "subagentType"),
            Category = JsonHelpers.GetString(member, "category"),
            Model = JsonHelpers.GetObjectDictionary(member, "model") is { } modelDict ? new RuntimeStateMemberModel
            {
                ProviderId = modelDict.TryGetValue("providerID", out var providerId) && providerId is string provider ? provider : string.Empty,
                ModelId = modelDict.TryGetValue("modelID", out var modelId) && modelId is string model ? model : string.Empty,
                Variant = modelDict.TryGetValue("variant", out var variant) && variant is string variantText ? variantText : null,
                ReasoningEffort = modelDict.TryGetValue("reasoningEffort", out var reasoningEffort) && reasoningEffort is string effort ? effort : null,
                Temperature = modelDict.TryGetValue("temperature", out var temperature) ? Convert.ToDouble(temperature) : null,
                TopP = modelDict.TryGetValue("top_p", out var topP) ? Convert.ToDouble(topP) : null,
                MaxTokens = modelDict.TryGetValue("maxTokens", out var maxTokens) ? Convert.ToDouble(maxTokens) : null,
                Thinking = modelDict.TryGetValue("thinking", out var thinking) && thinking is Dictionary<string, object?> thinkingDict ? new RuntimeStateThinking
                {
                    Type = thinkingDict.TryGetValue("type", out var type) && type is string typeText ? typeText : "disabled",
                    BudgetTokens = thinkingDict.TryGetValue("budgetTokens", out var budgetTokens) && budgetTokens is not null ? Convert.ToInt32(budgetTokens) : null,
                } : null,
            } : null,
            Status = status!,
            Color = JsonHelpers.GetString(member, "color"),
            WorktreePath = JsonHelpers.GetString(member, "worktreePath"),
            LastInjectedTurnMarker = JsonHelpers.GetString(member, "lastInjectedTurnMarker"),
            PendingInjectedMessageIds = JsonHelpers.GetStringList(member, "pendingInjectedMessageIds") ?? [],
        };
    }

    private static ShutdownRequest ParseShutdownRequest(JsonElement request)
    {
        return new ShutdownRequest
        {
            MemberId = JsonHelpers.GetString(request, "memberId") ?? string.Empty,
            RequesterName = JsonHelpers.GetString(request, "requesterName") ?? string.Empty,
            RequestedAt = JsonHelpers.GetLong(request, "requestedAt") ?? 0,
            ApprovedAt = JsonHelpers.GetLong(request, "approvedAt"),
            RejectedReason = JsonHelpers.GetString(request, "rejectedReason"),
            RejectedAt = JsonHelpers.GetLong(request, "rejectedAt"),
        };
    }
}
