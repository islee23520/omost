using System.Text.Json;
using System.Text.Json.Nodes;

namespace Omodot.Utils;

internal static class JsonNodeHelpers
{
    internal static JsonNode? Clone(JsonNode? node) => node?.DeepClone();

    internal static JsonNode? SerializeToNode<T>(T value) => JsonSerializer.SerializeToNode(value, JsonDefaults.Options);

    internal static JsonObject EmptyObject() => [];

    internal static JsonObject EnsureObject(JsonNode? node) => node as JsonObject ?? [];
}
