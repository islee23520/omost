using System.Text.Json.Nodes;

namespace Lfe.Utils;

public static class DeepMerge
{
    private static readonly HashSet<string> DangerousKeys = ["__proto__", "constructor", "prototype"];
    private const int MaxDepth = 50;

    public static bool IsPlainObject(JsonNode? value) => value is JsonObject;

    public static JsonObject? Merge(JsonObject? baseValue, JsonObject? overrideValue, int depth = 0)
    {
        if (baseValue is null && overrideValue is null)
        {
            return null;
        }

        if (baseValue is null)
        {
            return overrideValue?.DeepClone() as JsonObject;
        }

        if (overrideValue is null)
        {
            return baseValue.DeepClone() as JsonObject;
        }

        if (depth > MaxDepth)
        {
            return overrideValue.DeepClone() as JsonObject ?? baseValue.DeepClone() as JsonObject;
        }

        var result = baseValue.DeepClone() as JsonObject ?? [];

        foreach (var pair in overrideValue)
        {
            if (DangerousKeys.Contains(pair.Key))
            {
                continue;
            }

            var baseNode = baseValue[pair.Key];
            var overrideNode = pair.Value;

            if (baseNode is JsonObject baseObject && overrideNode is JsonObject overrideObject)
            {
                result[pair.Key] = Merge(baseObject, overrideObject, depth + 1);
                continue;
            }

            result[pair.Key] = JsonNodeHelpers.Clone(overrideNode);
        }

        return result;
    }
}
