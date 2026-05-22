using System.Text.Json.Nodes;

namespace Omodot.Utils;

public static class RecordTypeGuard
{
    public static bool IsRecord(JsonNode? value) => value is JsonObject or JsonArray;
}
