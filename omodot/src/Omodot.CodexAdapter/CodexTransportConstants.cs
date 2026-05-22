namespace Omodot.CodexAdapter;

/// <summary>
/// Constants for the codex exec JSONL transport.
/// Transport: spawn codex exec --experimental-json and parse JSONL stdout.
/// </summary>
public static class CodexTransportConstants
{
    /// <summary>
    /// The codex CLI command name used for PATH lookup.
    /// </summary>
    public const string CodexBinaryName = "codex";

    /// <summary>
    /// Environment variable name for overriding the codex binary path.
    /// </summary>
    public const string CodexBinaryEnvVar = "CODEX_BINARY_PATH";

    /// <summary>
    /// The exec subcommand argument.
    /// </summary>
    public const string ExecArg = "exec";

    /// <summary>
    /// The experimental JSON output flag.
    /// </summary>
    public const string ExperimentalJsonFlag = "--experimental-json";

    /// <summary>
    /// Default timeout in milliseconds if not specified in options.
    /// </summary>
    public const int DefaultTimeoutMs = 300_000;
}
