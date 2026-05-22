using System.Collections;
using System.Reflection;
using System.Text.Json;

namespace Omodot.BackgroundAgent;

internal static class ObjectRecord
{
    public static bool IsRecord(object? value)
    {
        return AsRecord(value) is not null;
    }

    public static IReadOnlyDictionary<string, object?>? AsRecord(object? value)
    {
        switch (value)
        {
            case null:
                return null;
            case IReadOnlyDictionary<string, object?> typed:
                return typed;
            case IDictionary<string, object?> mutable:
                return new Dictionary<string, object?>(mutable, StringComparer.Ordinal);
            case JsonElement { ValueKind: JsonValueKind.Object } element:
                return FromJsonElement(element);
            case string:
            case DateTime:
            case DateTimeOffset:
            case Guid:
            case Enum:
            case ValueType:
                return null;
        }

        var properties = value.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property => property.CanRead)
            .ToArray();

        if (properties.Length == 0)
        {
            return null;
        }

        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var property in properties)
        {
            result[property.Name] = property.GetValue(value);
        }

        return result;
    }

    public static string? GetString(IReadOnlyDictionary<string, object?>? record, string key)
    {
        if (record is null || !record.TryGetValue(key, out var value))
        {
            return null;
        }

        return value is string text && text.Length > 0 ? text : null;
    }

    public static IReadOnlyDictionary<string, object?>? GetRecord(IReadOnlyDictionary<string, object?>? record, string key)
    {
        if (record is null || !record.TryGetValue(key, out var value))
        {
            return null;
        }

        return AsRecord(value);
    }

    public static DateTime? GetDateTime(IReadOnlyDictionary<string, object?>? record, string key)
    {
        if (record is null || !record.TryGetValue(key, out var value))
        {
            return null;
        }

        return value switch
        {
            DateTime dateTime => dateTime,
            DateTimeOffset offset => offset.UtcDateTime,
            long milliseconds when milliseconds >= 0 => DateTimeOffset.FromUnixTimeMilliseconds(milliseconds).UtcDateTime,
            int milliseconds when milliseconds >= 0 => DateTimeOffset.FromUnixTimeMilliseconds(milliseconds).UtcDateTime,
            double milliseconds when double.IsFinite(milliseconds) => DateTimeOffset.FromUnixTimeMilliseconds((long)milliseconds).UtcDateTime,
            string text when DateTime.TryParse(text, out var parsed) => parsed,
            JsonElement { ValueKind: JsonValueKind.String } json when json.TryGetDateTime(out var parsed) => parsed,
            JsonElement { ValueKind: JsonValueKind.Number } json when json.TryGetInt64(out var milliseconds) => DateTimeOffset.FromUnixTimeMilliseconds(milliseconds).UtcDateTime,
            _ => null,
        };
    }

    public static int? GetInt32(IReadOnlyDictionary<string, object?>? record, string key)
    {
        if (record is null || !record.TryGetValue(key, out var value))
        {
            return null;
        }

        return value switch
        {
            int number => number,
            long number when number is >= int.MinValue and <= int.MaxValue => (int)number,
            string text when int.TryParse(text, out var parsed) => parsed,
            JsonElement { ValueKind: JsonValueKind.Number } json when json.TryGetInt32(out var parsed) => parsed,
            JsonElement { ValueKind: JsonValueKind.String } json when int.TryParse(json.GetString(), out var parsed) => parsed,
            _ => null,
        };
    }

    public static long? GetInt64(IReadOnlyDictionary<string, object?>? record, string key)
    {
        if (record is null || !record.TryGetValue(key, out var value))
        {
            return null;
        }

        return value switch
        {
            int number => number,
            long number => number,
            double number when double.IsFinite(number) => (long)number,
            string text when long.TryParse(text, out var parsed) => parsed,
            JsonElement { ValueKind: JsonValueKind.Number } json when json.TryGetInt64(out var parsed) => parsed,
            JsonElement { ValueKind: JsonValueKind.String } json when long.TryParse(json.GetString(), out var parsed) => parsed,
            _ => null,
        };
    }

    private static IReadOnlyDictionary<string, object?> FromJsonElement(JsonElement element)
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            result[property.Name] = FromJsonValue(property.Value);
        }

        return result;
    }

    private static object? FromJsonValue(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.Object => FromJsonElement(value),
            JsonValueKind.Array => value.EnumerateArray().Select(FromJsonValue).ToArray(),
            JsonValueKind.String when value.TryGetDateTime(out var parsed) => parsed,
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number when value.TryGetInt64(out var longValue) => longValue,
            JsonValueKind.Number when value.TryGetDouble(out var doubleValue) => doubleValue,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null,
        };
    }
}
