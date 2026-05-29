using System.Text.Json.Serialization;

namespace Lfe.Protocol.Types;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum UlwSessionEventType
{
    [JsonStringEnumMemberName("idle")]
    Idle,

    [JsonStringEnumMemberName("error")]
    Error,

    [JsonStringEnumMemberName("deleted")]
    Deleted,

    [JsonStringEnumMemberName("compacting")]
    Compacting,

    [JsonStringEnumMemberName("completed")]
    Completed,
}

public sealed record UlwSessionEvent
{
    [JsonPropertyName("type")]
    public UlwSessionEventType Type { get; init; }

    [JsonPropertyName("sessionID")]
    public string SessionID { get; init; } = string.Empty;

    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Error { get; init; }
}

public sealed record UlwPromptRequest
{
    [JsonPropertyName("sessionID")]
    public string SessionID { get; init; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;

    [JsonPropertyName("agentName")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AgentName { get; init; }

    [JsonPropertyName("modelID")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ModelID { get; init; }

    [JsonPropertyName("previousResponseId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PreviousResponseId { get; init; }

    [JsonPropertyName("storeMessages")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? StoreMessages { get; init; }

    [JsonPropertyName("useEncryptedContent")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? UseEncryptedContent { get; init; }

    [JsonPropertyName("continuationToken")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ContinuationToken { get; init; }
}

public sealed record UlwPromptReceipt
{
    [JsonPropertyName("accepted")]
    public bool Accepted { get; init; }

    [JsonPropertyName("sessionID")]
    public string SessionID { get; init; } = string.Empty;

    [JsonPropertyName("dispatchID")]
    public string DispatchID { get; init; } = string.Empty;

    [JsonPropertyName("responseId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ResponseId { get; init; }

    [JsonPropertyName("continuationToken")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ContinuationToken { get; init; }

    [JsonPropertyName("agenticStatePreserved")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? AgenticStatePreserved { get; init; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum UlwMessageRole
{
    [JsonStringEnumMemberName("user")]
    User,

    [JsonStringEnumMemberName("assistant")]
    Assistant,

    [JsonStringEnumMemberName("system")]
    System,

    [JsonStringEnumMemberName("tool")]
    Tool,
}

public sealed record UlwMessage
{
    [JsonPropertyName("role")]
    public UlwMessageRole Role { get; init; }

    [JsonPropertyName("text")]
    public string Text { get; init; } = string.Empty;
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum UlwTodoStatus
{
    [JsonStringEnumMemberName("pending")]
    Pending,

    [JsonStringEnumMemberName("in_progress")]
    InProgress,

    [JsonStringEnumMemberName("completed")]
    Completed,

    [JsonStringEnumMemberName("cancelled")]
    Cancelled,
}

public sealed record UlwTodo
{
    [JsonPropertyName("content")]
    public string Content { get; init; } = string.Empty;

    [JsonPropertyName("status")]
    public UlwTodoStatus Status { get; init; }
}

public static class LfeProtocolConstants
{
    public const string HeaderSeparator = "\r\n\r\n";
}
