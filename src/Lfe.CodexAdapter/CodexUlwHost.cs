using Lfe.UlwHostContract;

namespace Lfe.CodexAdapter;

public sealed class CodexUlwHost : IUlwHost, IDisposable
{
    private readonly Func<CodexResolvedConfig> _configFactory;
    private readonly Dictionary<string, CodexSessionState> _sessions = [];
    private readonly List<Action<UlwSessionEvent>> _eventListeners = [];
    private CancellationTokenSource? _currentCts;

    public CodexUlwHost(CodexResolvedConfig config) : this(() => config) { }

    public CodexUlwHost(Func<CodexResolvedConfig> configFactory)
    {
        ArgumentNullException.ThrowIfNull(configFactory);
        _configFactory = configFactory;
    }

    public async Task<UlwPromptReceipt> DispatchPromptAsync(UlwPromptRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var config = _configFactory();
        using var runner = new CodexProcessRunner(config);
        _currentCts = new CancellationTokenSource();

        try
        {
            var result = await runner.ExecuteAsync(request.Message, _currentCts.Token);
            var sessionState = UpdateSession(request.SessionId, result);
            ForwardEvents(request.SessionId, result.Events);
            return new UlwPromptReceipt(
                Accepted: result.ExitCode == 0 && !result.TimedOut,
                SessionId: request.SessionId,
                DispatchId: result.Events.FirstOrDefault(e => e.ItemId is not null)?.ItemId ?? request.SessionId);
        }
        catch (OperationCanceledException)
        {
            return new UlwPromptReceipt(Accepted: false, SessionId: request.SessionId, DispatchId: request.SessionId);
        }
        finally
        {
            _currentCts.Dispose();
            _currentCts = null;
        }
    }

    public Task<IReadOnlyList<UlwMessage>> ReadMessagesAsync(string sessionId)
    {
        if (_sessions.TryGetValue(sessionId, out var state))
            return Task.FromResult<IReadOnlyList<UlwMessage>>(state.Messages);

        return Task.FromResult<IReadOnlyList<UlwMessage>>([]);
    }

    public Task<IReadOnlyList<UlwTodo>> ReadTodosAsync(string sessionId)
        => Task.FromResult<IReadOnlyList<UlwTodo>>([]);

    public Task<string> ReadStatusAsync(string sessionId)
    {
        if (_sessions.TryGetValue(sessionId, out var state))
            return Task.FromResult(state.Status);

        return Task.FromResult("unknown");
    }

    public Task AbortAsync(string sessionId)
    {
        _currentCts?.Cancel();
        return Task.CompletedTask;
    }

    public Action OnEvent(Action<UlwSessionEvent> listener)
    {
        ArgumentNullException.ThrowIfNull(listener);
        _eventListeners.Add(listener);
        return () => _eventListeners.Remove(listener);
    }

    private CodexSessionState UpdateSession(string sessionId, CodexRunResult result)
    {
        var messages = result.Events
            .Where(e => e.EventType == CodexAdapterEventType.Message && e.Role is not null && e.Content is not null)
            .Select(e => new UlwMessage(e.Role!, e.Content!))
            .ToList();

        var status = MapStatus(result);

        var state = new CodexSessionState(messages, status);
        _sessions[sessionId] = state;
        return state;
    }

    private void ForwardEvents(string sessionId, IReadOnlyList<CodexAdapterEvent> events)
    {
        foreach (var evt in events)
        {
            var sessionEvent = MapEvent(sessionId, evt);
            if (sessionEvent is null) continue;

            foreach (var listener in _eventListeners)
                listener(sessionEvent);
        }
    }

    private static string MapStatus(CodexRunResult result)
    {
        if (result.TimedOut) return "timed_out";
        if (result.ExitCode != 0) return "failed";

        var hasCompleted = result.Events.Any(e => e.EventType == CodexAdapterEventType.Completed);
        if (hasCompleted) return "completed";

        var hasIdle = result.Events.Any(e => e.EventType == CodexAdapterEventType.Idle);
        if (hasIdle) return "idle";

        return "unknown";
    }

    private static UlwSessionEvent? MapEvent(string sessionId, CodexAdapterEvent evt)
    {
        return evt.EventType switch
        {
            CodexAdapterEventType.Idle => new UlwSessionEvent(UlwSessionEventType.Idle, sessionId),
            CodexAdapterEventType.Completed => new UlwSessionEvent(UlwSessionEventType.Completed, sessionId),
            CodexAdapterEventType.Error => new UlwSessionEvent(UlwSessionEventType.Error, sessionId, evt.Error),
            _ => null,
        };
    }

    public void Dispose()
    {
        _currentCts?.Cancel();
        _currentCts?.Dispose();
    }

    private sealed class CodexSessionState(List<UlwMessage> messages, string status)
    {
        public List<UlwMessage> Messages { get; } = messages;
        public string Status { get; } = status;
    }
}
