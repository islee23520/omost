using System.Text.Json;
using System.Text.Json.Serialization;

namespace Omodot.Protocol.Types;

public static class OmoProtocolInfo
{
    public const string ImplementationName = "omodot";
    public const string ProtocolVersion = "1.0.0";
}

public static class OmoMethodNames
{
    public const string Initialize = "omo.initialize";
    public const string SessionStart = "omo.session.start";
    public const string RunDispatch = "omo.run.dispatch";
    public const string RunCancel = "omo.run.cancel";

    public static IReadOnlyList<string> All { get; } =
        new[] { Initialize, SessionStart, RunDispatch, RunCancel };
}

public static class OmoNotificationNames
{
    public const string RunProgress = "omo.run.progress";
    public const string RunResult = "omo.run.result";
    public const string RunError = "omo.run.error";

    public static IReadOnlyList<string> All { get; } =
        new[] { RunProgress, RunResult, RunError };
}

public static class OmoCapabilityNames
{
    public const string Initialize = "phase1.initialize";
    public const string SessionStart = "phase1.session-start";
    public const string RunDispatch = "phase1.run-dispatch";
    public const string RunProgress = "phase1.run-progress";
    public const string RunResult = "phase1.run-result";
    public const string RunCancel = "phase1.run-cancel";

    public static IReadOnlyList<string> All { get; } =
        new[] { Initialize, SessionStart, RunDispatch, RunProgress, RunResult, RunCancel };
}

public static class OmoClientKinds
{
    public const string HostBridge = "host-bridge";
    public const string ImplementationToolkit = "implementation-toolkit";

    public static bool IsValid(string? value)
    {
        return string.Equals(value, HostBridge, StringComparison.Ordinal) ||
               string.Equals(value, ImplementationToolkit, StringComparison.Ordinal);
    }
}

public static class OmoServerModes
{
    public const string Standalone = "standalone";
    public const string Bridge = "bridge";
}

public static class OmoRunPhaseValues
{
    public const string Queued = "queued";
    public const string Running = "running";
    public const string Tool = "tool";
    public const string Completed = "completed";
    public const string Failed = "failed";
    public const string Cancelled = "cancelled";

    public static IReadOnlyList<string> All { get; } =
        new[] { Queued, Running, Tool, Completed, Failed, Cancelled };
}

public static class OmoRunStatusValues
{
    public const string Completed = "completed";
    public const string Failed = "failed";
    public const string Cancelled = "cancelled";

    public static IReadOnlyList<string> All { get; } =
        new[] { Completed, Failed, Cancelled };

    public static bool IsTerminal(string? value)
    {
        return string.Equals(value, Completed, StringComparison.Ordinal) ||
               string.Equals(value, Failed, StringComparison.Ordinal) ||
               string.Equals(value, Cancelled, StringComparison.Ordinal);
    }
}

public sealed record InitializeRequestParams
{
    [JsonPropertyName("protocolVersion")]
    public string ProtocolVersion { get; init; } = string.Empty;

    [JsonPropertyName("hostName")]
    public string HostName { get; init; } = string.Empty;

    [JsonPropertyName("hostVersion")]
    public string HostVersion { get; init; } = string.Empty;

    [JsonPropertyName("clientKind")]
    public string ClientKind { get; init; } = string.Empty;

    [JsonPropertyName("requestedCapabilities")]
    public string[] RequestedCapabilities { get; init; } = Array.Empty<string>();
}

public sealed record InitializeResult
{
    [JsonPropertyName("protocolVersion")]
    public string ProtocolVersion { get; init; } = string.Empty;

    [JsonPropertyName("implementationName")]
    public string ImplementationName { get; init; } = string.Empty;

    [JsonPropertyName("acceptedCapabilities")]
    public string[] AcceptedCapabilities { get; init; } = Array.Empty<string>();

    [JsonPropertyName("serverMode")]
    public string ServerMode { get; init; } = OmoServerModes.Standalone;
}

public sealed record SessionStartRequestParams
{
    [JsonPropertyName("sessionId")]
    public string SessionId { get; init; } = string.Empty;

    [JsonPropertyName("cwd")]
    public string Cwd { get; init; } = string.Empty;

    [JsonPropertyName("arguments")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string[]? Arguments { get; init; }

    [JsonPropertyName("metadata")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Metadata { get; init; }
}

public sealed record SessionStartResult
{
    [JsonPropertyName("sessionId")]
    public string SessionId { get; init; } = string.Empty;

    [JsonPropertyName("accepted")]
    public bool Accepted { get; init; }
}

public sealed record RunDispatchRequestParams
{
    [JsonPropertyName("runId")]
    public string RunId { get; init; } = string.Empty;

    [JsonPropertyName("sessionId")]
    public string SessionId { get; init; } = string.Empty;

    [JsonPropertyName("prompt")]
    public string Prompt { get; init; } = string.Empty;

    [JsonPropertyName("agent")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Agent { get; init; }

    [JsonPropertyName("model")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Model { get; init; }

    [JsonPropertyName("continuationToken")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ContinuationToken { get; init; }
}

public sealed record RunDispatchResult
{
    [JsonPropertyName("runId")]
    public string RunId { get; init; } = string.Empty;

    [JsonPropertyName("accepted")]
    public bool Accepted { get; init; }
}

public sealed record RunCancelRequestParams
{
    [JsonPropertyName("runId")]
    public string RunId { get; init; } = string.Empty;

    [JsonPropertyName("reason")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Reason { get; init; }
}

public sealed record RunCancelResult
{
    [JsonPropertyName("runId")]
    public string RunId { get; init; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; init; } = OmoRunStatusValues.Cancelled;
}

public sealed record RunProgressParams
{
    [JsonPropertyName("runId")]
    public string RunId { get; init; } = string.Empty;

    [JsonPropertyName("phase")]
    public string Phase { get; init; } = OmoRunPhaseValues.Queued;

    [JsonPropertyName("message")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Message { get; init; }

    [JsonPropertyName("completed")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Completed { get; init; }

    [JsonPropertyName("total")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Total { get; init; }
}

public sealed record RunResultParams
{
    [JsonPropertyName("runId")]
    public string RunId { get; init; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; init; } = OmoRunStatusValues.Completed;

    [JsonPropertyName("outputText")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? OutputText { get; init; }

    [JsonPropertyName("outputJson")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? OutputJson { get; init; }

    [JsonPropertyName("finalSessionId")]
    public string FinalSessionId { get; init; } = string.Empty;
}

public sealed record RunErrorParams
{
    [JsonPropertyName("runId")]
    public string RunId { get; init; } = string.Empty;

    [JsonPropertyName("code")]
    public int Code { get; init; }

    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;

    [JsonPropertyName("retryable")]
    public bool Retryable { get; init; }
}
