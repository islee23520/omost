using System.Text.Json.Serialization;

using Lfe.Protocol.JsonRpc;

namespace Lfe.Protocol.Types;

public sealed record OmoErrorData
{
    [JsonPropertyName("omoCode")]
    public string OmoCode { get; init; } = string.Empty;

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
    public OmoErrorData? Data { get; init; }
}

public sealed class OmoProtocolException : Exception
{
    public OmoProtocolException(int code, string message, OmoErrorData? data = null)
        : base(message)
    {
        Code = code;
        ErrorData = data;
    }

    public int Code { get; }

    public OmoErrorData? ErrorData { get; }

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

public static class OmoProtocolErrors
{
    public static OmoProtocolException InternalError(string message)
    {
        return new OmoProtocolException(ErrorCode.InternalError, message);
    }

    public static OmoProtocolException InvalidParams(string message)
    {
        return new OmoProtocolException(ErrorCode.InvalidParams, message);
    }

    public static OmoProtocolException InvalidRequest(string message)
    {
        return new OmoProtocolException(
            ErrorCode.InvalidRequest,
            message,
            new OmoErrorData
            {
                OmoCode = OmoErrorCode.InvalidRequest,
                Retryable = false,
            });
    }

    public static OmoProtocolException MethodNotFound(string methodName)
    {
        return new OmoProtocolException(ErrorCode.MethodNotFound, $"Method not found: {methodName}");
    }

    public static OmoProtocolException ParseError(string message)
    {
        return new OmoProtocolException(ErrorCode.ParseError, message);
    }

    public static OmoProtocolException RunFailed(string message, bool retryable = false)
    {
        return new OmoProtocolException(
            ErrorCode.RunFailure,
            message,
            new OmoErrorData
            {
                OmoCode = OmoErrorCode.RunFailed,
                Retryable = retryable,
            });
    }

    public static OmoProtocolException VersionMismatch(string requestedVersion, string supportedVersion)
    {
        return new OmoProtocolException(
            ErrorCode.VersionMismatch,
            $"Protocol version mismatch: client {requestedVersion} is not supported",
            new OmoErrorData
            {
                OmoCode = OmoErrorCode.VersionMismatch,
                Retryable = false,
            });
    }
}
