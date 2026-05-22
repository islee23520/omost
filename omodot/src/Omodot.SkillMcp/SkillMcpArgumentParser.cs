using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Omodot.SkillMcp;

public static class SkillMcpArgumentParser
{
    public static Dictionary<string, object?> ParseSkillMcpArguments(object? argsJson)
    {
        if (argsJson is null)
        {
            return [];
        }

        if (argsJson is JsonElement jsonElement)
        {
            return ParseJsonElement(jsonElement);
        }

        if (argsJson is JsonNode jsonNode)
        {
            return ParseJsonNode(jsonNode);
        }

        if (argsJson is IDictionary<string, object?> dictionary)
        {
            return new Dictionary<string, object?>(dictionary);
        }

        if (argsJson is IReadOnlyDictionary<string, object?> readOnlyDictionary)
        {
            return readOnlyDictionary.ToDictionary(pair => pair.Key, pair => pair.Value);
        }

        if (argsJson is System.Collections.IEnumerable and not string)
        {
            throw new InvalidOperationException("Arguments must be a JSON object");
        }

        if (argsJson.GetType().IsClass && argsJson.GetType() != typeof(string))
        {
            return argsJson.GetType()
                .GetProperties()
                .Where(property => property.GetIndexParameters().Length == 0 && property.CanRead)
                .ToDictionary(property => property.Name, property => ConvertValue(property.GetValue(argsJson)));
        }

        if (argsJson is not string argsString)
        {
            throw new InvalidOperationException("Invalid arguments JSON: Arguments must be a JSON object\n\nExpected a valid JSON object, e.g.: '{\"key\": \"value\"}'\nReceived: " + argsJson);
        }

        try
        {
            var jsonString = argsString.Length >= 2 && argsString.StartsWith('\'') && argsString.EndsWith('\'')
                ? argsString[1..^1]
                : argsString;

            var node = JsonNode.Parse(jsonString);
            if (node is not JsonObject jsonObject)
            {
                throw new InvalidOperationException("Arguments must be a JSON object");
            }

            return ParseJsonNode(jsonObject);
        }
        catch (Exception error)
        {
            var message = error is InvalidOperationException invalidOperationException ? invalidOperationException.Message : error.Message;
            throw new InvalidOperationException($"Invalid arguments JSON: {message}\n\nExpected a valid JSON object, e.g.: '{{\"key\": \"value\"}}'\nReceived: {argsString}");
        }
    }

    private static Dictionary<string, object?> ParseJsonElement(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("Arguments must be a JSON object");
        }

        var result = new Dictionary<string, object?>();
        foreach (var property in element.EnumerateObject())
        {
            result[property.Name] = ConvertJsonElement(property.Value);
        }

        return result;
    }

    private static Dictionary<string, object?> ParseJsonNode(JsonNode node)
    {
        if (node is not JsonObject jsonObject)
        {
            throw new InvalidOperationException("Arguments must be a JSON object");
        }

        var result = new Dictionary<string, object?>();
        foreach (var pair in jsonObject)
        {
            result[pair.Key] = ConvertJsonNode(pair.Value);
        }

        return result;
    }

    private static object? ConvertJsonNode(JsonNode? node)
    {
        return node switch
        {
            null => null,
            JsonObject obj => obj.ToDictionary(pair => pair.Key, pair => ConvertJsonNode(pair.Value)),
            JsonArray array => array.Select(ConvertJsonNode).ToList(),
            JsonValue value => ConvertJsonElement(value.GetValue<JsonElement>()),
            _ => null,
        };
    }

    private static object? ConvertValue(object? value)
    {
        return value switch
        {
            null => null,
            JsonElement element => ConvertJsonElement(element),
            JsonNode node => ConvertJsonNode(node),
            System.Collections.IDictionary dictionary => dictionary.Cast<System.Collections.DictionaryEntry>().ToDictionary(entry => entry.Key?.ToString() ?? string.Empty, entry => ConvertValue(entry.Value)),
            System.Collections.IEnumerable enumerable and not string => enumerable.Cast<object?>().Select(ConvertValue).ToList(),
            _ => value,
        };
    }

    private static object? ConvertJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => element.EnumerateObject().ToDictionary(property => property.Name, property => ConvertJsonElement(property.Value)),
            JsonValueKind.Array => element.EnumerateArray().Select(ConvertJsonElement).ToList(),
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number when element.TryGetInt64(out var longValue) => longValue,
            JsonValueKind.Number => element.GetDecimal(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            _ => element.ToString(),
        };
    }
}
