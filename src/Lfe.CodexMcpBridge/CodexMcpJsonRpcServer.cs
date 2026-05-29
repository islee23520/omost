using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lfe.CodexMcpBridge;

public static class CodexMcpJsonRpcServer
{
    public const string ServerName = "lfe_codex";
    public const string ServerVersion = "0.1.0";
    public const string ProtocolVersion = "2024-11-05";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly McpToolDescriptor[] Tools =
    [
        new("codex_dispatch", "Codex dispatch", "Dispatch a prompt to Codex CLI through CodexUlwHost.", new
        {
            type = "object",
            properties = new Dictionary<string, object>
            {
                ["prompt"] = new { type = "string", description = "Prompt to dispatch" },
                ["sessionId"] = new { type = "string", description = "Optional session id" },
            },
            required = new[] { "prompt" },
            additionalProperties = false,
        }),
        new("codex_read_status", "Codex read status", "Read the current status of a Codex session.", new
        {
            type = "object",
            properties = new Dictionary<string, object>
            {
                ["sessionId"] = new { type = "string", description = "Session id" },
            },
            required = new[] { "sessionId" },
            additionalProperties = false,
        }),
        new("codex_read_messages", "Codex read messages", "Read messages from a Codex session.", new
        {
            type = "object",
            properties = new Dictionary<string, object>
            {
                ["sessionId"] = new { type = "string", description = "Session id" },
            },
            required = new[] { "sessionId" },
            additionalProperties = false,
        }),
        new("codex_abort", "Codex abort", "Abort a running Codex session.", new
        {
            type = "object",
            properties = new Dictionary<string, object>
            {
                ["sessionId"] = new { type = "string", description = "Session id" },
            },
            required = new[] { "sessionId" },
            additionalProperties = false,
        }),
    ];

    public static async Task<string?> HandleRequestAsync(string json, CodexMcpToolServer server)
    {
        ArgumentNullException.ThrowIfNull(server);

        JsonDocument? doc;
        try { doc = JsonDocument.Parse(json); }
        catch { return Serialize(new JsonRpcResponse("2.0", null, Error: new(-32700, "Parse error"))); }

        using (doc)
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return Serialize(new JsonRpcResponse("2.0", null, Error: new(-32600, "Invalid Request")));
            }

            var root = doc.RootElement;
            var id = ExtractId(root);
            var method = root.TryGetProperty("method", out var methodEl) ? methodEl.GetString() : null;

            return method switch
            {
                "notifications/initialized" => null,
                "ping" => Serialize(new JsonRpcResponse("2.0", id, Result: new JsonRpcResult())),
                "initialize" => Serialize(new JsonRpcResponse("2.0", id, Result: new JsonRpcResult
                {
                    Capabilities = new { tools = new { listChanged = false } },
                    ServerInfo = new { name = ServerName, version = ServerVersion },
                    ProtocolVersion = ProtocolVersion,
                })),
                "tools/list" => Serialize(new JsonRpcResponse("2.0", id, Result: new JsonRpcResult { Tools = Tools })),
                "tools/call" => await HandleToolCallAsync(id, root, server).ConfigureAwait(false),
                _ => Serialize(new JsonRpcResponse("2.0", id, Error: new(-32601, $"Method not found: {method}"))),
            };
        }
    }

    private static async Task<string> HandleToolCallAsync(JsonRpcId? id, JsonElement root, CodexMcpToolServer server)
    {
        var hasParams = root.TryGetProperty("params", out var paramsEl);
        var name = hasParams && paramsEl.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : null;
        if (string.IsNullOrWhiteSpace(name)) return Serialize(new JsonRpcResponse("2.0", id, Error: new(-32602, "tools/call requires params.name")));

        var args = hasParams && paramsEl.TryGetProperty("arguments", out var argsEl) ? argsEl : default;
        try
        {
            object result = name switch
            {
                "codex_dispatch" => await server.DispatchAsync(RequireString(args, "prompt"), OptionalString(args, "sessionId")).ConfigureAwait(false),
                "codex_read_status" => await server.ReadStatusAsync(RequireString(args, "sessionId")).ConfigureAwait(false),
                "codex_read_messages" => await server.ReadMessagesAsync(RequireString(args, "sessionId")).ConfigureAwait(false),
                "codex_abort" => await server.AbortAsync(RequireString(args, "sessionId")).ConfigureAwait(false),
                _ => throw new ArgumentException($"Unknown tool: {name}"),
            };

            return Serialize(new JsonRpcResponse("2.0", id, Result: new JsonRpcResult
            {
                Content = [new TextContent("text", JsonSerializer.Serialize(result, JsonOptions))],
                IsError = false,
            }));
        }
        catch (Exception ex)
        {
            return Serialize(new JsonRpcResponse("2.0", id, Result: new JsonRpcResult
            {
                Content = [new TextContent("text", ex.Message)],
                IsError = true,
            }));
        }
    }

    private static JsonRpcId? ExtractId(JsonElement root)
    {
        if (!root.TryGetProperty("id", out var idEl)) return null;
        return idEl.ValueKind switch
        {
            JsonValueKind.String => JsonRpcId.FromString(idEl.GetString()),
            JsonValueKind.Number => JsonRpcId.FromLong(idEl.GetInt64()),
            _ => null,
        };
    }

    private static string RequireString(JsonElement args, string key)
    {
        if (args.ValueKind != JsonValueKind.Object) throw new ArgumentException($"{key} must be a non-empty string");
        if (args.TryGetProperty(key, out var el) && el.ValueKind == JsonValueKind.String)
        {
            var value = el.GetString();
            if (!string.IsNullOrWhiteSpace(value)) return value;
        }
        throw new ArgumentException($"{key} must be a non-empty string");
    }

    private static string? OptionalString(JsonElement args, string key)
    {
        if (args.ValueKind != JsonValueKind.Object) return null;
        return args.TryGetProperty(key, out var el) && el.ValueKind == JsonValueKind.String ? el.GetString() : null;
    }

    private static string Serialize(JsonRpcResponse response) => JsonSerializer.Serialize(response, JsonOptions);
}

public sealed record TextContent(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("text")] string Text);

public sealed record McpToolDescriptor(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("inputSchema")] object InputSchema);

public sealed record JsonRpcError(
    [property: JsonPropertyName("code")] int Code,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("data")] object? Data = null);

public sealed record JsonRpcResult
{
    [JsonPropertyName("capabilities")] public object? Capabilities { get; init; }
    [JsonPropertyName("serverInfo")] public object? ServerInfo { get; init; }
    [JsonPropertyName("protocolVersion")] public string? ProtocolVersion { get; init; }
    [JsonPropertyName("tools")] public IReadOnlyList<McpToolDescriptor>? Tools { get; init; }
    [JsonPropertyName("content")] public IReadOnlyList<TextContent>? Content { get; init; }
    [JsonPropertyName("isError")] public bool IsError { get; init; }
}

public sealed record JsonRpcResponse(
    [property: JsonPropertyName("jsonrpc")] string JsonRpc,
    [property: JsonPropertyName("id")] JsonRpcId? Id,
    [property: JsonPropertyName("result")] JsonRpcResult? Result = null,
    [property: JsonPropertyName("error")] JsonRpcError? Error = null);

[JsonConverter(typeof(JsonRpcIdConverter))]
public sealed record JsonRpcId(object? Value)
{
    public static JsonRpcId? FromString(string? value) => value is null ? null : new(value);
    public static JsonRpcId? FromLong(long? value) => value is null ? null : new(value);
}

public sealed class JsonRpcIdConverter : JsonConverter<JsonRpcId>
{
    public override JsonRpcId? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.String => new(reader.GetString()),
            JsonTokenType.Number => new(reader.GetInt64()),
            JsonTokenType.Null => null,
            _ => null,
        };

    public override void Write(Utf8JsonWriter writer, JsonRpcId value, JsonSerializerOptions options)
    {
        if (value.Value is string s) writer.WriteStringValue(s);
        else if (value.Value is long n) writer.WriteNumberValue(n);
        else writer.WriteNullValue();
    }
}
