using System.Text.Json;
using Lfe.AstGrep;

namespace Lfe.AstGrepMcp;

public static class McpServer
{
    private const string ServerName = "ast_grep";
    private const string ServerVersion = "0.1.0";
    private const string ProtocolVersion = "2024-11-05";

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private static readonly McpToolDescriptor[] Tools =
    [
        new("search", "AST grep search", ToolDescriptions.SearchDescription, new {
            type = "object",
            properties = new Dictionary<string, object>
            {
                ["pattern"] = new { type = "string", description = ToolDescriptions.SearchPatternParam },
                ["lang"] = new { type = "string", description = "Target language" },
                ["paths"] = new { type = "array", items = new { type = "string" }, description = "Paths to search" },
            },
            required = new[] { "pattern", "lang" },
            additionalProperties = false,
        }),
        new("replace", "AST grep replace", ToolDescriptions.ReplaceDescription, new {
            type = "object",
            properties = new Dictionary<string, object>
            {
                ["pattern"] = new { type = "string", description = "AST pattern to match" },
                ["rewrite"] = new { type = "string", description = "Replacement pattern" },
                ["lang"] = new { type = "string", description = "Target language" },
                ["dryRun"] = new { type = "boolean", description = "Preview changes without applying. Defaults to true." },
            },
            required = new[] { "pattern", "rewrite", "lang" },
            additionalProperties = false,
        }),
    ];

    public static async Task<string?> HandleRequestAsync(string json, AstGrepMcpOptions? options = null, SgRunnerDeps? deps = null)
    {
        options ??= new();
        JsonDocument? doc;
        try { doc = JsonDocument.Parse(json); }
        catch { return JsonSerializer.Serialize(new JsonRpcResponse("2.0", null, Error: new(-32700, "Parse error"))); }

        if (doc.RootElement.ValueKind != JsonValueKind.Object)
            return Serialize(new JsonRpcResponse("2.0", null, Error: new(-32600, "Invalid Request")));

        var root = doc.RootElement;
        var id = ExtractId(root);

        var method = root.TryGetProperty("method", out var methodEl) ? methodEl.GetString() : null;
        if (method == "notifications/initialized") { doc.Dispose(); return null; }
        if (method == "ping") { doc.Dispose(); return Serialize(new JsonRpcResponse("2.0", id, Result: new JsonRpcResult())); }
        if (method == "initialize")
        {
            doc.Dispose();
            return Serialize(new JsonRpcResponse("2.0", id, Result: new JsonRpcResult
            {
                Capabilities = new { tools = new { listChanged = false } },
                ServerInfo = new { name = ServerName, version = ServerVersion },
                ProtocolVersion = ProtocolVersion,
            }));
        }
        if (method == "tools/list") { doc.Dispose(); return Serialize(new JsonRpcResponse("2.0", id, Result: new JsonRpcResult { Tools = Tools })); }
        if (method == "tools/call")
        {
            var result = await HandleToolCallAsync(id, root, options, deps);
            doc.Dispose();
            return result;
        }
        doc.Dispose();
        return Serialize(new JsonRpcResponse("2.0", id, Error: new(-32601, $"Method not found: {method}")));
    }

    private static JsonRpcId? ExtractId(JsonElement root)
    {
        if (!root.TryGetProperty("id", out var idEl)) return null;
        return idEl.ValueKind switch
        {
            JsonValueKind.String => JsonRpcId.FromString(idEl.GetString()),
            JsonValueKind.Number => JsonRpcId.FromLong(idEl.GetInt64()),
            _ => null
        };
    }

    private static async Task<string> HandleToolCallAsync(JsonRpcId? id, JsonElement root, AstGrepMcpOptions options, SgRunnerDeps? deps)
    {
        var hasParams = root.TryGetProperty("params", out var paramsEl);
        var name = hasParams && paramsEl.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : null;
        if (name is null) return Serialize(new JsonRpcResponse("2.0", id, Error: new(-32602, "tools/call requires params.name")));

        try
        {
            var argsEl = hasParams && paramsEl.TryGetProperty("arguments", out var el) ? el : (JsonElement?)null;
            var workspace = WorkspacePaths.NormalizeWorkspaceDirectory(options.WorkspaceDirectory ?? Directory.GetCurrentDirectory());

            if (deps is null)
            {
                return Serialize(new JsonRpcResponse("2.0", id, Result: new JsonRpcResult
                {
                    Content = [new TextContent("text", "Error: No SgRunnerDeps provided")],
                    IsError = true
                }));
            }

            if (name == "search")
            {
                var runArgs = ParseSearchArgs(argsEl ?? default, workspace);
                var result = await SgRunner.RunSgAsync(runArgs, deps).ConfigureAwait(false);
                var output = ResultFormatter.FormatSearchResult(result);
                if (result.Matches.Count == 0 && result.Error is null)
                {
                    var hint = PatternHints.GetPatternHint(runArgs.Pattern, runArgs.Language);
                    if (hint is not null) output += $"\n\n{hint}";
                }
                return Serialize(new JsonRpcResponse("2.0", id, Result: new JsonRpcResult { Content = [new TextContent("text", output)], IsError = result.Error is not null }));
            }
            if (name == "replace")
            {
                var (runArgs, dryRun) = ParseReplaceArgs(argsEl ?? default, workspace);
                var result = await SgRunner.RunSgAsync(runArgs, deps).ConfigureAwait(false);
                var output = ResultFormatter.FormatReplaceResult(result, dryRun);
                return Serialize(new JsonRpcResponse("2.0", id, Result: new JsonRpcResult { Content = [new TextContent("text", output)], IsError = result.Error is not null }));
            }
            return Serialize(new JsonRpcResponse("2.0", id, Error: new(-32602, $"Unknown tool: {name}")));
        }
        catch (Exception ex)
        {
            return Serialize(new JsonRpcResponse("2.0", id, Result: new JsonRpcResult { Content = [new TextContent("text", ex.Message)], IsError = true }));
        }
    }

    internal static SgRunArgs ParseSearchArgs(JsonElement args, string workspace) => new()
    {
        Pattern = RequireString(args, "pattern"),
        Language = ParseLanguage(RequireString(args, "lang")),
        Cwd = workspace,
        Paths = WorkspacePaths.ResolveWorkspacePaths(OptionalStringArray(args, "paths"), workspace)
    };

    internal static (SgRunArgs Options, bool DryRun) ParseReplaceArgs(JsonElement args, string workspace)
    {
        var dryRun = OptionalBool(args, "dryRun") ?? true;
        return (new SgRunArgs
        {
            Pattern = RequireString(args, "pattern"),
            Rewrite = RequireString(args, "rewrite"),
            Language = ParseLanguage(RequireString(args, "lang")),
            Cwd = workspace,
            Paths = WorkspacePaths.ResolveWorkspacePaths(OptionalStringArray(args, "paths"), workspace),
            UpdateAll = !dryRun
        }, dryRun);
    }

    private static CliLanguage ParseLanguage(string lang) => Enum.TryParse<CliLanguage>(lang, ignoreCase: true, out var result) ? result : CliLanguage.Typescript;

    private static string RequireString(JsonElement args, string key)
    {
        if (args.ValueKind != JsonValueKind.Object) throw new ArgumentException($"{key} must be a non-empty string");
        if (args.TryGetProperty(key, out var el) && el.ValueKind == JsonValueKind.String)
        {
            var s = el.GetString();
            if (!string.IsNullOrEmpty(s)) return s;
        }
        throw new ArgumentException($"{key} must be a non-empty string");
    }

    private static string[]? OptionalStringArray(JsonElement args, string key)
    {
        if (args.ValueKind != JsonValueKind.Object) return null;
        if (!args.TryGetProperty(key, out var el) || el.ValueKind != JsonValueKind.Array) return null;
        return el.EnumerateArray().Select(e => e.GetString()).Where(s => s is not null).Cast<string>().ToArray();
    }

    private static bool? OptionalBool(JsonElement args, string key)
    {
        if (args.ValueKind != JsonValueKind.Object) return null;
        if (!args.TryGetProperty(key, out var el)) return null;
        return el.ValueKind == JsonValueKind.True ? true : el.ValueKind == JsonValueKind.False ? false : null;
    }

    private static string Serialize(JsonRpcResponse resp) => JsonSerializer.Serialize(resp, JsonOpts);
}
