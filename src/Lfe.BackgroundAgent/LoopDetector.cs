using System.Text.Json;
using System.Text.Json.Nodes;

namespace Lfe.BackgroundAgent;

public sealed record CircuitBreakerSettings
{
    public bool Enabled { get; init; }
    public int MaxToolCalls { get; init; }
    public int ConsecutiveThreshold { get; init; }
}

public sealed record ToolLoopDetectionResult
{
    public bool Triggered { get; init; }
    public string? ToolName { get; init; }
    public int? RepeatedCount { get; init; }
}

public static class LoopDetector
{
    public static CircuitBreakerSettings ResolveCircuitBreakerSettings(BackgroundTaskCoreConfig? config = null)
    {
        return new CircuitBreakerSettings
        {
            Enabled = config?.CircuitBreaker?.Enabled ?? BackgroundAgentConstants.DefaultCircuitBreakerEnabled,
            MaxToolCalls = config?.CircuitBreaker?.MaxToolCalls ?? config?.MaxToolCalls ?? BackgroundAgentConstants.DefaultMaxToolCalls,
            ConsecutiveThreshold = config?.CircuitBreaker?.ConsecutiveThreshold ?? BackgroundAgentConstants.DefaultCircuitBreakerConsecutiveThreshold,
        };
    }

    public static string CreateToolCallSignature(string toolName, IReadOnlyDictionary<string, object?>? toolInput = null)
    {
        if (toolInput is null || toolInput.Count == 0)
        {
            return toolName;
        }

        var sortedJson = SortNode(JsonSerializer.SerializeToNode(toolInput));
        return $"{toolName}::{sortedJson!.ToJsonString()}";
    }

    public static ToolCallWindow RecordToolCall(
        ToolCallWindow? window,
        string toolName,
        CircuitBreakerSettings settings,
        IReadOnlyDictionary<string, object?>? toolInput = null)
    {
        if (toolInput is null)
        {
            return new ToolCallWindow
            {
                LastSignature = $"{toolName}::__unknown-input__",
                ConsecutiveCount = 1,
                Threshold = settings.ConsecutiveThreshold,
            };
        }

        var signature = CreateToolCallSignature(toolName, toolInput);
        return new ToolCallWindow
        {
            LastSignature = signature,
            ConsecutiveCount = window?.LastSignature == signature ? window.ConsecutiveCount + 1 : 1,
            Threshold = settings.ConsecutiveThreshold,
        };
    }

    public static ToolLoopDetectionResult DetectRepetitiveToolUse(ToolCallWindow? window)
    {
        if (window is null || window.ConsecutiveCount < window.Threshold)
        {
            return new ToolLoopDetectionResult { Triggered = false };
        }

        return new ToolLoopDetectionResult
        {
            Triggered = true,
            ToolName = window.LastSignature.Split("::", 2, StringSplitOptions.None)[0],
            RepeatedCount = window.ConsecutiveCount,
        };
    }

    private static JsonNode? SortNode(JsonNode? node)
    {
        return node switch
        {
            null => null,
            JsonObject obj => SortObject(obj),
            JsonArray array => new JsonArray(array.Select(SortNode).ToArray()),
            _ => node,
        };
    }

    private static JsonObject SortObject(JsonObject obj)
    {
        var sorted = new JsonObject();
        foreach (var property in obj.OrderBy(property => property.Key, StringComparer.Ordinal))
        {
            sorted[property.Key] = SortNode(property.Value)?.DeepClone();
        }

        return sorted;
    }
}
