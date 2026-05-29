namespace Lfe.Protocol.JsonRpc;

public static class ErrorCode
{
    public const int ParseError = -32700;
    public const int InvalidRequest = -32600;
    public const int MethodNotFound = -32601;
    public const int InvalidParams = -32602;
    public const int InternalError = -32603;
    public const int VersionMismatch = -32001;
    public const int RunFailure = -32010;
}

public static class LfeErrorCode
{
    public const string VersionMismatch = "LFE_VERSION_MISMATCH";
    public const string InvalidRequest = "LFE_INVALID_REQUEST";
    public const string RunFailed = "LFE_RUN_FAILED";
}
