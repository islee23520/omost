using System.Text;
using System.Text.Json;

namespace Omodot.UlwHostContract;

public enum UlwSessionEventType
{
    Idle,
    Error,
    Deleted,
    Compacting,
    Completed,
}

public sealed record UlwSessionEvent(UlwSessionEventType Type, string SessionId, string? Error = null);

public sealed record UlwPromptRequest(
    string SessionId,
    string Message,
    string? AgentName = null,
    string? ModelId = null,
    string? PreviousResponseId = null,
    bool? StoreMessages = null,
    bool? UseEncryptedContent = null,
    string? ContinuationToken = null);

public sealed record UlwPromptReceipt(
    bool Accepted,
    string SessionId,
    string DispatchId,
    string? ResponseId = null,
    string? ContinuationToken = null,
    bool? AgenticStatePreserved = null);

public sealed record UlwMessage(string Role, string Text);

public sealed record UlwTodo(string Content, string Status);

public interface IUlwHost
{
    Task<UlwPromptReceipt> DispatchPromptAsync(UlwPromptRequest request);
    Task<IReadOnlyList<UlwMessage>> ReadMessagesAsync(string sessionId);
    Task<IReadOnlyList<UlwTodo>> ReadTodosAsync(string sessionId);
    Task<string> ReadStatusAsync(string sessionId);
    Task AbortAsync(string sessionId);
    Action OnEvent(Action<UlwSessionEvent> listener);
}

public static class OmoProtocolConstants
{
    public const string ProtocolVersion = "1.0.0";
    public const string HeaderSeparator = "\r\n\r\n";
    public static readonly IReadOnlyList<string> Phase1Capabilities =
    [
        "phase1.initialize",
        "phase1.session-start",
        "phase1.run-dispatch",
        "phase1.run-progress",
        "phase1.run-result",
        "phase1.run-cancel",
    ];
}

public static class OmoMethods
{
    public const string Initialize = "omo.initialize";
    public const string SessionStart = "omo.session.start";
    public const string RunDispatch = "omo.run.dispatch";
    public const string RunCancel = "omo.run.cancel";
}

public static class OmoNotifications
{
    public const string RunProgress = "omo.run.progress";
    public const string RunResult = "omo.run.result";
    public const string RunError = "omo.run.error";
}

public static class OmoJsonRpcErrorCodes
{
    public const int ParseError = -32700;
    public const int InvalidRequest = -32600;
    public const int MethodNotFound = -32601;
    public const int InvalidParams = -32602;
    public const int InternalError = -32603;
    public const int VersionMismatch = -32001;
    public const int RunFailed = -32010;
}

public static class OmoErrorCodes
{
    public const string VersionMismatch = "OMO_VERSION_MISMATCH";
    public const string InvalidRequest = "OMO_INVALID_REQUEST";
    public const string RunFailed = "OMO_RUN_FAILED";
}

public enum OmotsClientKind
{
    HostBridge,
    ImplementationToolkit,
}

public enum OmotsServerMode
{
    Standalone,
    Bridge,
}

public enum OmotsCancellationStatus
{
    Cancelled,
    Completed,
    Failed,
}

public enum OmotsRunPhase
{
    Queued,
    Running,
    Tool,
    Completed,
    Failed,
    Cancelled,
}

public enum OmotsRunStatus
{
    Completed,
    Failed,
    Cancelled,
}

public enum OmotsSessionState
{
    Created,
    Active,
    Completed,
}

public enum OmotsRunLifecycleState
{
    Accepted,
    Queued,
    Running,
    Completed,
    Failed,
    Cancelled,
}

public sealed record InitializeParams(
    string ProtocolVersion,
    string HostName,
    string HostVersion,
    OmotsClientKind ClientKind,
    IReadOnlyList<string> RequestedCapabilities);

public sealed record InitializeResult(
    string ProtocolVersion,
    string ImplementationName,
    IReadOnlyList<string> AcceptedCapabilities,
    OmotsServerMode ServerMode);

public sealed record SessionStartParams(
    string SessionId,
    string Cwd,
    IReadOnlyList<string>? Arguments = null,
    IReadOnlyDictionary<string, object?>? Metadata = null);

public sealed record SessionStartResult(string SessionId, bool Accepted);

public sealed record RunDispatchParams(
    string RunId,
    string SessionId,
    string Prompt,
    string? Agent = null,
    string? Model = null,
    string? ContinuationToken = null);

public sealed record RunDispatchResult(string RunId, bool Accepted);

public sealed record RunCancelParams(string RunId, string? Reason = null);

public sealed record RunCancelResult(string RunId, OmotsCancellationStatus Status);

public sealed record RunProgressParams(
    string RunId,
    OmotsRunPhase Phase,
    string? Message = null,
    int? Completed = null,
    int? Total = null);

public sealed record RunResultParams(
    string RunId,
    OmotsRunStatus Status,
    string? OutputText,
    IReadOnlyDictionary<string, object?>? OutputJson,
    string FinalSessionId);

public sealed record RunErrorParams(string RunId, int Code, string Message, bool Retryable);

public sealed record JsonRpcErrorData(string? OmoCode = null, bool? Retryable = null);

public sealed record JsonRpcError(int Code, string Message, JsonRpcErrorData? Data = null);

public sealed record JsonRpcSuccessResponse<TResult>(string Jsonrpc, object? Id, TResult Result);

public sealed record JsonRpcErrorResponse(string Jsonrpc, object? Id, JsonRpcError Error);

public sealed record JsonRpcRequest<TParams>(string Jsonrpc, object? Id, string Method, TParams Params);

public sealed record JsonRpcNotification<TParams>(string Jsonrpc, string Method, TParams Params);

public static class OmoProtocol
{
    private static readonly HashSet<string> Phase1CapabilitySet =
        [.. OmoProtocolConstants.Phase1Capabilities];

    private static readonly HashSet<OmotsRunLifecycleState> TerminalRunLifecycleStates =
    [
        OmotsRunLifecycleState.Completed,
        OmotsRunLifecycleState.Failed,
        OmotsRunLifecycleState.Cancelled,
    ];

    private static readonly HashSet<OmotsRunStatus> TerminalRunStatuses =
    [
        OmotsRunStatus.Completed,
        OmotsRunStatus.Failed,
        OmotsRunStatus.Cancelled,
    ];

    public static IReadOnlyList<string> GetAcceptedCapabilities(IEnumerable<string> requestedCapabilities)
    {
        var accepted = new List<string>();
        foreach (var capability in requestedCapabilities)
        {
            if (!Phase1CapabilitySet.Contains(capability) || accepted.Contains(capability, StringComparer.Ordinal))
                continue;

            accepted.Add(capability);
        }

        return accepted;
    }

    public static bool IsSupportedProtocolVersion(string protocolVersion)
        => string.Equals(protocolVersion, OmoProtocolConstants.ProtocolVersion, StringComparison.Ordinal);

    public static bool IsTerminalRunLifecycleState(OmotsRunLifecycleState state)
        => TerminalRunLifecycleStates.Contains(state);

    public static bool IsTerminalRunStatus(OmotsRunStatus status)
        => TerminalRunStatuses.Contains(status);

    public static int? ParseContentLength(string headers)
    {
        foreach (var line in headers.Split("\r\n", StringSplitOptions.None))
        {
            var separatorIndex = line.IndexOf(':');
            if (separatorIndex < 0)
                continue;

            var name = line[..separatorIndex].Trim();
            if (!string.Equals(name, "content-length", StringComparison.OrdinalIgnoreCase))
                continue;

            return int.TryParse(line[(separatorIndex + 1)..].Trim(), out var value) && value >= 0
                ? value
                : null;
        }

        return null;
    }

    public static string CreateContentLengthFrame<TMessage>(TMessage message)
    {
        var body = JsonSerializer.Serialize(message);
        return $"Content-Length: {Encoding.UTF8.GetByteCount(body)}{OmoProtocolConstants.HeaderSeparator}{body}";
    }

    public static string? GetInitializeValidationError(object? value)
    {
        if (!PropertyBag.TryCreate(value, out var bag)) return "initialize params must be an object";
        if (!bag.TryGetNonEmptyString("protocolVersion", out _)) return "protocolVersion must be a non-empty string";
        if (!bag.TryGetNonEmptyString("hostName", out _)) return "hostName must be a non-empty string";
        if (!bag.TryGetNonEmptyString("hostVersion", out _)) return "hostVersion must be a non-empty string";
        if (!bag.TryGetClientKind("clientKind")) return "clientKind must be host-bridge or implementation-toolkit";
        if (!bag.TryGetStringArray("requestedCapabilities")) return "requestedCapabilities must be an array of strings";
        return null;
    }

    public static string? GetSessionStartValidationError(object? value)
    {
        if (!PropertyBag.TryCreate(value, out var bag)) return "session.start params must be an object";
        if (!bag.TryGetNonEmptyString("sessionId", out _)) return "sessionId must be a non-empty string";
        if (!bag.TryGetNonEmptyString("cwd", out _)) return "cwd must be a non-empty string";
        if (bag.HasValue("arguments") && !bag.TryGetStringArray("arguments")) return "arguments must be an array of strings";
        if (bag.HasValue("metadata") && !bag.TryGetObject("metadata")) return "metadata must be an object";
        return null;
    }

    public static string? GetRunDispatchValidationError(object? value)
    {
        if (!PropertyBag.TryCreate(value, out var bag)) return "run.dispatch params must be an object";
        if (!bag.TryGetNonEmptyString("runId", out _)) return "runId must be a non-empty string";
        if (!bag.TryGetNonEmptyString("sessionId", out _)) return "sessionId must be a non-empty string";
        if (!bag.TryGetNonEmptyString("prompt", out _)) return "prompt must be a non-empty string";
        if (bag.HasValue("agent") && !bag.TryGetNonEmptyString("agent", out _)) return "agent must be a non-empty string";
        if (bag.HasValue("model") && !bag.TryGetNonEmptyString("model", out _)) return "model must be a non-empty string";
        if (bag.HasValue("continuationToken") && !bag.TryGetNonEmptyString("continuationToken", out _)) return "continuationToken must be a non-empty string";
        return null;
    }

    public static string? GetRunCancelValidationError(object? value)
    {
        if (!PropertyBag.TryCreate(value, out var bag)) return "run.cancel params must be an object";
        if (!bag.TryGetNonEmptyString("runId", out _)) return "runId must be a non-empty string";
        if (bag.HasValue("reason") && !bag.TryGetNonEmptyString("reason", out _)) return "reason must be a non-empty string";
        return null;
    }

    private sealed class PropertyBag
    {
        private readonly IReadOnlyDictionary<string, object?> _values;

        private PropertyBag(IReadOnlyDictionary<string, object?> values)
        {
            _values = values;
        }

        public static bool TryCreate(object? value, out PropertyBag bag)
        {
            bag = null!;

            if (value is JsonElement element)
            {
                if (element.ValueKind != JsonValueKind.Object) return false;
                bag = new PropertyBag(ToDictionary(element));
                return true;
            }

            if (value is IReadOnlyDictionary<string, object?> readOnlyDictionary)
            {
                bag = new PropertyBag(readOnlyDictionary);
                return true;
            }

            if (value is IDictionary<string, object?> dictionary)
            {
                bag = new PropertyBag(new Dictionary<string, object?>(dictionary, StringComparer.Ordinal));
                return true;
            }

            return false;
        }

        public bool HasValue(string name) => _values.TryGetValue(name, out var value) && value is not null;

        public bool TryGetNonEmptyString(string name, out string result)
        {
            result = string.Empty;
            if (!_values.TryGetValue(name, out var value)) return false;
            var text = value switch
            {
                JsonElement element when element.ValueKind == JsonValueKind.String => element.GetString(),
                string stringValue => stringValue,
                _ => null,
            };

            if (string.IsNullOrWhiteSpace(text)) return false;
            result = text;
            return true;
        }

        public bool TryGetStringArray(string name)
        {
            if (!_values.TryGetValue(name, out var value)) return false;
            return value switch
            {
                JsonElement element when element.ValueKind == JsonValueKind.Array => element.EnumerateArray().All(static entry => entry.ValueKind == JsonValueKind.String),
                IEnumerable<string> => true,
                IEnumerable<object?> items => items.All(static entry => entry is string),
                _ => false,
            };
        }

        public bool TryGetObject(string name)
        {
            if (!_values.TryGetValue(name, out var value)) return false;
            return value switch
            {
                JsonElement element => element.ValueKind == JsonValueKind.Object,
                IReadOnlyDictionary<string, object?> => true,
                IDictionary<string, object?> => true,
                _ => false,
            };
        }

        public bool TryGetClientKind(string name)
        {
            if (!TryGetNonEmptyString(name, out var clientKind)) return false;
            return clientKind is "host-bridge" or "implementation-toolkit";
        }

        private static IReadOnlyDictionary<string, object?> ToDictionary(JsonElement element)
        {
            var values = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
                values[property.Name] = property.Value;

            return values;
        }
    }
}
