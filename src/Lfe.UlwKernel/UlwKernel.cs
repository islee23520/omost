using Lfe.UlwHostContract;
using Lfe.UlwIntent;
using Lfe.UlwLoopState;
using UlwLoopStateRecord = Lfe.UlwLoopState.UlwLoopState;

namespace Lfe.UlwKernel;

public sealed record RunUlwInput(
    IUlwHost Host,
    string SessionId,
    string Text,
    string? AgentName = null,
    string? ModelId = null);

public sealed record RunUlwResult(
    bool Dispatched,
    IReadOnlyList<string> Intents,
    IReadOnlyList<UlwPromptReceipt> Receipts);

public sealed record RunTrackedUlwInput(
    IUlwHost Host,
    string SessionId,
    string Text,
    UlwLoopStateController LoopState,
    string? CompletionPromise = null,
    string? AgentName = null,
    string? ModelId = null);

public sealed record UlwLoopEngineOptions(IUlwHost Host, UlwLoopStateController LoopState);

public interface IUlwLoopEngine
{
    void Stop();
}

public static class UlwKernelRuntime
{
    public static async Task<RunUlwResult> RunUlwAsync(RunUlwInput input)
    {
        var intents = UlwIntentDetector.DetectUlwIntent(input.Text);
        var intentNames = new List<string>(intents.Count);
        var receipts = new List<UlwPromptReceipt>(intents.Count);

        foreach (var intent in intents)
        {
            intentNames.Add(ToIntentName(intent.Type));
            receipts.Add(await input.Host.DispatchPromptAsync(new UlwPromptRequest(
                input.SessionId,
                intent.Prompt,
                input.AgentName,
                input.ModelId)));
        }

        return new RunUlwResult(receipts.Any(static receipt => receipt.Accepted), intentNames, receipts);
    }

    public static async Task<RunUlwResult> RunTrackedUlwAsync(RunTrackedUlwInput input)
    {
        var messageCountAtStart = (await input.Host.ReadMessagesAsync(input.SessionId)).Count;
        var result = await RunUlwAsync(new RunUlwInput(input.Host, input.SessionId, input.Text, input.AgentName, input.ModelId));

        if (HasAcceptedTrackedUlwIntent(result))
        {
            input.LoopState.Start(new StartUlwLoopOptions(
                input.SessionId,
                input.Text,
                CompletionPromise: input.CompletionPromise,
                MessageCountAtStart: messageCountAtStart,
                Ultrawork: true));
        }

        return result;
    }

    public static IUlwLoopEngine CreateUlwLoopEngine(UlwLoopEngineOptions options)
    {
        var unsubscribe = options.Host.OnEvent(@event =>
        {
            if (@event.Type != UlwSessionEventType.Idle)
                return;

            _ = HandleUlwLoopIdleAsync(options, @event.SessionId);
        });

        return new SubscriptionUlwLoopEngine(unsubscribe);
    }

    public static async Task HandleUlwLoopIdleAsync(UlwLoopEngineOptions options, string sessionId)
    {
        var state = options.LoopState.GetState();
        if (state is null || !state.Active || !string.Equals(state.SessionID, sessionId, StringComparison.Ordinal))
            return;

        if (await CompletionDetectedAsync(options.Host, state, sessionId))
        {
            await HandleDetectedCompletionAsync(options, state, sessionId);
            return;
        }

        if (state.VerificationPending == true)
        {
            await HandlePendingVerificationAsync(options, state, sessionId);
            return;
        }

        if (state.Iteration >= state.MaxIterations)
        {
            options.LoopState.Clear();
            return;
        }

        var nextIteration = state.Iteration + 1;
        var receipt = await options.Host.DispatchPromptAsync(new UlwPromptRequest(
            sessionId,
            BuildContinuationPrompt(state with { Iteration = nextIteration })));

        if (!receipt.Accepted)
        {
            options.LoopState.Clear();
            return;
        }

        options.LoopState.IncrementIteration(new IterationExpectation(state.Iteration, sessionId));
    }

    public static string BuildContinuationPrompt(UlwLoopStateRecord state)
        => state.VerificationPending == true
            ? $"ultrawork [SYSTEM DIRECTIVE: OH-MY-OPENCODE - ULTRAWORK LOOP VERIFICATION {state.Iteration}/{state.MaxIterations}]\n\nYou already emitted <promise>{state.InitialCompletionPromise}</promise>. This does NOT finish the loop yet.\n\nREQUIRED NOW:\n- Call Oracle using task(subagent_type=\"oracle\", load_skills=[], run_in_background=false, ...)\n- Ask Oracle to verify whether the original task is actually complete\n- Include the original task in the Oracle request\n- Explicitly tell Oracle to review skeptically and critically, and to look for reasons the task may still be incomplete or wrong\n- The system will inspect the Oracle session directly for the verification result\n- If Oracle does not verify, continue fixing the task and do not consider it complete\n\nOriginal task:\n{state.Prompt}"
            : $"ultrawork [SYSTEM DIRECTIVE: OH-MY-OPENCODE - RALPH LOOP {state.Iteration}/{state.MaxIterations}]\nContinue. Output <promise>{state.CompletionPromise}</promise> when done.\n{state.Prompt}";

    public static string BuildVerificationFailurePrompt(UlwLoopStateRecord state)
        => $"ultrawork [SYSTEM DIRECTIVE: OH-MY-OPENCODE - ULTRAWORK LOOP VERIFICATION FAILED {state.Iteration}/{state.MaxIterations}]\n\nOracle did not emit <promise>VERIFIED</promise>. Verification failed.\n\nREQUIRED NOW:\n- Verification failed. Fix the task until Oracle's review is satisfied\n- Oracle does not lie. Treat the verification result as ground truth\n- Do not claim completion early or argue with the failed verification\n- After fixing the remaining issues, request Oracle review again using task(subagent_type=\"oracle\", load_skills=[], run_in_background=false, ...)\n- Include the original task in the Oracle request and tell Oracle to review skeptically and critically\n- Only when the work is ready for review again, output: <promise>{state.InitialCompletionPromise}</promise>\n\nOriginal task:\n{state.Prompt}";

    private static bool HasAcceptedTrackedUlwIntent(RunUlwResult result)
    {
        for (var index = 0; index < result.Intents.Count; index++)
        {
            if (IsTrackedUlwIntent(result.Intents[index]) && result.Receipts[index].Accepted)
                return true;
        }

        return false;
    }

    private static bool IsTrackedUlwIntent(string intent)
        => intent is "ultrawork" or "hyperplan-ultrawork";

    private static string ToIntentName(UlwIntentType type) => type switch
    {
        UlwIntentType.HyperplanUltrawork => "hyperplan-ultrawork",
        UlwIntentType.Hyperplan => "hyperplan",
        _ => "ultrawork",
    };

    private static async Task<bool> CompletionDetectedAsync(IUlwHost host, UlwLoopStateRecord state, string sessionId)
    {
        var messages = await host.ReadMessagesAsync(sessionId);
        return messages
            .Skip(state.MessageCountAtStart ?? 0)
            .Any(message => string.Equals(message.Role, "assistant", StringComparison.Ordinal) &&
                            message.Text.Contains($"<promise>{state.CompletionPromise}</promise>", StringComparison.Ordinal));
    }

    private static async Task HandleDetectedCompletionAsync(UlwLoopEngineOptions options, UlwLoopStateRecord state, string sessionId)
    {
        if (state.Ultrawork == true && state.VerificationPending != true)
        {
            var verificationMessageCountAtStart = (await options.Host.ReadMessagesAsync(sessionId)).Count;
            var verificationState = options.LoopState.MarkVerificationPending(sessionId, verificationMessageCountAtStart);
            if (verificationState is null)
                return;

            var receipt = await options.Host.DispatchPromptAsync(new UlwPromptRequest(sessionId, BuildContinuationPrompt(verificationState)));
            if (!receipt.Accepted)
                options.LoopState.Clear();

            return;
        }

        options.LoopState.Clear();
    }

    private static async Task HandlePendingVerificationAsync(UlwLoopEngineOptions options, UlwLoopStateRecord state, string sessionId)
    {
        if (await OracleVerifiedAsync(options.Host, state, sessionId))
        {
            options.LoopState.Clear();
            return;
        }

        if (state.Iteration >= state.MaxIterations)
        {
            options.LoopState.Clear();
            return;
        }

        var messageCountAtStart = (await options.Host.ReadMessagesAsync(sessionId)).Count;
        var previewState = state with
        {
            Iteration = state.Iteration + 1,
            VerificationPending = null,
            VerificationSessionID = null,
            MessageCountAtStart = messageCountAtStart,
        };

        var receipt = await options.Host.DispatchPromptAsync(new UlwPromptRequest(sessionId, BuildVerificationFailurePrompt(previewState)));
        if (!receipt.Accepted)
        {
            options.LoopState.Clear();
            return;
        }

        var cleared = options.LoopState.ClearVerificationState(sessionId, messageCountAtStart);
        if (cleared is null)
        {
            options.LoopState.Clear();
            return;
        }

        if (options.LoopState.IncrementIteration(new IterationExpectation(cleared.Iteration, sessionId)) is null)
            options.LoopState.Clear();
    }

    private static async Task<bool> OracleVerifiedAsync(IUlwHost host, UlwLoopStateRecord state, string sessionId)
    {
        var messages = await host.ReadMessagesAsync(state.VerificationSessionID ?? sessionId);
        return messages
            .Skip(state.MessageCountAtStart ?? 0)
            .Any(message => string.Equals(message.Role, "assistant", StringComparison.Ordinal) &&
                            message.Text.Contains("<promise>VERIFIED</promise>", StringComparison.Ordinal));
    }

    private sealed class SubscriptionUlwLoopEngine(Action unsubscribe) : IUlwLoopEngine
    {
        public void Stop() => unsubscribe();
    }
}
