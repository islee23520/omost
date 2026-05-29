using System.Text.Json;
using System.Text.Json.Serialization;

using Lfe.Protocol.Types;

namespace Lfe.Protocol.JsonRpc;

public static class JsonRpcProtocol
{
    public const string Version = "2.0";

    public static JsonSerializerOptions SerializerOptions { get; } = CreateSerializerOptions();

    public static bool TryConvertId(JsonElement element, out object? id)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                id = element.GetString();
                return true;
            case JsonValueKind.Number:
                if (element.TryGetInt64(out var integerId))
                {
                    id = integerId;
                    return true;
                }

                if (element.TryGetDouble(out var floatingPointId))
                {
                    id = floatingPointId;
                    return true;
                }

                break;
            case JsonValueKind.Null:
                id = null;
                return true;
        }

        id = null;
        return false;
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        return new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
        };
    }
}

public sealed record JsonRpcInboundRequestMessage
{
    [JsonPropertyName("jsonrpc")]
    public string Jsonrpc { get; init; } = string.Empty;

    [JsonPropertyName("id")]
    public JsonElement Id { get; init; }

    [JsonPropertyName("method")]
    public string? Method { get; init; }

    [JsonPropertyName("params")]
    public JsonElement Params { get; init; }
}

public sealed record JsonRpcRequestMessage<TParams>
{
    [JsonPropertyName("jsonrpc")]
    public string Jsonrpc { get; init; } = JsonRpcProtocol.Version;

    [JsonPropertyName("id")]
    public object? Id { get; init; }

    [JsonPropertyName("method")]
    public string Method { get; init; } = string.Empty;

    [JsonPropertyName("params")]
    public TParams Params { get; init; } = default!;
}

public sealed record JsonRpcNotificationMessage<TParams>
{
    [JsonPropertyName("jsonrpc")]
    public string Jsonrpc { get; init; } = JsonRpcProtocol.Version;

    [JsonPropertyName("method")]
    public string Method { get; init; } = string.Empty;

    [JsonPropertyName("params")]
    public TParams Params { get; init; } = default!;
}

public sealed record JsonRpcSuccessResponseMessage<TResult>
{
    [JsonPropertyName("jsonrpc")]
    public string Jsonrpc { get; init; } = JsonRpcProtocol.Version;

    [JsonPropertyName("id")]
    public object? Id { get; init; }

    [JsonPropertyName("result")]
    public TResult Result { get; init; } = default!;
}

public sealed record JsonRpcErrorResponseMessage
{
    [JsonPropertyName("jsonrpc")]
    public string Jsonrpc { get; init; } = JsonRpcProtocol.Version;

    [JsonPropertyName("id")]
    public object? Id { get; init; }

    [JsonPropertyName("error")]
    public JsonRpcError Error { get; init; } = new();
}
