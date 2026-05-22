using System.Text.Json;
using System.Text.Json.Serialization;

namespace Omodot.AstGrepMcp;

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
    public static JsonRpcId? FromString(string? s) => s is null ? null : new(s);
    public static JsonRpcId? FromLong(long? n) => n is null ? null : new(n);
}

public sealed class JsonRpcIdConverter : JsonConverter<JsonRpcId>
{
    public override JsonRpcId? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.String => new(reader.GetString()),
            JsonTokenType.Number => new(reader.GetInt64()),
            JsonTokenType.Null => null,
            _ => null
        };
    }
    public override void Write(Utf8JsonWriter writer, JsonRpcId value, JsonSerializerOptions options)
    {
        if (value.Value is string s) writer.WriteStringValue(s);
        else if (value.Value is long n) writer.WriteNumberValue(n);
        else writer.WriteNullValue();
    }
}

public sealed record AstGrepMcpOptions(
    string? WorkspaceDirectory = null,
    string[]? DisabledTools = null);
