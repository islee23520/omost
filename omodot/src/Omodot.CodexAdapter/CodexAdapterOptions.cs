namespace Omodot.CodexAdapter;

/// <summary>
/// Configuration options for the Codex adapter.
/// Controls how the codex binary is discovered and invoked.
/// Transport: spawn codex exec --experimental-json and parse JSONL stdout.
/// </summary>
public sealed record CodexAdapterOptions
{
    /// <summary>
    /// Explicit path to the codex binary. When set, takes precedence over
    /// environment variable and PATH lookup.
    /// </summary>
    public string? CodexBinaryPath { get; init; }

    /// <summary>
    /// Working directory for the codex process. Defaults to current directory.
    /// </summary>
    public string? WorkingDirectory { get; init; }

    /// <summary>
    /// Timeout in milliseconds for the codex process. Null means no timeout.
    /// </summary>
    public int? TimeoutMs { get; init; }

    /// <summary>
    /// Additional environment variables to pass to the codex process.
    /// </summary>
    public IReadOnlyDictionary<string, string>? EnvironmentOverrides { get; init; }

    /// <summary>
    /// Session execution options that control how prompts are dispatched.
    /// </summary>
    public CodexSessionOptions SessionOptions { get; init; } = new();
}

/// <summary>
/// Options specific to a single codex session execution.
/// </summary>
public sealed record CodexSessionOptions
{
    /// <summary>
    /// Agent name to use for dispatch. Null uses codex default.
    /// </summary>
    public string? AgentName { get; init; }

    /// <summary>
    /// Model identifier to use for dispatch. Null uses codex default.
    /// </summary>
    public string? ModelId { get; init; }
}
