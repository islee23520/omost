using System.Text.Json;
using Lfe.CodexAdapter;
using Lfe.CodexMcpBridge;
using Xunit;

namespace Lfe.CodexMcpBridge.Tests;

public sealed class CodexMcpJsonRpcServerTests
{
    private static CodexResolvedConfig CreateTestConfig() => new(
        BinaryPath: "/usr/bin/echo",
        WorkingDirectory: Path.GetTempPath(),
        TimeoutMs: 5000,
        EnvironmentOverrides: new Dictionary<string, string>(),
        SessionOptions: new CodexSessionOptions());

    [Fact]
    public async Task Initialize_ReturnsMcpCapabilities()
    {
        using var server = new CodexMcpToolServer(CreateTestConfig());
        var response = await CodexMcpJsonRpcServer.HandleRequestAsync("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\"}", server);

        using var doc = JsonDocument.Parse(response!);
        Assert.Equal("2024-11-05", doc.RootElement.GetProperty("result").GetProperty("protocolVersion").GetString());
        Assert.Equal("lfe_codex", doc.RootElement.GetProperty("result").GetProperty("serverInfo").GetProperty("name").GetString());
    }

    [Fact]
    public async Task ToolsList_ReturnsFourTools()
    {
        using var server = new CodexMcpToolServer(CreateTestConfig());
        var response = await CodexMcpJsonRpcServer.HandleRequestAsync("{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"tools/list\"}", server);

        using var doc = JsonDocument.Parse(response!);
        var tools = doc.RootElement.GetProperty("result").GetProperty("tools");
        Assert.Equal(4, tools.GetArrayLength());
        Assert.Equal("codex_dispatch", tools[0].GetProperty("name").GetString());
    }

    [Fact]
    public async Task ToolsCall_ReadStatus_ReturnsTextContent()
    {
        using var server = new CodexMcpToolServer(CreateTestConfig());
        var request = "{\"jsonrpc\":\"2.0\",\"id\":3,\"method\":\"tools/call\",\"params\":{\"name\":\"codex_read_status\",\"arguments\":{\"sessionId\":\"missing\"}}}";
        var response = await CodexMcpJsonRpcServer.HandleRequestAsync(request, server);

        using var doc = JsonDocument.Parse(response!);
        var content = doc.RootElement.GetProperty("result").GetProperty("content");
        Assert.Equal("text", content[0].GetProperty("type").GetString());
        Assert.Contains("codex_read_status", content[0].GetProperty("text").GetString());
    }
}
