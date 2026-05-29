using System.Text.Json;

namespace Lfe.AstGrepMcp.Tests;

public sealed class McpServerTests
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private static Task<string?> Handle(object request, AstGrepMcpOptions? options = null)
    {
        var json = JsonSerializer.Serialize(request, JsonOpts);
        return McpServer.HandleRequestAsync(json, options);
    }

    private static Task<string?> Handle(string json, AstGrepMcpOptions? options = null)
        => McpServer.HandleRequestAsync(json, options);

    [Fact]
    public async Task Initialize_ReturnsCapabilities()
    {
        var response = await Handle(new { jsonrpc = "2.0", id = 1, method = "initialize", @params = new { protocolVersion = "2024-11-05" } });
        Assert.Contains("\"tools\"", response);
        Assert.Contains("\"listChanged\":false", response);
        Assert.Contains("\"ast_grep\"", response);
        Assert.Contains("\"0.1.0\"", response);
        Assert.Contains("\"2024-11-05\"", response);
    }

    [Fact]
    public async Task ToolsList_ReturnsSearchAndReplace()
    {
        var response = await Handle(new { jsonrpc = "2.0", id = "tools", method = "tools/list" });
        Assert.NotNull(response);
        Assert.Contains("\"search\"", response);
        Assert.Contains("\"replace\"", response);
    }

    [Fact]
    public async Task ToolsList_SearchDescriptionContainsGuidance()
    {
        var response = await Handle(new { jsonrpc = "2.0", id = "tools", method = "tools/list" });
        Assert.NotNull(response);
        Assert.Contains("This is NOT regex", response);
        Assert.Contains("Meta-variables", response);
    }

    [Fact]
    public async Task NotificationsInitialized_ReturnsNull()
    {
        var response = await Handle(new { jsonrpc = "2.0", id = "init", method = "notifications/initialized" });
        Assert.Null(response);
    }

    [Fact]
    public async Task Ping_ReturnsEmptyResult()
    {
        var response = await Handle(new { jsonrpc = "2.0", id = "ping", method = "ping" });
        Assert.NotNull(response);
        Assert.Contains("\"id\":\"ping\"", response);
    }

    [Fact]
    public async Task UnknownMethod_ReturnsMethodNotFound()
    {
        var response = await Handle(new { jsonrpc = "2.0", id = "missing", method = "does/not-exist" });
        Assert.NotNull(response);
        Assert.Contains("-32601", response);
        Assert.Contains("Method not found", response);
    }

    [Fact]
    public async Task InvalidJson_ReturnsParseError()
    {
        var response = await Handle("{invalid json}");
        Assert.NotNull(response);
        Assert.Contains("-32700", response);
        Assert.Contains("Parse error", response);
    }

    [Fact]
    public async Task NullInput_ReturnsInvalidRequest()
    {
        var response = await Handle("null");
        Assert.NotNull(response);
        Assert.Contains("-32600", response);
        Assert.Contains("Invalid Request", response);
    }

    [Fact]
    public async Task ToolCall_MissingName_ReturnsError()
    {
        var response = await Handle(new { jsonrpc = "2.0", id = "missing-name", method = "tools/call", @params = new { } });
        Assert.NotNull(response);
        Assert.Contains("-32602", response);
        Assert.Contains("params.name", response);
    }

    [Fact]
    public async Task Search_WithoutDeps_ReturnsNoDepsError()
    {
        var response = await Handle(new
        {
            jsonrpc = "2.0",
            id = "search",
            method = "tools/call",
            @params = new { name = "search", arguments = new { pattern = "console.log($$$)", lang = "typescript" } }
        });
        Assert.NotNull(response);
        Assert.Contains("No SgRunnerDeps", response);
    }

    [Fact]
    public async Task Replace_WithoutDeps_ReturnsNoDepsError()
    {
        var response = await Handle(new
        {
            jsonrpc = "2.0",
            id = "replace",
            method = "tools/call",
            @params = new { name = "replace", arguments = new { pattern = "console.log($MSG)", rewrite = "logger.info($MSG)", lang = "typescript" } }
        });
        Assert.NotNull(response);
        Assert.Contains("No SgRunnerDeps", response);
    }

    [Fact]
    public void ParseSearchArgs_ParsesCorrectly()
    {
        var json = """{"pattern":"console.log($$$)","lang":"typescript"}""";
        using var doc = JsonDocument.Parse(json);
        var result = McpServer.ParseSearchArgs(doc.RootElement, "/tmp/workspace");
        Assert.Equal("console.log($$$)", result.Pattern);
        Assert.Equal(Lfe.AstGrep.CliLanguage.Typescript, result.Language);
        Assert.Equal("/tmp/workspace", result.Cwd);
        Assert.Equal(["."], result.Paths);
    }

    [Fact]
    public void ParseReplaceArgs_DefaultsDryRunTrue()
    {
        var json = """{"pattern":"console.log($MSG)","rewrite":"logger.info($MSG)","lang":"typescript"}""";
        using var doc = JsonDocument.Parse(json);
        var (opts, dryRun) = McpServer.ParseReplaceArgs(doc.RootElement, "/tmp/workspace");
        Assert.True(dryRun);
        Assert.False(opts.UpdateAll);
        Assert.Equal("console.log($MSG)", opts.Pattern);
        Assert.Equal("logger.info($MSG)", opts.Rewrite);
    }

    [Fact]
    public void ParseReplaceArgs_DryRunFalse_SetsUpdateAll()
    {
        var json = """{"pattern":"a","rewrite":"b","lang":"python","dryRun":false}""";
        using var doc = JsonDocument.Parse(json);
        var (opts, dryRun) = McpServer.ParseReplaceArgs(doc.RootElement, "/tmp");
        Assert.False(dryRun);
        Assert.True(opts.UpdateAll);
    }

    [Fact]
    public void ParseSearchArgs_ThrowsOnMissingPattern()
    {
        var json = """{"lang":"typescript"}""";
        using var doc = JsonDocument.Parse(json);
        Assert.Throws<ArgumentException>(() => McpServer.ParseSearchArgs(doc.RootElement, "/tmp"));
    }
}
