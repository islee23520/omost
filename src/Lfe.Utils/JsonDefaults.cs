using System.Text.Json;

namespace Lfe.Utils;

internal static class JsonDefaults
{
    internal static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        AllowTrailingCommas = true,
        WriteIndented = false,
    };
}
