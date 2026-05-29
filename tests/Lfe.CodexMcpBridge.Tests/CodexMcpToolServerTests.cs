using Lfe.CodexAdapter;
using Lfe.CodexMcpBridge;
using Xunit;

namespace Lfe.CodexMcpBridge.Tests;

public sealed class CodexMcpToolServerTests
{
    private static CodexResolvedConfig CreateTestConfig() => new(
        BinaryPath: "/usr/bin/echo",
        WorkingDirectory: Path.GetTempPath(),
        TimeoutMs: 5000,
        EnvironmentOverrides: new Dictionary<string, string>(),
        SessionOptions: new CodexSessionOptions());

    [Fact]
    public void Constructor_WithValidConfig_CreatesInstance()
    {
        using var server = new CodexMcpToolServer(CreateTestConfig());
        Assert.NotNull(server);
    }

    [Fact]
    public void Constructor_NullConfig_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new CodexMcpToolServer(null!));
    }

    [Fact]
    public void ListTools_ReturnsFourTools()
    {
        using var server = new CodexMcpToolServer(CreateTestConfig());
        var tools = server.ListTools();
        Assert.Equal(4, tools.Count);
        Assert.True(tools.ContainsKey("codex_dispatch"));
        Assert.True(tools.ContainsKey("codex_read_status"));
        Assert.True(tools.ContainsKey("codex_read_messages"));
        Assert.True(tools.ContainsKey("codex_abort"));
    }

    [Fact]
    public void ListTools_AllDescriptionsAreNonEmpty()
    {
        using var server = new CodexMcpToolServer(CreateTestConfig());
        foreach (var (name, description) in server.ListTools())
        {
            Assert.False(string.IsNullOrWhiteSpace(description), $"Tool '{name}' has empty description");
        }
    }

    [Fact]
    public async Task DispatchAsync_NullPrompt_Throws()
    {
        using var server = new CodexMcpToolServer(CreateTestConfig());
        await Assert.ThrowsAsync<ArgumentNullException>(() => server.DispatchAsync(null!));
    }

    [Fact]
    public async Task DispatchAsync_EmptyPrompt_Throws()
    {
        using var server = new CodexMcpToolServer(CreateTestConfig());
        await Assert.ThrowsAsync<ArgumentException>(() => server.DispatchAsync(""));
    }

    [Fact]
    public async Task ReadStatusAsync_NullSessionId_Throws()
    {
        using var server = new CodexMcpToolServer(CreateTestConfig());
        await Assert.ThrowsAsync<ArgumentNullException>(() => server.ReadStatusAsync(null!));
    }

    [Fact]
    public async Task AbortAsync_NullSessionId_Throws()
    {
        using var server = new CodexMcpToolServer(CreateTestConfig());
        await Assert.ThrowsAsync<ArgumentNullException>(() => server.AbortAsync(null!));
    }

    [Fact]
    public async Task ReadStatusAsync_UnknownSession_ReturnsUnknown()
    {
        using var server = new CodexMcpToolServer(CreateTestConfig());
        var result = await server.ReadStatusAsync("nonexistent");
        Assert.Equal("unknown", result.Status);
        Assert.Equal("codex_read_status", result.ToolName);
    }

    [Fact]
    public async Task ReadMessagesAsync_UnknownSession_ReturnsEmpty()
    {
        using var server = new CodexMcpToolServer(CreateTestConfig());
        var result = await server.ReadMessagesAsync("nonexistent");
        Assert.Empty(result.Messages);
        Assert.Equal("codex_read_messages", result.ToolName);
    }

    [Fact]
    public async Task AbortAsync_UnknownSession_Succeeds()
    {
        using var server = new CodexMcpToolServer(CreateTestConfig());
        var result = await server.AbortAsync("nonexistent");
        Assert.True(result.Success);
        Assert.Equal("codex_abort", result.ToolName);
    }

    [Fact]
    public void RegisterEventListener_ReturnsUnsubscribeAction()
    {
        using var server = new CodexMcpToolServer(CreateTestConfig());
        var unsubscribe = server.RegisterEventListener(_ => { });
        Assert.NotNull(unsubscribe);
        unsubscribe();
    }
}
