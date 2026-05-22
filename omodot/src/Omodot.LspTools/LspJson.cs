using System.Text.Json;
using System.Text.Json.Serialization;

namespace Omodot.LspTools;

public static class LspJson
{
    public const string JsonRpcVersion = "2.0";
    public const string DefaultMcpProtocolVersion = "2024-11-05";
    public const string ServerName = "lsp";
    public const string ServerVersion = "0.1.0";

    public static JsonSerializerOptions SerializerOptions { get; } = CreateSerializerOptions();

    public static bool TryConvertJsonRpcId(JsonElement element, out object? id)
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
