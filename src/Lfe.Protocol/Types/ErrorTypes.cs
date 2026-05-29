using System.Text.Json.Serialization;

using Lfe.Protocol.JsonRpc;

namespace Lfe.Protocol.Types;

public sealed record LfeErrorData
{
    [JsonPropertyName("lfeCode")]
    public string LfeCode { get; init; } = string.Empty;

    [JsonPropertyName("retryable")]
    public bool Retryable { get; init; }
}

public sealed record JsonRpcError
{
    [JsonPropertyName("code")]
    public int Code { get; init; }

    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;

    [JsonPropertyName("data")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public LfeErrorData? Data { get; init; }
}

public sealed class LfeProtocolException : Exception
{
    public LfeProtocolException(int code, string message, LfeErrorData? data = null)
        : base(message)
    {
        Code = code;
        ErrorData = data;
    }

    public int Code { get; }

    public LfeErrorData? ErrorData { get; }

    public JsonRpcError ToJsonRpcError()
    {
        return new JsonRpcError
        {
            Code = Code,
            Data = ErrorData,
            Message = Message,
        };
    }
}

public static class LfeProtocolErrors
{
    public static LfeProtocolException InternalError(string message)
    {
        return new LfeProtocolException(ErrorCode.InternalError, message);
    }

    public static LfeProtocolException InvalidParams(string message)
    {
        return new LfeProtocolException(ErrorCode.InvalidParams, message);
    }

    public static LfeProtocolException InvalidRequest(string message)
    {
        return new LfeProtocolException(
            ErrorCode.InvalidRequest,
            message,
            new LfeErrorData
            {
                LfeCode = LfeErrorCode.InvalidRequest,
                Retryable = false,
            });
    }

    public static LfeProtocolException MethodNotFound(string methodName)
    {
        return new LfeProtocolException(ErrorCode.MethodNotFound, $"Method not found: {methodName}");
    }

    public static LfeProtocolException ParseError(string message)
    {
        return new LfeProtocolException(ErrorCode.ParseError, message);
    }

    public static LfeProtocolException RunFailed(string message, bool retryable = false)
    {
        return new LfeProtocolException(
            ErrorCode.RunFailure,
            message,
            new LfeErrorData
            {
                LfeCode = LfeErrorCode.RunFailed,
                Retryable = retryable,
            });
    }

    public static LfeProtocolException VersionMismatch(string requestedVersion, string supportedVersion)
    {
        return new LfeProtocolException(
            ErrorCode.VersionMismatch,
            $"Protocol version mismatch: client {requestedVersion} is not supported",
            new LfeErrorData
            {
                LfeCode = LfeErrorCode.VersionMismatch,
                Retryable = false,
            });
    }
}
