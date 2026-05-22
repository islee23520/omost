using Xunit;

namespace Omodot.CodexAdapter.Tests;

public sealed class CodexJsonlParserTests
{
    private readonly CodexJsonlParser _parser = new();

    [Fact]
    public void ParseLine_ValidMessage_ReturnsMessageEvent()
    {
        var line = """{"type":"message","session_id":"s1","role":"assistant","content":"hello"}""";
        var result = _parser.ParseLine(line);
        Assert.NotNull(result);
        Assert.Equal(CodexAdapterEventType.Message, result.EventType);
        Assert.Equal("s1", result.SessionId);
        Assert.Equal("assistant", result.Role);
        Assert.Equal("hello", result.Content);
    }

    [Fact]
    public void ParseLine_IdleType_ReturnsIdleEvent()
    {
        var line = """{"type":"idle","session_id":"s1"}""";
        var result = _parser.ParseLine(line);
        Assert.NotNull(result);
        Assert.Equal(CodexAdapterEventType.Idle, result.EventType);
    }

    [Fact]
    public void ParseLine_CompletedType_ReturnsCompletedEvent()
    {
        var line = """{"type":"completed","session_id":"s1"}""";
        var result = _parser.ParseLine(line);
        Assert.NotNull(result);
        Assert.Equal(CodexAdapterEventType.Completed, result.EventType);
    }

    [Fact]
    public void ParseLine_ErrorType_ReturnsErrorEvent()
    {
        var line = """{"type":"error","session_id":"s1","error":"something went wrong"}""";
        var result = _parser.ParseLine(line);
        Assert.NotNull(result);
        Assert.Equal(CodexAdapterEventType.Error, result.EventType);
        Assert.Equal("something went wrong", result.Error);
    }

    [Fact]
    public void ParseLine_ThreadMessageFormat_ReturnsMessageEvent()
    {
        var line = """{"type":"thread.message","session_id":"s2","role":"user","content":"input"}""";
        var result = _parser.ParseLine(line);
        Assert.NotNull(result);
        Assert.Equal(CodexAdapterEventType.Message, result.EventType);
    }

    [Fact]
    public void ParseLine_EmptyLine_ReturnsNull()
    {
        Assert.Null(_parser.ParseLine(""));
        Assert.Null(_parser.ParseLine("   "));
        Assert.Null(_parser.ParseLine("\t"));
    }

    [Fact]
    public void ParseLine_MalformedJson_ReturnsErrorEvent()
    {
        var result = _parser.ParseLine("{invalid json");
        Assert.NotNull(result);
        Assert.Equal(CodexAdapterEventType.Error, result.EventType);
        Assert.Contains("JSONL parse error", result.Error);
    }

    [Fact]
    public void ParseLine_UnknownType_ReturnsUnknownEvent()
    {
        var line = """{"type":"custom_event","session_id":"s1"}""";
        var result = _parser.ParseLine(line);
        Assert.NotNull(result);
        Assert.Equal(CodexAdapterEventType.Unknown, result.EventType);
    }

    [Fact]
    public void ParseLine_StatusFieldIdle_ReturnsIdleEvent()
    {
        var line = """{"type":"custom","session_id":"s1","status":"idle"}""";
        var result = _parser.ParseLine(line);
        Assert.NotNull(result);
        Assert.Equal(CodexAdapterEventType.Idle, result.EventType);
    }

    [Fact]
    public void ParseStream_MultipleLines_ReturnsAllEvents()
    {
        var jsonl = """
            {"type":"message","role":"assistant","content":"hi"}
            {"type":"idle","session_id":"s1"}
            {"type":"completed","session_id":"s1"}
            """;
        var results = _parser.ParseStream(jsonl);
        Assert.Equal(3, results.Count);
        Assert.Equal(CodexAdapterEventType.Message, results[0].EventType);
        Assert.Equal(CodexAdapterEventType.Idle, results[1].EventType);
        Assert.Equal(CodexAdapterEventType.Completed, results[2].EventType);
    }

    [Fact]
    public void ParseStream_WithEmptyLines_SkipsEmptyLines()
    {
        var jsonl = """
            {"type":"message","role":"assistant","content":"hi"}

            {"type":"completed","session_id":"s1"}
            """;
        var results = _parser.ParseStream(jsonl);
        Assert.Equal(2, results.Count);
    }

    [Fact]
    public void ParseLine_ValidRole_ReturnsRoleUnchanged()
    {
        foreach (var role in new[] { "user", "assistant", "system", "tool" })
        {
            var line = $$"""{"type":"message","role":"{{role}}","content":"test"}""";
            var result = _parser.ParseLine(line);
            Assert.NotNull(result);
            Assert.Equal(role, result.Role);
        }
    }

    [Fact]
    public void ParseLine_InvalidRole_ReturnsNullRole()
    {
        var line = """{"type":"message","role":"unknown_role","content":"test"}""";
        var result = _parser.ParseLine(line);
        Assert.NotNull(result);
        Assert.Null(result.Role);
    }

    [Fact]
    public void ParseLine_ExtensionData_PreservedInRawData()
    {
        var line = """{"type":"message","role":"assistant","content":"hi","custom_field":"custom_value"}""";
        var result = _parser.ParseLine(line);
        Assert.NotNull(result);
        Assert.NotNull(result.RawData);
    }

    [Fact]
    public void ParseLine_RealCodex_ThreadStarted_ReturnsStatusWithThreadId()
    {
        var line = """{"type":"thread.started","thread_id":"019e6475-6f1f-7010-b5c9-1c870fce95e0"}""";
        var result = _parser.ParseLine(line);
        Assert.NotNull(result);
        Assert.Equal(CodexAdapterEventType.Status, result.EventType);
        Assert.Equal("019e6475-6f1f-7010-b5c9-1c870fce95e0", result.SessionId);
        Assert.Equal("019e6475-6f1f-7010-b5c9-1c870fce95e0", result.ThreadId);
    }

    [Fact]
    public void ParseLine_RealCodex_TurnStarted_ReturnsStatus()
    {
        var line = """{"type":"turn.started"}""";
        var result = _parser.ParseLine(line);
        Assert.NotNull(result);
        Assert.Equal(CodexAdapterEventType.Status, result.EventType);
    }

    [Fact]
    public void ParseLine_RealCodex_ItemCompleted_AgentMessage_ReturnsMessage()
    {
        var line = """{"type":"item.completed","item":{"id":"item_0","type":"agent_message","text":"hello world"}}""";
        var result = _parser.ParseLine(line);
        Assert.NotNull(result);
        Assert.Equal(CodexAdapterEventType.Message, result.EventType);
        Assert.Equal("assistant", result.Role);
        Assert.Equal("hello world", result.Content);
        Assert.Equal("item_0", result.ItemId);
    }

    [Fact]
    public void ParseLine_RealCodex_TurnCompleted_ReturnsCompleted()
    {
        var line = """{"type":"turn.completed","usage":{"input_tokens":17165,"cached_input_tokens":3456}}""";
        var result = _parser.ParseLine(line);
        Assert.NotNull(result);
        Assert.Equal(CodexAdapterEventType.Completed, result.EventType);
    }

    [Fact]
    public void ParseStream_RealCodexFullSession_ParsesAllEvents()
    {
        var jsonl = """
            {"type":"thread.started","thread_id":"thread-abc"}
            {"type":"turn.started"}
            {"type":"item.completed","item":{"id":"item_0","type":"agent_message","text":"hello world"}}
            {"type":"turn.completed","usage":{"input_tokens":17165}}
            """;
        var results = _parser.ParseStream(jsonl);
        Assert.Equal(4, results.Count);

        Assert.Equal(CodexAdapterEventType.Status, results[0].EventType);
        Assert.Equal("thread-abc", results[0].SessionId);

        Assert.Equal(CodexAdapterEventType.Status, results[1].EventType);

        Assert.Equal(CodexAdapterEventType.Message, results[2].EventType);
        Assert.Equal("assistant", results[2].Role);
        Assert.Equal("hello world", results[2].Content);

        Assert.Equal(CodexAdapterEventType.Completed, results[3].EventType);
    }
}
