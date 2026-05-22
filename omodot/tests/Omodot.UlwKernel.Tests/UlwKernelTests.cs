using Omodot.UlwHostContract;
using Omodot.UlwKernel;
using Omodot.UlwLoopState;
using UlwLoopStateRecord = Omodot.UlwLoopState.UlwLoopState;

using Xunit;

namespace Omodot.UlwKernel.Tests;

public class UlwKernelTests
{
    [Fact]
    public void BuildContinuationPrompt_RendersCurrentLoopState()
    {
        var prompt = UlwKernelRuntime.BuildContinuationPrompt(new UlwLoopStateRecord(
            true,
            3,
            10,
            "DONE",
            "DONE",
            "2026-01-01T00:00:00.000Z",
            "ship the feature",
            "ses_1",
            Strategy: UlwLoopStrategy.Continue,
            Ultrawork: true));

        Assert.Contains("RALPH LOOP 3/10", prompt);
        Assert.Contains("<promise>DONE</promise>", prompt);
        Assert.Contains("ship the feature", prompt);
    }

    [Fact]
    public void BuildContinuationPrompt_RendersVerificationPrompt()
    {
        var prompt = UlwKernelRuntime.BuildContinuationPrompt(new UlwLoopStateRecord(
            true,
            2,
            10,
            "VERIFIED",
            "DONE",
            "2026-01-01T00:00:00.000Z",
            "ship the feature",
            "ses_1",
            VerificationPending: true,
            Strategy: UlwLoopStrategy.Continue,
            Ultrawork: true));

        Assert.Contains("ULTRAWORK LOOP VERIFICATION 2/10", prompt);
        Assert.Contains("You already emitted <promise>DONE</promise>", prompt);
        Assert.Contains("Original task:", prompt);
    }

    [Fact]
    public void BuildVerificationFailurePrompt_RendersExpectedText()
    {
        var prompt = UlwKernelRuntime.BuildVerificationFailurePrompt(new UlwLoopStateRecord(
            true,
            4,
            10,
            "VERIFIED",
            "DONE",
            "2026-01-01T00:00:00.000Z",
            "ship the feature",
            "ses_1",
            VerificationPending: true,
            Strategy: UlwLoopStrategy.Continue,
            Ultrawork: true));

        Assert.Contains("ULTRAWORK LOOP VERIFICATION FAILED 4/10", prompt);
        Assert.Contains("<promise>DONE</promise>", prompt);
        Assert.Contains("ship the feature", prompt);
    }

    [Fact]
    public async Task RunUlwAsync_DispatchesDetectedPrompts()
    {
        var requests = new List<UlwPromptRequest>();
        var host = CreateTestHost(requests: requests);

        var result = await UlwKernelRuntime.RunUlwAsync(new RunUlwInput(host, "ses_1", "please ulw"));

        Assert.True(result.Dispatched);
        Assert.Equal(["ultrawork"], result.Intents);
        Assert.Equal([new UlwPromptRequest("ses_1", "ULTRAWORK MODE ENABLED!")], requests);
    }

    [Fact]
    public async Task RunTrackedUlwAsync_StartsTrackedLoopAfterSuccessfulDispatch()
    {
        var loopState = new UlwLoopStateController(new MemoryUlwLoopStateStore());
        var requests = new List<UlwPromptRequest>();
        var host = CreateTestHost(
            requests: requests,
            messages: [new UlwMessage("assistant", "old <promise>DONE</promise>")]);

        var result = await UlwKernelRuntime.RunTrackedUlwAsync(new RunTrackedUlwInput(host, "ses_1", "please ulw", loopState));

        Assert.True(result.Dispatched);
        Assert.Equal(UlwLoopConstants.UltraworkMaxIterations, loopState.GetState()?.MaxIterations);
        Assert.Equal(1, loopState.GetState()?.MessageCountAtStart);
        Assert.Single(requests);
    }

    [Fact]
    public async Task RunTrackedUlwAsync_DoesNotStartStateWithoutUltraworkIntent()
    {
        var loopState = new UlwLoopStateController(new MemoryUlwLoopStateStore());
        var host = CreateTestHost(dispatchPromptAsync: _ => throw new InvalidOperationException("dispatch should not run"));

        var result = await UlwKernelRuntime.RunTrackedUlwAsync(new RunTrackedUlwInput(host, "ses_1", "hello", loopState));

        Assert.False(result.Dispatched);
        Assert.Null(loopState.GetState());
    }

    [Fact]
    public async Task RunTrackedUlwAsync_DoesNotStartStateWhenOnlyNonUlwReceiptIsAccepted()
    {
        var loopState = new UlwLoopStateController(new MemoryUlwLoopStateStore());
        var host = CreateTestHost(dispatchPromptAsync: request =>
            Task.FromResult(new UlwPromptReceipt(request.Message == "HYPERPLAN MODE ENABLED!", request.SessionId, request.Message)));

        var result = await UlwKernelRuntime.RunTrackedUlwAsync(new RunTrackedUlwInput(host, "ses_1", "ulw and hyperplan", loopState));

        Assert.True(result.Dispatched);
        Assert.Equal(["ultrawork", "hyperplan"], result.Intents);
        Assert.Null(loopState.GetState());
    }

    [Fact]
    public async Task HandleUlwLoopIdleAsync_ContinuesActiveLoopWhenCompletionAbsent()
    {
        var loopState = new UlwLoopStateController(new MemoryUlwLoopStateStore());
        loopState.Start(new StartUlwLoopOptions("ses_1", "build", Ultrawork: true));
        var requests = new List<UlwPromptRequest>();
        var host = CreateTestHost(requests: requests);

        await UlwKernelRuntime.HandleUlwLoopIdleAsync(new UlwLoopEngineOptions(host, loopState), "ses_1");

        Assert.Equal(2, loopState.GetState()?.Iteration);
        Assert.Contains("RALPH LOOP 2/500", requests[0].Message);
    }

    [Fact]
    public async Task HandleUlwLoopIdleAsync_MovesCompletionIntoVerificationPending()
    {
        var loopState = new UlwLoopStateController(new MemoryUlwLoopStateStore());
        loopState.Start(new StartUlwLoopOptions("ses_1", "build", Ultrawork: true));
        var requests = new List<UlwPromptRequest>();
        var host = CreateTestHost(
            requests: requests,
            messages: [new UlwMessage("assistant", "<promise>DONE</promise>")]);

        await UlwKernelRuntime.HandleUlwLoopIdleAsync(new UlwLoopEngineOptions(host, loopState), "ses_1");

        Assert.True(loopState.GetState()?.VerificationPending);
        Assert.Equal(UlwLoopConstants.UltraworkVerificationPromise, loopState.GetState()?.CompletionPromise);
        Assert.Contains("ULTRAWORK LOOP VERIFICATION", requests[0].Message);
    }

    [Fact]
    public async Task HandleUlwLoopIdleAsync_IgnoresUserCompletionPromises()
    {
        var loopState = new UlwLoopStateController(new MemoryUlwLoopStateStore());
        loopState.Start(new StartUlwLoopOptions("ses_1", "build", Ultrawork: true));
        var requests = new List<UlwPromptRequest>();
        var host = CreateTestHost(
            requests: requests,
            messages: [new UlwMessage("user", "Continue. Output <promise>DONE</promise> when done.")]);

        await UlwKernelRuntime.HandleUlwLoopIdleAsync(new UlwLoopEngineOptions(host, loopState), "ses_1");

        Assert.Null(loopState.GetState()?.VerificationPending);
        Assert.Equal(2, loopState.GetState()?.Iteration);
        Assert.Contains("RALPH LOOP 2/500", requests[0].Message);
    }

    [Fact]
    public async Task HandleUlwLoopIdleAsync_ClearsLoopWhenVerificationSucceeds()
    {
        var loopState = new UlwLoopStateController(new MemoryUlwLoopStateStore());
        loopState.Start(new StartUlwLoopOptions("ses_1", "build", Ultrawork: true));
        loopState.MarkVerificationPending("ses_1");
        var host = CreateTestHost(messages: [new UlwMessage("assistant", "<promise>VERIFIED</promise>")]);

        await UlwKernelRuntime.HandleUlwLoopIdleAsync(new UlwLoopEngineOptions(host, loopState), "ses_1");

        Assert.Null(loopState.GetState());
    }

    [Fact]
    public async Task HandleUlwLoopIdleAsync_ContinuesAfterFailedVerification()
    {
        var loopState = new UlwLoopStateController(new MemoryUlwLoopStateStore());
        loopState.Start(new StartUlwLoopOptions("ses_1", "build", Ultrawork: true));
        loopState.MarkVerificationPending("ses_1");
        var requests = new List<UlwPromptRequest>();
        var host = CreateTestHost(requests: requests, messages: [new UlwMessage("assistant", "not verified")]);

        await UlwKernelRuntime.HandleUlwLoopIdleAsync(new UlwLoopEngineOptions(host, loopState), "ses_1");

        Assert.Null(loopState.GetState()?.VerificationPending);
        Assert.Equal(2, loopState.GetState()?.Iteration);
        Assert.Contains("ULTRAWORK LOOP VERIFICATION FAILED", requests[0].Message);
    }

    [Fact]
    public async Task HandleUlwLoopIdleAsync_ClearsLoopAtMaxIteration()
    {
        var loopState = new UlwLoopStateController(new MemoryUlwLoopStateStore(new UlwLoopStateRecord(
            true,
            1,
            1,
            "DONE",
            "DONE",
            "2026-01-01T00:00:00.000Z",
            "build",
            "ses_1",
            Strategy: UlwLoopStrategy.Continue,
            Ultrawork: true)));
        var host = CreateTestHost(messages: [new UlwMessage("assistant", "still working")]);

        await UlwKernelRuntime.HandleUlwLoopIdleAsync(new UlwLoopEngineOptions(host, loopState), "ses_1");

        Assert.Null(loopState.GetState());
    }

    [Fact]
    public async Task CreateUlwLoopEngine_SubscribesToIdleEvents()
    {
        var loopState = new UlwLoopStateController(new MemoryUlwLoopStateStore());
        loopState.Start(new StartUlwLoopOptions("ses_1", "build", Ultrawork: true));
        Action<UlwSessionEvent>? listener = null;
        var requests = new List<UlwPromptRequest>();
        var host = CreateTestHost(
            requests: requests,
            onEvent: nextListener =>
            {
                listener = nextListener;
                return () => listener = null;
            });

        var engine = UlwKernelRuntime.CreateUlwLoopEngine(new UlwLoopEngineOptions(host, loopState));
        listener?.Invoke(new UlwSessionEvent(UlwSessionEventType.Idle, "ses_1"));
        await WaitUntilAsync(() => requests.Count > 0);
        engine.Stop();

        Assert.Contains("RALPH LOOP 2/500", requests[0].Message);
        Assert.Null(listener);
    }

    private static async Task WaitUntilAsync(Func<bool> predicate)
    {
        for (var index = 0; index < 20; index++)
        {
            if (predicate())
                return;

            await Task.Delay(10);
        }

        Assert.True(predicate());
    }

    private static TestUlwHost CreateTestHost(
        List<UlwPromptRequest>? requests = null,
        IReadOnlyList<UlwMessage>? messages = null,
        Func<UlwPromptRequest, Task<UlwPromptReceipt>>? dispatchPromptAsync = null,
        Func<Action<UlwSessionEvent>, Action>? onEvent = null)
        => new()
        {
            DispatchPromptAsyncHandler = dispatchPromptAsync ?? (request =>
            {
                requests?.Add(request);
                return Task.FromResult(new UlwPromptReceipt(true, request.SessionId, $"dispatch-{requests?.Count ?? 1}"));
            }),
            ReadMessagesAsyncHandler = _ => Task.FromResult(messages ?? Array.Empty<UlwMessage>()),
            OnEventHandler = onEvent ?? (_ => () => { }),
        };

    private sealed class TestUlwHost : IUlwHost
    {
        public Func<UlwPromptRequest, Task<UlwPromptReceipt>> DispatchPromptAsyncHandler { get; init; } =
            request => Task.FromResult(new UlwPromptReceipt(true, request.SessionId, "dispatch-1"));

        public Func<string, Task<IReadOnlyList<UlwMessage>>> ReadMessagesAsyncHandler { get; init; } =
            _ => Task.FromResult<IReadOnlyList<UlwMessage>>(Array.Empty<UlwMessage>());

        public Func<string, Task<IReadOnlyList<UlwTodo>>> ReadTodosAsyncHandler { get; init; } =
            _ => Task.FromResult<IReadOnlyList<UlwTodo>>(Array.Empty<UlwTodo>());

        public Func<string, Task<string>> ReadStatusAsyncHandler { get; init; } =
            _ => Task.FromResult("idle");

        public Func<string, Task> AbortAsyncHandler { get; init; } = _ => Task.CompletedTask;

        public Func<Action<UlwSessionEvent>, Action> OnEventHandler { get; init; } = _ => () => { };

        public Task<UlwPromptReceipt> DispatchPromptAsync(UlwPromptRequest request) => DispatchPromptAsyncHandler(request);
        public Task<IReadOnlyList<UlwMessage>> ReadMessagesAsync(string sessionId) => ReadMessagesAsyncHandler(sessionId);
        public Task<IReadOnlyList<UlwTodo>> ReadTodosAsync(string sessionId) => ReadTodosAsyncHandler(sessionId);
        public Task<string> ReadStatusAsync(string sessionId) => ReadStatusAsyncHandler(sessionId);
        public Task AbortAsync(string sessionId) => AbortAsyncHandler(sessionId);
        public Action OnEvent(Action<UlwSessionEvent> listener) => OnEventHandler(listener);
    }
}
