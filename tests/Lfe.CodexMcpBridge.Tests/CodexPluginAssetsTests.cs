using System.Text.Json;
using Lfe.CodexMcpBridge;
using Xunit;

namespace Lfe.CodexMcpBridge.Tests;

public sealed class CodexPluginAssetsTests
{
    [Fact]
    public void CreateMarketplaceJson_UsesCodexMarketplaceShape()
    {
        using var doc = JsonDocument.Parse(CodexPluginAssets.CreateMarketplaceJson());
        Assert.Equal("lfe-codex-plugins", doc.RootElement.GetProperty("name").GetString());
        var plugin = doc.RootElement.GetProperty("plugins")[0];
        Assert.Equal("lfe", plugin.GetProperty("name").GetString());
        Assert.Equal("./plugins/lfe", plugin.GetProperty("source").GetString());
        Assert.Equal("AVAILABLE", plugin.GetProperty("policy").GetProperty("installation").GetString());
    }

    [Fact]
    public void CreatePluginJson_DeclaresMcpServersAndInterface()
    {
        using var doc = JsonDocument.Parse(CodexPluginAssets.CreatePluginJson());
        Assert.Equal("lfe", doc.RootElement.GetProperty("name").GetString());
        Assert.Equal("./.mcp.json", doc.RootElement.GetProperty("mcpServers").GetString());
        Assert.Equal("Lfe", doc.RootElement.GetProperty("interface").GetProperty("displayName").GetString());
    }

    [Fact]
    public void CreateMcpJson_DeclaresLfeCodexServer()
    {
        using var doc = JsonDocument.Parse(CodexPluginAssets.CreateMcpJson());
        var server = doc.RootElement.GetProperty("mcpServers").GetProperty("lfe-codex");
        Assert.Equal("dotnet", server.GetProperty("command").GetString());
        Assert.Equal("Lfe.CodexMcpBridge.dll", server.GetProperty("args")[0].GetString());
        Assert.Equal("mcp", server.GetProperty("args")[1].GetString());
        Assert.Equal(".", server.GetProperty("cwd").GetString());
    }
}
