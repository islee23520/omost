using System.Text.Json;

namespace Lfe.TeamModeCore;

internal static class JsonHelpers
{
    public static JsonElement ToElement(object? input)
    {
        return input switch
        {
            null => default,
            JsonElement element => element.Clone(),
            _ => JsonSerializer.SerializeToElement(input),
        };
    }

    public static object? ToPlainValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => element.EnumerateObject().ToDictionary(property => property.Name, property => ToPlainValue(property.Value)),
            JsonValueKind.Array => element.EnumerateArray().Select(ToPlainValue).ToList(),
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number when element.TryGetInt64(out var longValue) => longValue,
            JsonValueKind.Number => element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            _ => null,
        };
    }

    public static Dictionary<string, object?>? ToPlainObject(object? input)
    {
        var element = ToElement(input);
        return element.ValueKind == JsonValueKind.Object
            ? (Dictionary<string, object?>)ToPlainValue(element)!
            : null;
    }

    public static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement property)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            property = default;
            return false;
        }

        foreach (var candidate in element.EnumerateObject())
        {
            if (string.Equals(candidate.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                property = candidate.Value;
                return true;
            }
        }

        property = default;
        return false;
    }

    public static string? GetString(JsonElement element, string propertyName)
    {
        return TryGetProperty(element, propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    public static bool? GetBool(JsonElement element, string propertyName)
    {
        return TryGetProperty(element, propertyName, out var property) && (property.ValueKind is JsonValueKind.True or JsonValueKind.False)
            ? property.GetBoolean()
            : null;
    }

    public static long? GetLong(JsonElement element, string propertyName)
    {
        if (!TryGetProperty(element, propertyName, out var property) || property.ValueKind != JsonValueKind.Number)
        {
            return null;
        }

        return property.TryGetInt64(out var longValue) ? longValue : null;
    }

    public static double? GetDouble(JsonElement element, string propertyName)
    {
        if (!TryGetProperty(element, propertyName, out var property) || property.ValueKind != JsonValueKind.Number)
        {
            return null;
        }

        return property.GetDouble();
    }

    public static List<string>? GetStringList(JsonElement element, string propertyName)
    {
        if (!TryGetProperty(element, propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var values = new List<string>();
        foreach (var item in property.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                values.Add(item.GetString() ?? string.Empty);
            }
        }

        return values;
    }

    public static List<object?>? GetObjectList(JsonElement element, string propertyName)
    {
        if (!TryGetProperty(element, propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        return property.EnumerateArray().Select(ToPlainValue).ToList();
    }

    public static Dictionary<string, object?>? GetObjectDictionary(JsonElement element, string propertyName)
    {
        if (!TryGetProperty(element, propertyName, out var property) || property.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return (Dictionary<string, object?>)ToPlainValue(property)!;
    }

    public static string NormalizeNameStem(string value)
    {
        var normalized = new string(value.Trim().ToLowerInvariant().Select(character => char.IsLetterOrDigit(character) ? character : '-').ToArray());
        while (normalized.Contains("--", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("--", "-", StringComparison.Ordinal);
        }

        normalized = normalized.Trim('-');
        return normalized.Length > 0 ? normalized : "member";
    }

    public static string? GetMemberName(object? value)
    {
        return value switch
        {
            ITeamMember member => member.Name,
            Dictionary<string, object?> dictionary when dictionary.TryGetValue("name", out var name) && name is string nameText => nameText,
            _ => null,
        };
    }
}
