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

public static class OmoErrorCode
{
    public const string VersionMismatch = "OMO_VERSION_MISMATCH";
    public const string InvalidRequest = "OMO_INVALID_REQUEST";
    public const string RunFailed = "OMO_RUN_FAILED";
}
