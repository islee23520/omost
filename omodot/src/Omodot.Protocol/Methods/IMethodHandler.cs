using System.Text.Json;

using Omodot.Protocol.JsonRpc;
using Omodot.Protocol.Types;

namespace Omodot.Protocol.Methods;

public interface IMethodHandler
{
    string MethodName { get; }

    Task<object> HandleAsync(JsonElement paramsElement, CancellationToken cancellationToken);
}

public abstract class MethodHandlerBase<TParams, TResult> : IMethodHandler
{
    private readonly JsonSerializerOptions _serializerOptions;

    protected MethodHandlerBase(string methodName, JsonSerializerOptions? serializerOptions = null)
    {
        MethodName = methodName;
        _serializerOptions = serializerOptions ?? JsonRpcProtocol.SerializerOptions;
    }

    public string MethodName { get; }

    public async Task<object> HandleAsync(JsonElement paramsElement, CancellationToken cancellationToken)
    {
        if (paramsElement.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            throw OmoProtocolErrors.InvalidRequest($"Method '{MethodName}' requires a params object.");
        }

        var parameters = paramsElement.Deserialize<TParams>(_serializerOptions);
        if (parameters is null)
        {
            throw OmoProtocolErrors.InvalidRequest($"Method '{MethodName}' could not deserialize params.");
        }

        Validate(parameters);
        var result = await HandleTypedAsync(parameters, cancellationToken).ConfigureAwait(false);
        return (object)result!;
    }

    protected virtual void Validate(TParams parameters)
    {
    }

    protected abstract ValueTask<TResult> HandleTypedAsync(TParams parameters, CancellationToken cancellationToken);
}

public sealed class OmodotServerState
{
    private readonly object _gate = new();
    private readonly Dictionary<string, RunRecord> _runs = new(StringComparer.Ordinal);
    private readonly HashSet<string> _sessions = new(StringComparer.Ordinal);

    public bool HasSession(string sessionId)
    {
        lock (_gate)
        {
            return _sessions.Contains(sessionId);
        }
    }

    public RunRecord RecordRun(string runId, string sessionId, string status)
    {
        lock (_gate)
        {
            var record = new RunRecord(runId, sessionId, status);
            _runs[runId] = record;
            return record;
        }
    }

    public bool StartSession(string sessionId)
    {
        lock (_gate)
        {
            return _sessions.Add(sessionId);
        }
    }

    public bool TryGetRun(string runId, out RunRecord? runRecord)
    {
        lock (_gate)
        {
            if (_runs.TryGetValue(runId, out var existingRecord))
            {
                runRecord = existingRecord;
                return true;
            }
        }

        runRecord = null;
        return false;
    }

    public void UpdateRunStatus(string runId, string status)
    {
        lock (_gate)
        {
            if (_runs.TryGetValue(runId, out var existingRecord))
            {
                existingRecord.Status = status;
            }
        }
    }

    public bool TryCancelRun(string runId, string? reason, out RunRecord? runRecord)
    {
        lock (_gate)
        {
            if (_runs.TryGetValue(runId, out var existingRecord))
            {
                existingRecord.CancellationReason = reason;
                existingRecord.CancellationTokenSource.Cancel();
                runRecord = existingRecord;
                return true;
            }
        }

        runRecord = null;
        return false;
    }

    public sealed class RunRecord
    {
        public RunRecord(string runId, string sessionId, string status)
        {
            RunId = runId;
            SessionId = sessionId;
            Status = status;
        }

        public string? CancellationReason { get; set; }

        public CancellationTokenSource CancellationTokenSource { get; } = new();

        public string RunId { get; }

        public string SessionId { get; }

        public string Status { get; set; }
    }
}

internal static class RequestValidator
{
    public static void RequireNonEmptyString(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw OmoProtocolErrors.InvalidRequest($"{fieldName} must be a non-empty string");
        }
    }

    public static void RequireStringArray(string[]? values, string fieldName)
    {
        if (values is null)
        {
            throw OmoProtocolErrors.InvalidRequest($"{fieldName} must be an array of strings");
        }

        if (values.Any(static value => value is null))
        {
            throw OmoProtocolErrors.InvalidRequest($"{fieldName} must be an array of strings");
        }
    }
}
