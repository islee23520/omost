using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lfe.CodexAdapter;

/// <summary>
/// Raw JSONL line from codex exec --experimental-json output.
/// </summary>
public sealed record CodexJsonlLine
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("session_id")]
    public string? SessionId { get; init; }

    [JsonPropertyName("thread_id")]
    public string? ThreadId { get; init; }

    [JsonPropertyName("role")]
    public string? Role { get; init; }

    [JsonPropertyName("content")]
    public string? Content { get; init; }

    [JsonPropertyName("status")]
    public string? Status { get; init; }

    [JsonPropertyName("error")]
    public string? Error { get; init; }

    [JsonPropertyName("item")]
    public CodexItemLine? Item { get; init; }

    /// <summary>
    /// Raw JSON for any fields not explicitly mapped.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

public sealed record CodexItemLine
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("type")]
    public string? Type { get; init; }

    [JsonPropertyName("text")]
    public string? Text { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

/// <summary>
/// Normalized adapter event derived from a JSONL line.
/// </summary>
public sealed record CodexAdapterEvent
{
    public CodexAdapterEventType EventType { get; init; }
    public string? SessionId { get; init; }
    public string? ThreadId { get; init; }
    public string? Role { get; init; }
    public string? Content { get; init; }
    public string? Status { get; init; }
    public string? Error { get; init; }
    public string? ItemId { get; init; }
    public JsonElement? RawData { get; init; }
}

public enum CodexAdapterEventType
{
    Unknown,
    Message,
    Status,
    Error,
    Idle,
    Completed,
}
