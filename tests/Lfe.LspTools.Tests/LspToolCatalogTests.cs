using System.Text.Json;

using Lfe.LspTools;

namespace Lfe.LspTools.Tests;

public sealed class LspToolCatalogTests
{
    [Fact]
    public void ToolCatalogMatchesExpectedNamesAndAliases()
    {
        Assert.Equal(
            new[]
            {
                LspToolNames.Status,
                LspToolNames.Diagnostics,
                LspToolNames.GotoDefinition,
                LspToolNames.FindReferences,
                LspToolNames.Symbols,
                LspToolNames.PrepareRename,
                LspToolNames.Rename,
            },
            LspMcpToolCatalog.All.Select(tool => tool.Name).ToArray());

        Assert.Contains(LspMcpToolCatalog.All, tool => tool.Aliases?.Contains("lsp_status", StringComparer.Ordinal) == true);
        Assert.Contains(LspMcpToolCatalog.All, tool => tool.Aliases?.Contains("lsp_rename", StringComparer.Ordinal) == true);
    }

    [Fact]
    public void DiagnosticsAndSymbolsSchemasMatchTypeScriptDefinitions()
    {
        var diagnostics = LspMcpToolCatalog.All.Single(tool => tool.Name == LspToolNames.Diagnostics);
        var symbols = LspMcpToolCatalog.All.Single(tool => tool.Name == LspToolNames.Symbols);

        Assert.Equal(new[] { "filePath" }, diagnostics.InputSchema.Required);
        Assert.Equal(SeverityFilters.Values, diagnostics.InputSchema.Properties!["severity"].Enum);
        Assert.Equal(new[] { "filePath", "scope" }, symbols.InputSchema.Required);
        Assert.Equal(LspSymbolScopes.Values, symbols.InputSchema.Properties!["scope"].Enum);
    }

    [Fact]
    public void JsonRpcResponseSerializesWithExpectedContract()
    {
        var response = new JsonRpcResponse
        {
            Id = 7,
            Result = new JsonRpcResult
            {
                ProtocolVersion = LspJson.DefaultMcpProtocolVersion,
                Content = new[] { new TextContent { Text = "ok" } },
            },
        };

        var json = JsonSerializer.Serialize(response, LspJson.SerializerOptions);

        Assert.Contains("\"jsonrpc\":\"2.0\"", json, StringComparison.Ordinal);
        Assert.Contains("\"protocolVersion\":\"2024-11-05\"", json, StringComparison.Ordinal);
        Assert.Contains("\"text\":\"ok\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"error\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void DetailsTypesRetainExpectedErrorKindValues()
    {
        var details = new LspDiagnosticsDetails
        {
            FilePath = "a.ts",
            Severity = SeverityFilters.Warning,
            Mode = LspDiagnosticModes.Directory,
            ErrorKind = LspToolErrorKinds.NoFiles,
        };

        Assert.Equal(SeverityFilters.Warning, details.Severity);
        Assert.Equal(LspDiagnosticModes.Directory, details.Mode);
        Assert.Equal(LspToolErrorKinds.NoFiles, details.ErrorKind);
    }
}
