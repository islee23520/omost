using System.Text;
using System.Text.Json.Nodes;

namespace Lfe.Utils;

public static class SnakeCase
{
    public static string CamelToSnake(string value)
    {
        var builder = new StringBuilder();

        foreach (var character in value)
        {
            if (char.IsUpper(character))
            {
                builder.Append('_');
                builder.Append(char.ToLowerInvariant(character));
            }
            else
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }

    public static string SnakeToCamel(string value)
    {
        var builder = new StringBuilder();

        for (var index = 0; index < value.Length; index++)
        {
            if (value[index] == '_' && index + 1 < value.Length && char.IsLower(value[index + 1]))
            {
                builder.Append(char.ToUpperInvariant(value[index + 1]));
                index++;
                continue;
            }

            builder.Append(value[index]);
        }

        return builder.ToString();
    }

    public static JsonObject TransformObjectKeys(JsonObject value, Func<string, string> transformer, bool deep = true)
    {
        var result = new JsonObject();

        foreach (var pair in value)
        {
            result[transformer(pair.Key)] = deep ? TransformNode(pair.Value, transformer) : JsonNodeHelpers.Clone(pair.Value);
        }

        return result;
    }

    public static JsonObject ObjectToSnakeCase(JsonObject value, bool deep = true) => TransformObjectKeys(value, CamelToSnake, deep);

    public static JsonObject ObjectToCamelCase(JsonObject value, bool deep = true) => TransformObjectKeys(value, SnakeToCamel, deep);

    private static JsonNode? TransformNode(JsonNode? node, Func<string, string> transformer)
    {
        return node switch
        {
            JsonObject jsonObject => TransformObjectKeys(jsonObject, transformer, true),
            JsonArray jsonArray => new JsonArray(jsonArray.Select(item => TransformNode(item, transformer)).ToArray()),
            _ => JsonNodeHelpers.Clone(node),
        };
    }
}
