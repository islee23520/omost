using Xunit;
using Omodot.UlwHostContract;

namespace Omodot.CodexAdapter.E2eTests;

[Trait("Category", "E2E")]
public sealed class CodexE2eTests
{
    private static string? FindCodexBinary()
    {
        var envPath = Environment.GetEnvironmentVariable("CODEX_BINARY_PATH");
        if (!string.IsNullOrWhiteSpace(envPath) && File.Exists(envPath))
            return envPath;

        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in pathEnv.Split(Path.PathSeparator))
        {
            var candidate = Path.Combine(dir, "codex");
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }

    private static CodexAdapterOptions CreateOptions()
    {
        var binary = FindCodexBinary();
        if (binary is null)
            throw new InvalidOperationException("codex binary not found in PATH or CODEX_BINARY_PATH — skipping E2E tests");
        return new CodexAdapterOptions
        {
            CodexBinaryPath = binary,
            TimeoutMs = 30_000,
        };
    }

    [Fact]
    public async Task Dispatch_RealCodex_ReturnsAcceptedReceipt()
    {
        var options = CreateOptions();
        using var host = new CodexUlwHost(new CodexBinaryResolver().ResolveConfig(options));

        var receipt = await host.DispatchPromptAsync(
            new UlwPromptRequest("e2e-session-1", "Say exactly: hello world"));

        Assert.True(receipt.Accepted, $"Dispatch not accepted. SessionId={receipt.SessionId}, DispatchId={receipt.DispatchId}");
        Assert.Equal("e2e-session-1", receipt.SessionId);
        Assert.NotNull(receipt.DispatchId);
    }

    [Fact]
    public async Task ReadMessages_RealCodex_ReturnsAssistantMessage()
    {
        var options = CreateOptions();
        using var host = new CodexUlwHost(new CodexBinaryResolver().ResolveConfig(options));

        await host.DispatchPromptAsync(
            new UlwPromptRequest("e2e-session-2", "Say exactly: e2e test response"));

        var messages = await host.ReadMessagesAsync("e2e-session-2");
        Assert.NotEmpty(messages);
        Assert.Contains(messages, m => m.Role == "assistant");
        Assert.Contains(messages, m => m.Text.Contains("e2e test response", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ReadStatus_RealCodex_ReturnsCompletedStatus()
    {
        var options = CreateOptions();
        using var host = new CodexUlwHost(new CodexBinaryResolver().ResolveConfig(options));

        await host.DispatchPromptAsync(
            new UlwPromptRequest("e2e-session-3", "Say exactly: status test"));

        var status = await host.ReadStatusAsync("e2e-session-3");
        Assert.True(status is "completed" or "idle", $"Unexpected status: {status}");
    }

    [Fact]
    public async Task ReadTodos_RealCodex_ReturnsEmptyList()
    {
        var options = CreateOptions();
        using var host = new CodexUlwHost(new CodexBinaryResolver().ResolveConfig(options));

        await host.DispatchPromptAsync(
            new UlwPromptRequest("e2e-session-4", "Say exactly: todo test"));

        var todos = await host.ReadTodosAsync("e2e-session-4");
        Assert.Empty(todos);
    }

    [Fact]
    public async Task OnEvent_RealCodex_ForwardsEvents()
    {
        var options = CreateOptions();
        using var host = new CodexUlwHost(new CodexBinaryResolver().ResolveConfig(options));

        var receivedEvents = new List<UlwSessionEvent>();
        host.OnEvent(evt => receivedEvents.Add(evt));

        await host.DispatchPromptAsync(
            new UlwPromptRequest("e2e-session-5", "Say exactly: event test"));

        Assert.NotEmpty(receivedEvents);
    }
}

[Trait("Category", "E2E")]
public sealed class CodexContractParityTests
{
    [Fact]
    public void TS_IUlwHost_DispatchPrompt_ContractMatches()
    {
        // TS: dispatchPrompt(request) → { accepted, sessionID, dispatchID }
        // .NET: DispatchPromptAsync(request) → UlwPromptReceipt(Accepted, SessionId, DispatchId)
        Assert.True(typeof(CodexUlwHost).GetMethod("DispatchPromptAsync") is not null);
        Assert.True(typeof(UlwPromptReceipt).GetProperty("Accepted") is not null);
        Assert.True(typeof(UlwPromptReceipt).GetProperty("SessionId") is not null);
        Assert.True(typeof(UlwPromptReceipt).GetProperty("DispatchId") is not null);
    }

    [Fact]
    public void TS_IUlwHost_ReadMessages_ContractMatches()
    {
        // TS: readMessages(sessionID) → UlwMessage[]
        // .NET: ReadMessagesAsync(sessionId) → Task<IReadOnlyList<UlwMessage>>
        Assert.True(typeof(UlwMessage).GetProperty("Role") is not null);
        Assert.True(typeof(UlwMessage).GetProperty("Text") is not null);
    }

    [Fact]
    public void TS_IUlwHost_ReadTodos_ContractMatches()
    {
        // TS: readTodos(sessionID) → UlwTodo[]
        // .NET: ReadTodosAsync(sessionId) → Task<IReadOnlyList<UlwTodo>> (returns empty)
        Assert.True(typeof(UlwTodo).GetProperty("Content") is not null);
        Assert.True(typeof(UlwTodo).GetProperty("Status") is not null);
    }

    [Fact]
    public void TS_IUlwHost_ReadStatus_ContractMatches()
    {
        // TS: readStatus(sessionID) → string
        // .NET: ReadStatusAsync(sessionId) → Task<string>
        Assert.True(typeof(CodexUlwHost).GetMethod("ReadStatusAsync") is not null);
    }

    [Fact]
    public void TS_IUlwHost_Abort_ContractMatches()
    {
        // TS: abort(sessionID) → void
        // .NET: AbortAsync(sessionId) → Task
        Assert.True(typeof(CodexUlwHost).GetMethod("AbortAsync") is not null);
    }

    [Fact]
    public void TS_IUlwHost_OnEvent_ContractMatches()
    {
        // TS: onEvent(listener) → unsubscribe function
        // .NET: OnEvent(listener) → Action (unsubscribe)
        var method = typeof(CodexUlwHost).GetMethod("OnEvent");
        Assert.True(method is not null);
        Assert.Equal(typeof(Action), method.ReturnType);
    }

    [Fact]
    public void TS_UlwSessionEventTypes_MatchDotNet()
    {
        // TS: "idle" | "error" | "deleted" | "compacting" | "completed"
        // .NET: UlwSessionEventType enum
        Assert.True(Enum.IsDefined(typeof(UlwSessionEventType), UlwSessionEventType.Idle));
        Assert.True(Enum.IsDefined(typeof(UlwSessionEventType), UlwSessionEventType.Error));
        Assert.True(Enum.IsDefined(typeof(UlwSessionEventType), UlwSessionEventType.Deleted));
        Assert.True(Enum.IsDefined(typeof(UlwSessionEventType), UlwSessionEventType.Compacting));
        Assert.True(Enum.IsDefined(typeof(UlwSessionEventType), UlwSessionEventType.Completed));
    }
}
