using Omodot.CodexAdapter;
using Omodot.UlwHostContract;

namespace Omodot.CodexMcpBridge;

/// <summary>
/// MCP tool server that exposes CodexAdapter capabilities as discoverable MCP tools.
/// This is the primary supported integration path for Codex CLI.
/// </summary>
public sealed class CodexMcpToolServer : IDisposable
{
    private readonly CodexUlwHost _host;
    private readonly Dictionary<string, string> _toolDescriptions;

    public CodexMcpToolServer(CodexResolvedConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        _host = new CodexUlwHost(config);
        _toolDescriptions = InitializeToolDescriptions();
    }

    /// <summary>
    /// Returns the list of available MCP tools.
    /// </summary>
    public IReadOnlyDictionary<string, string> ListTools() => _toolDescriptions;

    /// <summary>
    /// Dispatches a prompt to Codex via the CodexAdapter.
    /// MCP tool: codex_dispatch
    /// </summary>
    public async Task<CodexMcpToolResult> DispatchAsync(string prompt, string? sessionId = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);

        sessionId ??= Guid.NewGuid().ToString("N")[..16];

        var receipt = await _host.DispatchPromptAsync(new UlwPromptRequest(sessionId, prompt));

        return new CodexMcpToolResult(
            ToolName: "codex_dispatch",
            Success: receipt.Accepted,
            SessionId: receipt.SessionId,
            DispatchId: receipt.DispatchId,
            Error: receipt.Accepted ? null : "Dispatch was not accepted");
    }

    /// <summary>
    /// Reads the current status of a Codex session.
    /// MCP tool: codex_read_status
    /// </summary>
    public Task<CodexMcpStatusResult> ReadStatusAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        return _host.ReadStatusAsync(sessionId).ContinueWith(
            task => new CodexMcpStatusResult(
                ToolName: "codex_read_status",
                SessionId: sessionId,
                Status: task.Result),
            cancellationToken, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.Current);
    }

    /// <summary>
    /// Reads messages from a Codex session.
    /// MCP tool: codex_read_messages
    /// </summary>
    public async Task<CodexMcpMessagesResult> ReadMessagesAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        var messages = await _host.ReadMessagesAsync(sessionId);
        return new CodexMcpMessagesResult(
            ToolName: "codex_read_messages",
            SessionId: sessionId,
            Messages: [.. messages.Select(m => new CodexMcpMessage(m.Role, m.Text))]);
    }

    /// <summary>
    /// Aborts the current Codex operation.
    /// MCP tool: codex_abort
    /// </summary>
    public Task<CodexMcpToolResult> AbortAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        return _host.AbortAsync(sessionId).ContinueWith(
            _ => new CodexMcpToolResult(
                ToolName: "codex_abort",
                Success: true,
                SessionId: sessionId,
                DispatchId: null,
                Error: null),
            cancellationToken, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.Current);
    }

    /// <summary>
    /// Registers an event listener for Codex session events.
    /// </summary>
    public Action RegisterEventListener(Action<UlwSessionEvent> listener) => _host.OnEvent(listener);

    public void Dispose() => _host.Dispose();

    private static Dictionary<string, string> InitializeToolDescriptions() => new()
    {
        ["codex_dispatch"] = "Dispatch a prompt to Codex CLI. Parameters: prompt (string, required), sessionId (string, optional)",
        ["codex_read_status"] = "Read the current status of a Codex session. Parameters: sessionId (string, required)",
        ["codex_read_messages"] = "Read messages from a Codex session. Parameters: sessionId (string, required)",
        ["codex_abort"] = "Abort the current Codex operation. Parameters: sessionId (string, required)",
    };
}

/// <summary>
/// Result from an MCP tool invocation.
/// </summary>
public sealed record CodexMcpToolResult(
    string ToolName,
    bool Success,
    string? SessionId,
    string? DispatchId,
    string? Error);

/// <summary>
/// Status result from codex_read_status.
/// </summary>
public sealed record CodexMcpStatusResult(
    string ToolName,
    string SessionId,
    string Status);

/// <summary>
/// Messages result from codex_read_messages.
/// </summary>
public sealed record CodexMcpMessagesResult(
    string ToolName,
    string SessionId,
    IReadOnlyList<CodexMcpMessage> Messages);

/// <summary>
/// Single message in an MCP messages result.
/// </summary>
public sealed record CodexMcpMessage(string Role, string Content);
