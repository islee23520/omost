using System.Text.Json;

namespace Omodot.Utils;

internal static class JsonDefaults
{
    internal static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        AllowTrailingCommas = true,
        WriteIndented = false,
    };
}
