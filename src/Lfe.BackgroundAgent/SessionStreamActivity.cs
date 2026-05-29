namespace Lfe.BackgroundAgent;

public sealed record StreamMessagePartState
{
    public string? Status { get; init; }
    public IReadOnlyDictionary<string, object?>? Input { get; init; }
}

public sealed record StreamMessagePartInfo
{
    public string? Id { get; init; }
    public string? SessionId { get; init; }
    public string? Type { get; init; }
    public string? Tool { get; init; }
    public IReadOnlyDictionary<string, object?>? Input { get; init; }
    public StreamMessagePartState? State { get; init; }
    public string? Field { get; init; }
    public DateTime? ActivityTime { get; init; }
}

public static class SessionStreamActivity
{
    public static StreamMessagePartInfo? ResolveMessagePartInfo(object? properties)
    {
        var props = ObjectRecord.AsRecord(properties);
        if (props is null)
        {
            return null;
        }

        var nestedPart = ObjectRecord.GetRecord(props, "part");
        return nestedPart is not null ? BuildPartInfo(nestedPart, props) : BuildPartInfo(props, null);
    }

    public static StreamMessagePartInfo? ResolveSessionNextPartInfo(string eventType, object? properties)
    {
        if (!eventType.StartsWith(BackgroundAgentConstants.SessionNextEventPrefix, StringComparison.Ordinal) ||
            !IsTrackedSessionNextActivityEvent(eventType))
        {
            return null;
        }

        var props = ObjectRecord.AsRecord(properties);
        var sessionId = ObjectRecord.GetString(props, "sessionID");
        if (props is null || string.IsNullOrEmpty(sessionId))
        {
            return null;
        }

        var input = ObjectRecord.GetRecord(props, "input");
        if (string.Equals(eventType, "session.next.tool.called", StringComparison.Ordinal))
        {
            return new StreamMessagePartInfo
            {
                Id = ObjectRecord.GetString(props, "callID"),
                SessionId = sessionId,
                Type = "tool",
                Tool = ObjectRecord.GetString(props, "tool"),
                Input = input,
                State = new StreamMessagePartState { Status = "running", Input = input },
                ActivityTime = ObjectRecord.GetDateTime(props, "timestamp"),
            };
        }

        var type = SessionNextType(eventType);
        return new StreamMessagePartInfo
        {
            Id = ObjectRecord.GetString(props, "callID"),
            SessionId = sessionId,
            Type = type,
            Field = eventType.EndsWith(".delta", StringComparison.Ordinal) ? type : null,
            ActivityTime = ObjectRecord.GetDateTime(props, "timestamp"),
        };
    }

    public static bool IsMessagePartForSession(StreamMessagePartInfo? partInfo, string sessionId)
    {
        return partInfo?.SessionId is null || string.Equals(partInfo.SessionId, sessionId, StringComparison.Ordinal);
    }

    public static bool HasOutputSignalFromPart(StreamMessagePartInfo? partInfo, string? sessionId = null)
    {
        if (partInfo is null)
        {
            return false;
        }

        if (partInfo.SessionId is not null && sessionId is not null && !string.Equals(partInfo.SessionId, sessionId, StringComparison.Ordinal))
        {
            return false;
        }

        if (partInfo.SessionId is null && sessionId is null)
        {
            return false;
        }

        return partInfo.Tool is not null ||
               partInfo.Type is "tool" or "tool_result" or "text" or "reasoning" ||
               partInfo.Field is "text" or "reasoning";
    }

    private static StreamMessagePartInfo BuildPartInfo(IReadOnlyDictionary<string, object?> source, IReadOnlyDictionary<string, object?>? fallback)
    {
        return new StreamMessagePartInfo
        {
            Id = ObjectRecord.GetString(source, "id") ?? ObjectRecord.GetString(source, "callID"),
            SessionId = ObjectRecord.GetString(source, "sessionID") ?? ObjectRecord.GetString(fallback, "sessionID"),
            Type = ObjectRecord.GetString(source, "type") ?? ObjectRecord.GetString(fallback, "type"),
            Tool = ObjectRecord.GetString(source, "tool") ?? ObjectRecord.GetString(fallback, "tool"),
            Input = ObjectRecord.GetRecord(source, "input") ?? ObjectRecord.GetRecord(fallback, "input"),
            State = ResolveState(source) ?? ResolveState(fallback),
            Field = ObjectRecord.GetString(source, "field") ?? ObjectRecord.GetString(fallback, "field"),
            ActivityTime = ObjectRecord.GetDateTime(source, "activityTime")
                ?? ObjectRecord.GetDateTime(source, "timestamp")
                ?? ObjectRecord.GetDateTime(fallback, "activityTime")
                ?? ObjectRecord.GetDateTime(fallback, "timestamp"),
        };
    }

    private static StreamMessagePartState? ResolveState(IReadOnlyDictionary<string, object?>? record)
    {
        var state = ObjectRecord.GetRecord(record, "state");
        return state is null
            ? null
            : new StreamMessagePartState
            {
                Status = ObjectRecord.GetString(state, "status"),
                Input = ObjectRecord.GetRecord(state, "input"),
            };
    }

    private static string? SessionNextType(string eventType)
    {
        if (eventType.StartsWith("session.next.text.", StringComparison.Ordinal)) return "text";
        if (eventType.StartsWith("session.next.reasoning.", StringComparison.Ordinal)) return "reasoning";
        if (eventType.StartsWith("session.next.tool.", StringComparison.Ordinal) && !string.Equals(eventType, "session.next.tool.called", StringComparison.Ordinal)) return "tool_result";
        return null;
    }

    private static bool IsTrackedSessionNextActivityEvent(string eventType)
    {
        return string.Equals(eventType, "session.next.synthetic", StringComparison.Ordinal) ||
               string.Equals(eventType, "session.next.retried", StringComparison.Ordinal) ||
               eventType.StartsWith("session.next.shell.", StringComparison.Ordinal) ||
               eventType.StartsWith("session.next.step.", StringComparison.Ordinal) ||
               eventType.StartsWith("session.next.text.", StringComparison.Ordinal) ||
               eventType.StartsWith("session.next.reasoning.", StringComparison.Ordinal) ||
               eventType.StartsWith("session.next.tool.", StringComparison.Ordinal) ||
               eventType.StartsWith("session.next.compaction.", StringComparison.Ordinal);
    }
}
