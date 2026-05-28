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

    /// <summary>
    /// Initializes a new instance of the <see cref="CodexMcpToolServer"/> class.
    /// </summary>
    /// <param name="config">The resolved Codex configuration.</param>
    public CodexMcpToolServer(CodexResolvedConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        _host = new CodexUlwHost(config);
        _toolDescriptions = InitializeToolDescriptions();
    }

    /// <summary>
    /// Returns the list of available MCP tools.
    /// </summary>
    /// <returns>A dictionary of tool names and their descriptions.</returns>
    public IReadOnlyDictionary<string, string> ListTools() => _toolDescriptions;

    /// <summary>
    /// Dispatches a prompt to Codex via the CodexAdapter.
    /// MCP tool: codex_dispatch
    /// </summary>
    /// <param name="prompt">The prompt to dispatch.</param>
    /// <param name="sessionId">The optional session identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation, containing the tool result.</returns>
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
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation, containing the status result.</returns>
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
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation, containing the messages result.</returns>
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
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation, containing the tool result.</returns>
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
    /// <param name="listener">The event listener to register.</param>
    /// <returns>An action that unregisters the listener when invoked.</returns>
    public Action RegisterEventListener(Action<UlwSessionEvent> listener) => _host.OnEvent(listener);

    /// <inheritdoc />
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
/// <param name="ToolName">The name of the tool.</param>
/// <param name="Success">A value indicating whether the invocation was successful.</param>
/// <param name="SessionId">The session identifier.</param>
/// <param name="DispatchId">The dispatch identifier.</param>
/// <param name="Error">The error message, if any.</param>
public sealed record CodexMcpToolResult(
    string ToolName,
    bool Success,
    string? SessionId,
    string? DispatchId,
    string? Error);

/// <summary>
/// Status result from codex_read_status.
/// </summary>
/// <param name="ToolName">The name of the tool.</param>
/// <param name="SessionId">The session identifier.</param>
/// <param name="Status">The current status.</param>
public sealed record CodexMcpStatusResult(
    string ToolName,
    string SessionId,
    string Status);

/// <summary>
/// Messages result from codex_read_messages.
/// </summary>
/// <param name="ToolName">The name of the tool.</param>
/// <param name="SessionId">The session identifier.</param>
/// <param name="Messages">The list of messages.</param>
public sealed record CodexMcpMessagesResult(
    string ToolName,
    string SessionId,
    IReadOnlyList<CodexMcpMessage> Messages);

/// <summary>
/// Single message in an MCP messages result.
/// </summary>
/// <param name="Role">The role of the message sender.</param>
/// <param name="Content">The content of the message.</param>
public sealed record CodexMcpMessage(string Role, string Content);
