using System.Text.Json;

namespace Omodot.Utils;

public sealed record CompactionPart(string? Type);

public sealed record CompactionMessageInfo(string? Agent);

public sealed record CompactionMessage(string? Agent, CompactionMessageInfo? Info, IReadOnlyList<CompactionPart>? Parts);

public static class CompactionMarker
{
    public const string DefaultPartStorage = "/tmp/omo/parts";

    public static bool IsCompactionAgent(string? agent) => string.Equals(agent?.Trim(), "compaction", StringComparison.OrdinalIgnoreCase);

    public static bool HasCompactionPart(IReadOnlyList<CompactionPart>? parts) => parts?.Any(part => string.Equals(part.Type, "compaction", StringComparison.Ordinal)) == true;

    public static bool IsCompactionMessage(CompactionMessage message) => IsCompactionAgent(message.Info?.Agent ?? message.Agent) || HasCompactionPart(message.Parts);

    public static string GetCompactionPartStorageDir(string messageId, string? partStorage = null) => Path.Combine(partStorage ?? DefaultPartStorage, messageId);

    public static bool HasCompactionPartInStorage(string? messageId, string? partStorage = null)
    {
        if (string.IsNullOrEmpty(messageId))
        {
            return false;
        }

        var partDirectory = GetCompactionPartStorageDir(messageId, partStorage);
        if (!Directory.Exists(partDirectory))
        {
            return false;
        }

        try
        {
            return Directory.EnumerateFiles(partDirectory, "*.json")
                .Any(filePath =>
                {
                    try
                    {
                        var part = JsonSerializer.Deserialize<CompactionPart>(File.ReadAllText(filePath), JsonDefaults.Options);
                        return string.Equals(part?.Type, "compaction", StringComparison.Ordinal);
                    }
                    catch
                    {
                        return false;
                    }
                });
        }
        catch
        {
            return false;
        }
    }
}
