namespace Omodot.UlwLoopState;

public sealed class UlwLoopStateController(IUlwLoopStateStore store)
{
    public UlwLoopState Start(StartUlwLoopOptions options)
    {
        var completionPromise = options.CompletionPromise ?? UlwLoopConstants.DefaultCompletionPromise;
        var state = new UlwLoopState(
            Active: true,
            Iteration: 1,
            MaxIterations: options.Ultrawork == true ? UlwLoopConstants.UltraworkMaxIterations : options.MaxIterations ?? UlwLoopConstants.DefaultMaxIterations,
            CompletionPromise: completionPromise,
            InitialCompletionPromise: completionPromise,
            StartedAt: options.Now?.Invoke() ?? DateTime.Now.ToString("O"),
            Prompt: options.Prompt,
            SessionID: options.SessionID,
            MessageCountAtStart: options.MessageCountAtStart,
            Strategy: options.Strategy ?? UlwLoopStrategy.Continue,
            Ultrawork: options.Ultrawork == true ? true : null);
        store.Write(state);
        return state;
    }

    public bool Cancel(string sessionId)
    {
        var state = store.Read();
        if (state is null || state.SessionID != sessionId) return false;
        store.Clear();
        return true;
    }

    public UlwLoopState? GetState() => store.Read();
    public void Clear() => store.Clear();

    public UlwLoopState? IncrementIteration(IterationExpectation? expected = null)
    {
        var state = store.Read();
        if (state is null) return null;
        if (expected is not null && (state.Iteration != expected.Iteration || state.SessionID != expected.SessionID)) return null;
        var next = state with { Iteration = state.Iteration + 1 };
        store.Write(next);
        return next;
    }

    public UlwLoopState? MarkVerificationPending(string sessionId, int? messageCountAtStart = null)
    {
        var state = store.Read();
        if (state is null || state.SessionID != sessionId || state.Ultrawork != true) return null;
        var next = state with
        {
            CompletionPromise = UlwLoopConstants.UltraworkVerificationPromise,
            MessageCountAtStart = messageCountAtStart ?? state.MessageCountAtStart,
            VerificationPending = true,
            VerificationAttemptID = null,
            VerificationSessionID = null,
        };
        store.Write(next);
        return next;
    }

    public UlwLoopState? SetSessionID(string sessionId, string nextSessionId)
    {
        var state = store.Read();
        if (state is null || state.SessionID != sessionId) return null;
        var next = state with { SessionID = nextSessionId };
        store.Write(next);
        return next;
    }

    public UlwLoopState? SetVerificationSessionID(string sessionId, string verificationSessionId)
    {
        var state = store.Read();
        if (state is null || state.SessionID != sessionId || state.Ultrawork != true || state.VerificationPending != true) return null;
        var next = state with { VerificationSessionID = verificationSessionId };
        store.Write(next);
        return next;
    }

    public UlwLoopState? RestartAfterFailedVerification(string sessionId, int? messageCountAtStart = null)
    {
        var state = store.Read();
        if (state is null || state.SessionID != sessionId || state.Ultrawork != true || state.VerificationPending != true) return null;
        var next = state with
        {
            Iteration = state.Iteration + 1,
            CompletionPromise = state.InitialCompletionPromise,
            StartedAt = DateTime.Now.ToString("O"),
            VerificationPending = null,
            VerificationAttemptID = null,
            VerificationSessionID = null,
            MessageCountAtStart = messageCountAtStart ?? state.MessageCountAtStart,
        };
        store.Write(next);
        return next;
    }

    public UlwLoopState? ClearVerificationState(string sessionId, int? messageCountAtStart = null)
    {
        var state = store.Read();
        if (state is null || state.SessionID != sessionId || state.Ultrawork != true || state.VerificationPending != true) return null;
        var next = state with
        {
            CompletionPromise = state.InitialCompletionPromise,
            StartedAt = DateTime.Now.ToString("O"),
            VerificationPending = null,
            VerificationAttemptID = null,
            VerificationSessionID = null,
            MessageCountAtStart = messageCountAtStart ?? state.MessageCountAtStart,
        };
        store.Write(next);
        return next;
    }
}
