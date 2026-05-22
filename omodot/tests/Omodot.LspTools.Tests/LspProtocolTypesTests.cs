using System.Text.Json;

using Omodot.LspTools;

namespace Omodot.LspTools.Tests;

public sealed class LspProtocolTypesTests
{
    [Fact]
    public void DefaultsMatchTheTypeScriptPackage()
    {
        Assert.Equal(200, LspDefaults.DefaultMaxDiagnostics);
        Assert.Equal(200, LspDefaults.DefaultMaxReferences);
        Assert.Equal(200, LspDefaults.DefaultMaxSymbols);
        Assert.Equal(50, LspDefaults.DefaultMaxDirectoryFiles);
        Assert.Equal(15_000, LspDefaults.RequestTimeoutMs);
        Assert.Equal(60_000, LspDefaults.InitTimeoutMs);
    }

    [Fact]
    public void SymbolAndSeverityMapsMatchLspValues()
    {
        Assert.Equal("Class", LspProtocolMaps.SymbolKindMap[(int)SymbolKind.Class]);
        Assert.Equal("TypeParameter", LspProtocolMaps.SymbolKindMap[(int)SymbolKind.TypeParameter]);
        Assert.Equal(SeverityFilters.Error, LspProtocolMaps.SeverityMap[(int)DiagnosticSeverity.Error]);
        Assert.Equal(SeverityFilters.Hint, LspProtocolMaps.SeverityMap[(int)DiagnosticSeverity.Hint]);
    }

    [Fact]
    public void LanguageIdLookupFallsBackToPlaintext()
    {
        Assert.Equal("typescriptreact", LspProtocolMaps.GetLanguageId(".tsx"));
        Assert.Equal("markdown", LspProtocolMaps.GetLanguageId(".md"));
        Assert.Equal("plaintext", LspProtocolMaps.GetLanguageId(".unknown"));
    }

    [Fact]
    public void SerializerOmitsNullsAndUsesCamelCase()
    {
        var diagnostic = new Diagnostic
        {
            Message = "boom",
            Range = new Range
            {
                Start = new Position { Line = 1, Character = 2 },
                End = new Position { Line = 1, Character = 4 },
            },
        };

        var json = JsonSerializer.Serialize(diagnostic, LspJson.SerializerOptions);

        Assert.Contains("\"range\"", json, StringComparison.Ordinal);
        Assert.Contains("\"message\":\"boom\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("severity", json, StringComparison.Ordinal);
        Assert.DoesNotContain("source", json, StringComparison.Ordinal);
    }

    [Fact]
    public void WorkspaceDocumentChangeFactoriesProduceExpectedShapes()
    {
        var create = WorkspaceDocumentChange.ForCreateFile("file:///tmp/a.ts", new CreateFileOptions { Overwrite = true });
        var rename = WorkspaceDocumentChange.ForRenameFile("file:///tmp/a.ts", "file:///tmp/b.ts");
        var edit = WorkspaceDocumentChange.ForTextDocumentEdit(
            new VersionedTextDocumentIdentifier { Uri = "file:///tmp/a.ts", Version = 2 },
            new[] { new TextEdit { NewText = "x" } });

        Assert.Equal("create", create.Kind);
        Assert.Equal("rename", rename.Kind);
        Assert.Null(edit.Kind);
        Assert.NotNull(edit.TextDocument);
        Assert.Single(edit.Edits!);
    }

    [Fact]
    public void JsonRpcIdConversionSupportsStringNumberAndNull()
    {
        Assert.True(LspJson.TryConvertJsonRpcId(JsonDocument.Parse("\"abc\"").RootElement, out var stringId));
        Assert.Equal("abc", stringId);

        Assert.True(LspJson.TryConvertJsonRpcId(JsonDocument.Parse("123").RootElement, out var numberId));
        Assert.Equal(123L, numberId);

        Assert.True(LspJson.TryConvertJsonRpcId(JsonDocument.Parse("null").RootElement, out var nullId));
        Assert.Null(nullId);
    }
}
