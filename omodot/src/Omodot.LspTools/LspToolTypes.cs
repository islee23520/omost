using System.Text.Json.Serialization;

namespace Omodot.LspTools;

public static class LspToolNames
{
    public const string Status = "status";
    public const string Diagnostics = "diagnostics";
    public const string GotoDefinition = "goto_definition";
    public const string FindReferences = "find_references";
    public const string Symbols = "symbols";
    public const string PrepareRename = "prepare_rename";
    public const string Rename = "rename";

    public static IReadOnlyList<string> All { get; } =
        new[] { Status, Diagnostics, GotoDefinition, FindReferences, Symbols, PrepareRename, Rename };
}

public sealed record TextContent
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "text";

    [JsonPropertyName("text")]
    public string Text { get; init; } = string.Empty;
}

public sealed record ToolExecutionResult
{
    [JsonPropertyName("content")]
    public IReadOnlyList<TextContent> Content { get; init; } = Array.Empty<TextContent>();

    [JsonPropertyName("isError")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IsError { get; init; }

    [JsonPropertyName("details")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Details { get; init; }
}

public sealed record JsonSchema
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; init; }

    [JsonPropertyName("properties")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, JsonSchema>? Properties { get; init; }

    [JsonPropertyName("required")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Required { get; init; }

    [JsonPropertyName("items")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonSchema? Items { get; init; }

    [JsonPropertyName("enum")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Enum { get; init; }
}

public sealed record LspMcpToolDefinition
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("aliases")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Aliases { get; init; }

    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;

    [JsonPropertyName("inputSchema")]
    public JsonSchema InputSchema { get; init; } = new();
}

public sealed record McpToolDescriptor
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;

    [JsonPropertyName("inputSchema")]
    public object? InputSchema { get; init; }
}

public sealed record JsonRpcError
{
    [JsonPropertyName("code")]
    public int Code { get; init; }

    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;

    [JsonPropertyName("data")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Data { get; init; }
}

public sealed record JsonRpcResult
{
    [JsonPropertyName("capabilities")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, object?>? Capabilities { get; init; }

    [JsonPropertyName("serverInfo")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, object?>? ServerInfo { get; init; }

    [JsonPropertyName("protocolVersion")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ProtocolVersion { get; init; }

    [JsonPropertyName("tools")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<McpToolDescriptor>? Tools { get; init; }

    [JsonPropertyName("content")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<TextContent>? Content { get; init; }

    [JsonPropertyName("isError")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IsError { get; init; }

    [JsonPropertyName("details")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Details { get; init; }
}

public sealed record JsonRpcResponse
{
    [JsonPropertyName("jsonrpc")]
    public string Jsonrpc { get; init; } = LspJson.JsonRpcVersion;

    [JsonPropertyName("id")]
    public object? Id { get; init; }

    [JsonPropertyName("result")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonRpcResult? Result { get; init; }

    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonRpcError? Error { get; init; }
}

public sealed record LspDiagnosticsDetails
{
    [JsonPropertyName("filePath")]
    public string FilePath { get; init; } = string.Empty;

    [JsonPropertyName("severity")]
    public string Severity { get; init; } = SeverityFilters.All;

    [JsonPropertyName("mode")]
    public string Mode { get; init; } = LspDiagnosticModes.File;

    [JsonPropertyName("diagnostics")]
    public IReadOnlyList<DiagnosticEntry> Diagnostics { get; init; } = Array.Empty<DiagnosticEntry>();

    [JsonPropertyName("totalDiagnostics")]
    public int TotalDiagnostics { get; init; }

    [JsonPropertyName("truncated")]
    public bool Truncated { get; init; }

    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Error { get; init; }

    [JsonPropertyName("errorKind")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ErrorKind { get; init; }
}

public sealed record DiagnosticEntry
{
    [JsonPropertyName("file")]
    public string File { get; init; } = string.Empty;

    [JsonPropertyName("diagnostic")]
    public Diagnostic Diagnostic { get; init; } = new();
}

public sealed record LspGotoDefinitionDetails
{
    [JsonPropertyName("filePath")]
    public string FilePath { get; init; } = string.Empty;

    [JsonPropertyName("line")]
    public int Line { get; init; }

    [JsonPropertyName("character")]
    public int Character { get; init; }

    [JsonPropertyName("locations")]
    public IReadOnlyList<object> Locations { get; init; } = Array.Empty<object>();

    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Error { get; init; }

    [JsonPropertyName("errorKind")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ErrorKind { get; init; }
}

public sealed record LspFindReferencesDetails
{
    [JsonPropertyName("filePath")]
    public string FilePath { get; init; } = string.Empty;

    [JsonPropertyName("line")]
    public int Line { get; init; }

    [JsonPropertyName("character")]
    public int Character { get; init; }

    [JsonPropertyName("references")]
    public IReadOnlyList<Location> References { get; init; } = Array.Empty<Location>();

    [JsonPropertyName("totalReferences")]
    public int TotalReferences { get; init; }

    [JsonPropertyName("truncated")]
    public bool Truncated { get; init; }

    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Error { get; init; }

    [JsonPropertyName("errorKind")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ErrorKind { get; init; }
}

public sealed record LspSymbolsDetails
{
    [JsonPropertyName("filePath")]
    public string FilePath { get; init; } = string.Empty;

    [JsonPropertyName("scope")]
    public string Scope { get; init; } = LspSymbolScopes.Document;

    [JsonPropertyName("query")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Query { get; init; }

    [JsonPropertyName("symbols")]
    public IReadOnlyList<object> Symbols { get; init; } = Array.Empty<object>();

    [JsonPropertyName("totalSymbols")]
    public int TotalSymbols { get; init; }

    [JsonPropertyName("truncated")]
    public bool Truncated { get; init; }

    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Error { get; init; }

    [JsonPropertyName("errorKind")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ErrorKind { get; init; }
}

public sealed record LspPrepareRenameDetails
{
    [JsonPropertyName("filePath")]
    public string FilePath { get; init; } = string.Empty;

    [JsonPropertyName("line")]
    public int Line { get; init; }

    [JsonPropertyName("character")]
    public int Character { get; init; }

    [JsonPropertyName("result")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PrepareRenameValue? Result { get; init; }

    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Error { get; init; }

    [JsonPropertyName("errorKind")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ErrorKind { get; init; }
}

public sealed record LspRenameDetails
{
    [JsonPropertyName("filePath")]
    public string FilePath { get; init; } = string.Empty;

    [JsonPropertyName("line")]
    public int Line { get; init; }

    [JsonPropertyName("character")]
    public int Character { get; init; }

    [JsonPropertyName("newName")]
    public string NewName { get; init; } = string.Empty;

    [JsonPropertyName("apply")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ApplyResult? Apply { get; init; }

    [JsonPropertyName("edit")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public WorkspaceEdit? Edit { get; init; }

    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Error { get; init; }

    [JsonPropertyName("errorKind")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ErrorKind { get; init; }
}

public static class LspMcpToolCatalog
{
    public static IReadOnlyList<LspMcpToolDefinition> All { get; } =
        new[]
        {
            new LspMcpToolDefinition
            {
                Name = LspToolNames.Status,
                Aliases = new[] { "lsp_status" },
                Title = "LSP Status",
                Description = "List configured and active LSP servers without starting a new language server.",
                InputSchema = ObjectSchema(),
            },
            new LspMcpToolDefinition
            {
                Name = LspToolNames.Diagnostics,
                Aliases = new[] { "lsp_diagnostics" },
                Title = "LSP Diagnostics",
                Description = "Get errors, warnings, and hints for a source file or directory.",
                InputSchema = ObjectSchema(
                    new Dictionary<string, JsonSchema>
                    {
                        ["filePath"] = new JsonSchema { Type = "string", Description = "File or directory path to check." },
                        ["severity"] = new JsonSchema
                        {
                            Type = "string",
                            Enum = SeverityFilters.Values,
                            Description = "Severity filter. Defaults to all.",
                        },
                    },
                    "filePath"),
            },
            new LspMcpToolDefinition
            {
                Name = LspToolNames.GotoDefinition,
                Aliases = new[] { "lsp_goto_definition" },
                Title = "LSP Goto Definition",
                Description = "Find where a symbol is defined.",
                InputSchema = ObjectSchema(
                    new Dictionary<string, JsonSchema>
                    {
                        ["filePath"] = new JsonSchema { Type = "string", Description = "Source file containing the symbol." },
                        ["line"] = new JsonSchema { Type = "number", Description = "1-based line number." },
                        ["character"] = new JsonSchema { Type = "number", Description = "0-based column." },
                    },
                    "filePath", "line", "character"),
            },
            new LspMcpToolDefinition
            {
                Name = LspToolNames.FindReferences,
                Aliases = new[] { "lsp_find_references" },
                Title = "LSP Find References",
                Description = "Find references of a symbol across the workspace.",
                InputSchema = ObjectSchema(
                    new Dictionary<string, JsonSchema>
                    {
                        ["filePath"] = new JsonSchema { Type = "string", Description = "Source file containing the symbol." },
                        ["line"] = new JsonSchema { Type = "number", Description = "1-based line number." },
                        ["character"] = new JsonSchema { Type = "number", Description = "0-based column." },
                        ["includeDeclaration"] = new JsonSchema { Type = "boolean", Description = "Include the declaration. Defaults to true." },
                    },
                    "filePath", "line", "character"),
            },
            new LspMcpToolDefinition
            {
                Name = LspToolNames.Symbols,
                Aliases = new[] { "lsp_symbols" },
                Title = "LSP Symbols",
                Description = "List document symbols or search workspace symbols.",
                InputSchema = ObjectSchema(
                    new Dictionary<string, JsonSchema>
                    {
                        ["filePath"] = new JsonSchema { Type = "string", Description = "File path used as LSP context." },
                        ["scope"] = new JsonSchema
                        {
                            Type = "string",
                            Enum = LspSymbolScopes.Values,
                            Description = "Use document for file outline or workspace for project-wide search.",
                        },
                        ["query"] = new JsonSchema { Type = "string", Description = "Workspace symbol query." },
                        ["limit"] = new JsonSchema { Type = "number", Description = "Maximum number of symbols to return." },
                    },
                    "filePath", "scope"),
            },
            new LspMcpToolDefinition
            {
                Name = LspToolNames.PrepareRename,
                Aliases = new[] { "lsp_prepare_rename" },
                Title = "LSP Prepare Rename",
                Description = "Check whether a symbol can be renamed at a position.",
                InputSchema = ObjectSchema(
                    new Dictionary<string, JsonSchema>
                    {
                        ["filePath"] = new JsonSchema { Type = "string", Description = "Source file path." },
                        ["line"] = new JsonSchema { Type = "number", Description = "1-based line number." },
                        ["character"] = new JsonSchema { Type = "number", Description = "0-based column." },
                    },
                    "filePath", "line", "character"),
            },
            new LspMcpToolDefinition
            {
                Name = LspToolNames.Rename,
                Aliases = new[] { "lsp_rename" },
                Title = "LSP Rename",
                Description = "Rename a symbol across the workspace and apply the returned workspace edit.",
                InputSchema = ObjectSchema(
                    new Dictionary<string, JsonSchema>
                    {
                        ["filePath"] = new JsonSchema { Type = "string", Description = "Source file path." },
                        ["line"] = new JsonSchema { Type = "number", Description = "1-based line number." },
                        ["character"] = new JsonSchema { Type = "number", Description = "0-based column." },
                        ["newName"] = new JsonSchema { Type = "string", Description = "New symbol name." },
                    },
                    "filePath", "line", "character", "newName"),
            },
        };

    private static JsonSchema ObjectSchema(IReadOnlyDictionary<string, JsonSchema>? properties = null, params string[] required)
    {
        return new JsonSchema
        {
            Type = "object",
            Properties = properties,
            Required = required.Length == 0 ? Array.Empty<string>() : required,
        };
    }
}
