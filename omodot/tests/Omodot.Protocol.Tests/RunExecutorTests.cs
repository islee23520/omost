using System.Text.Json;

using Omodot.Protocol.Execution;
using Omodot.Protocol.JsonRpc;
using Omodot.Protocol.Methods;
using Omodot.Protocol.Notifications;
using Omodot.Protocol.Types;
using Omodot.Sidecar.Execution;

using UlwHost = Omodot.UlwHostContract.IUlwHost;
using UlwHostPromptRequest = Omodot.UlwHostContract.UlwPromptRequest;
using UlwHostPromptReceipt = Omodot.UlwHostContract.UlwPromptReceipt;
using UlwHostSessionEvent = Omodot.UlwHostContract.UlwSessionEvent;
using UlwHostMessage = Omodot.UlwHostContract.UlwMessage;
using UlwHostTodo = Omodot.UlwHostContract.UlwTodo;

namespace Omodot.Protocol.Tests;

public sealed class RunExecutorTests
{
    [Fact]
    public async Task ProtocolConformanceExecutor_Dispatch_ProducesCurrentPhase1StubOutput()
    {
        var serverState = new OmodotServerState();
        serverState.StartSession("session-phase1");
        var notifications = new CapturingNotificationEmitter();
        var executor = CreateConformanceExecutor(serverState, notifications);

        var accepted = await executor.DispatchAsync(new RunDispatchRequestParams
        {
            RunId = "run-phase1",
            SessionId = "session-phase1",
            Prompt = "Hello from Phase 1.",
            Agent = "omots-agent",
            Model = "gpt-5.4",
            ContinuationToken = "continue-123",
        }, CancellationToken.None);

        Assert.True(accepted.Accepted);
        Assert.Equal("run-phase1", accepted.RunId);

        var terminal = await notifications.WaitForAsync(OmoNotificationNames.RunResult, "run-phase1");
        Assert.Equal(OmoRunStatusValues.Completed, terminal.Params.GetProperty("status").GetString());
        Assert.Equal("session-phase1", terminal.Params.GetProperty("finalSessionId").GetString());
        Assert.Equal("Completed Phase 1 run: Hello from Phase 1.", terminal.Params.GetProperty("outputText").GetString());

        var outputJson = terminal.Params.GetProperty("outputJson");
        Assert.Equal("omots-agent", outputJson.GetProperty("agent").GetString());
        Assert.Equal("gpt-5.4", outputJson.GetProperty("model").GetString());
        Assert.Equal("continue-123", outputJson.GetProperty("continuationToken").GetString());

        Assert.Equal(new[]
        {
            OmoRunPhaseValues.Queued,
            OmoRunPhaseValues.Running,
            OmoRunPhaseValues.Completed,
        }, notifications.Notifications
            .Where(static notification => notification.Method == OmoNotificationNames.RunProgress)
            .Select(static notification => notification.Params.GetProperty("phase").GetString())
            .ToArray());
    }

    [Fact]
    public async Task AgentOsRuntimeExecutor_Dispatch_MapsProtocolDtoToUlwHostDto()
    {
        var host = new CapturingUlwHost("runtime-session", "dispatch-1", accepted: true);
        var executor = new AgentOsRuntimeExecutor(host);

        var accepted = await executor.DispatchAsync(new RunDispatchRequestParams
        {
            RunId = "run-protocol",
            SessionId = "session-protocol",
            Prompt = "Runtime prompt",
            Agent = "runtime-agent",
            Model = "runtime-model",
            ContinuationToken = "runtime-continuation",
        }, CancellationToken.None);

        Assert.True(accepted.Accepted);
        Assert.Equal("run-protocol", accepted.RunId);
        Assert.NotNull(host.LastPromptRequest);
        Assert.Equal("session-protocol", host.LastPromptRequest.SessionId);
        Assert.Equal("Runtime prompt", host.LastPromptRequest.Message);
        Assert.Equal("runtime-agent", host.LastPromptRequest.AgentName);
        Assert.Equal("runtime-model", host.LastPromptRequest.ModelId);
        Assert.Equal("runtime-continuation", host.LastPromptRequest.ContinuationToken);
    }

    [Fact]
    public async Task RunHandlers_InvokeInjectedExecutor()
    {
        var executor = new CapturingRunExecutor();
        var dispatchHandler = new RunDispatchHandler(executor);
        var cancelHandler = new RunCancelHandler(executor);

        var dispatch = await dispatchHandler.HandleAsync(ToElement(new RunDispatchRequestParams
        {
            RunId = "run-handler",
            SessionId = "session-handler",
            Prompt = "Handler prompt",
        }), CancellationToken.None);

        var cancel = await cancelHandler.HandleAsync(ToElement(new RunCancelRequestParams
        {
            RunId = "run-handler",
            Reason = "stop",
        }), CancellationToken.None);

        var dispatchResult = Assert.IsType<RunDispatchResult>(dispatch);
        var cancelResult = Assert.IsType<RunCancelResult>(cancel);
        Assert.Equal("run-handler", dispatchResult.RunId);
        Assert.True(dispatchResult.Accepted);
        Assert.Equal("run-handler", cancelResult.RunId);
        Assert.Equal(OmoRunStatusValues.Cancelled, cancelResult.Status);
        Assert.Equal("run-handler", executor.LastDispatchRequest?.RunId);
        Assert.Equal("session-handler", executor.LastDispatchRequest?.SessionId);
        Assert.Equal("Handler prompt", executor.LastDispatchRequest?.Prompt);
        Assert.Equal("run-handler", executor.LastCancelRunId);
        Assert.Equal("stop", executor.LastCancelReason);
    }

    [Fact]
    public async Task AgentOsRuntimeExecutor_DoesNotPassRunIdAsSessionId()
    {
        var host = new CapturingUlwHost("session-protocol", "dispatch-1", accepted: true);
        var executor = new AgentOsRuntimeExecutor(host);

        await executor.DispatchAsync(new RunDispatchRequestParams
        {
            RunId = "run-must-not-be-session",
            SessionId = "session-protocol",
            Prompt = "Runtime prompt",
        }, CancellationToken.None);

        Assert.NotNull(host.LastPromptRequest);
        Assert.Equal("session-protocol", host.LastPromptRequest.SessionId);
        Assert.NotEqual("run-must-not-be-session", host.LastPromptRequest.SessionId);
    }

    private static ProtocolConformanceExecutor CreateConformanceExecutor(
        OmodotServerState serverState,
        CapturingNotificationEmitter notifications)
    {
        return new ProtocolConformanceExecutor(
            serverState,
            new ProgressEmitter(notifications),
            new ResultEmitter(notifications),
            new ErrorEmitter(notifications));
    }

    private static JsonElement ToElement<T>(T value)
    {
        return JsonSerializer.SerializeToElement(value, JsonRpcProtocol.SerializerOptions);
    }

    private sealed class CapturingRunExecutor : IOmoRunExecutor
    {
        public string? LastCancelReason { get; private set; }

        public string? LastCancelRunId { get; private set; }

        public RunDispatchRequestParams? LastDispatchRequest { get; private set; }

        public Task<OmoRunAccepted> DispatchAsync(RunDispatchRequestParams request, CancellationToken ct)
        {
            LastDispatchRequest = request;
            return Task.FromResult(new OmoRunAccepted
            {
                Accepted = true,
                RunId = request.RunId,
            });
        }

        public Task<OmoCancelResult> CancelAsync(string runId, string? reason, CancellationToken ct)
        {
            LastCancelRunId = runId;
            LastCancelReason = reason;
            return Task.FromResult(new OmoCancelResult
            {
                RunId = runId,
                Status = OmoRunStatusValues.Cancelled,
            });
        }
    }

    private sealed class CapturingNotificationEmitter : INotificationEmitter
    {
        private readonly object _gate = new();
        private readonly List<CapturedNotification> _notifications = new();

        public IReadOnlyList<CapturedNotification> Notifications
        {
            get
            {
                lock (_gate)
                {
                    return _notifications.ToArray();
                }
            }
        }

        public Task EmitAsync<TParams>(string methodName, TParams parameters, CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                _notifications.Add(new CapturedNotification(
                    methodName,
                    JsonSerializer.SerializeToElement(parameters, JsonRpcProtocol.SerializerOptions)));
            }

            return Task.CompletedTask;
        }

        public async Task<CapturedNotification> WaitForAsync(string methodName, string runId)
        {
            var start = DateTime.UtcNow;
            while (true)
            {
                CapturedNotification? match;
                lock (_gate)
                {
                    match = _notifications.LastOrDefault(notification =>
                        notification.Method == methodName &&
                        notification.Params.TryGetProperty("runId", out var runIdElement) &&
                        runIdElement.GetString() == runId);
                }

                if (match is not null)
                {
                    return match;
                }

                if (DateTime.UtcNow - start > TimeSpan.FromSeconds(2))
                {
                    throw new TimeoutException($"Timed out waiting for {methodName} for {runId}.");
                }

                await Task.Delay(10);
            }
        }
    }

    private sealed record CapturedNotification(string Method, JsonElement Params);

    private sealed class CapturingUlwHost : UlwHost
    {
        private readonly bool _accepted;
        private readonly string _dispatchId;
        private readonly string _sessionId;

        public CapturingUlwHost(string sessionId, string dispatchId, bool accepted)
        {
            _sessionId = sessionId;
            _dispatchId = dispatchId;
            _accepted = accepted;
        }

        public string? LastAbortSessionId { get; private set; }

        public UlwHostPromptRequest? LastPromptRequest { get; private set; }

        public Task AbortAsync(string sessionId)
        {
            LastAbortSessionId = sessionId;
            return Task.CompletedTask;
        }

        public Task<UlwHostPromptReceipt> DispatchPromptAsync(UlwHostPromptRequest request)
        {
            LastPromptRequest = request;
            return Task.FromResult(new UlwHostPromptReceipt(_accepted, _sessionId, _dispatchId));
        }

        public Action OnEvent(Action<UlwHostSessionEvent> listener) => static () => { };

        public Task<IReadOnlyList<UlwHostMessage>> ReadMessagesAsync(string sessionId)
            => Task.FromResult<IReadOnlyList<UlwHostMessage>>(Array.Empty<UlwHostMessage>());

        public Task<string> ReadStatusAsync(string sessionId) => Task.FromResult("idle");

        public Task<IReadOnlyList<UlwHostTodo>> ReadTodosAsync(string sessionId)
            => Task.FromResult<IReadOnlyList<UlwHostTodo>>(Array.Empty<UlwHostTodo>());
    }
}
