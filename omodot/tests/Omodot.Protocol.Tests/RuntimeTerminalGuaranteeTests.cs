using System.Text.Json;

using Omodot.Protocol.JsonRpc;
using Omodot.Protocol.Notifications;
using Omodot.Protocol.Types;
using Omodot.Sidecar.Execution;

using UlwHost = Omodot.UlwHostContract.IUlwHost;
using UlwHostMessage = Omodot.UlwHostContract.UlwMessage;
using UlwHostPromptReceipt = Omodot.UlwHostContract.UlwPromptReceipt;
using UlwHostPromptRequest = Omodot.UlwHostContract.UlwPromptRequest;
using UlwHostSessionEvent = Omodot.UlwHostContract.UlwSessionEvent;
using UlwHostTodo = Omodot.UlwHostContract.UlwTodo;

namespace Omodot.Protocol.Tests;

public sealed class RuntimeTerminalGuaranteeTests
{
    [Fact]
    public async Task RuntimeExecutorSuccessEmitsExactlyOneRunResult()
    {
        var notifications = new CapturingNotificationEmitter();
        var executor = CreateExecutor(new ConfigurableUlwHost(static request =>
            Task.FromResult(new UlwHostPromptReceipt(true, request.SessionId, "dispatch-success"))), notifications);

        await executor.DispatchAsync(CreateRequest("run-success"), CancellationToken.None);

        var terminals = notifications.TerminalNotificationsFor("run-success");
        var result = Assert.Single(terminals);
        Assert.Equal(OmoNotificationNames.RunResult, result.Method);
        Assert.Equal(OmoRunStatusValues.Completed, result.Params.GetProperty("status").GetString());
    }

    [Fact]
    public async Task RuntimeExecutorFailureEmitsExactlyOneRunError()
    {
        var notifications = new CapturingNotificationEmitter();
        var executor = CreateExecutor(new ConfigurableUlwHost(static _ =>
            throw new InvalidOperationException("adapter crashed before terminal")), notifications);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            executor.DispatchAsync(CreateRequest("run-failure"), CancellationToken.None));

        var terminals = notifications.TerminalNotificationsFor("run-failure");
        var error = Assert.Single(terminals);
        Assert.Equal(OmoNotificationNames.RunError, error.Method);
        Assert.Equal(ErrorCode.RunFailure, error.Params.GetProperty("code").GetInt32());
    }

    [Fact]
    public async Task RuntimeExecutorCancellationEmitsExactlyOneTerminalNotification()
    {
        var notifications = new CapturingNotificationEmitter();
        var executor = CreateExecutor(new ConfigurableUlwHost(static _ =>
            throw new OperationCanceledException("adapter observed cancellation")), notifications);

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            executor.DispatchAsync(CreateRequest("run-cancelled"), CancellationToken.None));

        var terminal = Assert.Single(notifications.TerminalNotificationsFor("run-cancelled"));
        Assert.Contains(terminal.Method, new[] { OmoNotificationNames.RunResult, OmoNotificationNames.RunError });
    }

    [Fact]
    public async Task RuntimeExecutorDoesNotOrphanAcceptedRun()
    {
        var notifications = new CapturingNotificationEmitter();
        var executor = CreateExecutor(new ConfigurableUlwHost(static request =>
            Task.FromResult(new UlwHostPromptReceipt(true, request.SessionId, "dispatch-accepted"))), notifications);

        var accepted = await executor.DispatchAsync(CreateRequest("run-accepted"), CancellationToken.None);

        Assert.True(accepted.Accepted);
        Assert.Single(notifications.TerminalNotificationsFor("run-accepted"));
    }

    [Fact]
    public async Task RuntimeExecutorDoesNotEmitResultAndErrorForSameRun()
    {
        var notifications = new CapturingNotificationEmitter();
        var host = new ConfigurableUlwHost(static request =>
            Task.FromResult(new UlwHostPromptReceipt(true, request.SessionId, "dispatch-terminal-once")));
        var executor = CreateExecutor(host, notifications);

        await executor.DispatchAsync(CreateRequest("run-single-terminal"), CancellationToken.None);
        await executor.CancelAsync("run-single-terminal", "late cancellation", CancellationToken.None);

        var terminals = notifications.TerminalNotificationsFor("run-single-terminal");
        Assert.Single(terminals);
        Assert.Equal(1, terminals.Count(static notification => notification.Method == OmoNotificationNames.RunResult));
        Assert.DoesNotContain(terminals, static notification => notification.Method == OmoNotificationNames.RunError);
    }

    private static AgentOsRuntimeExecutor CreateExecutor(UlwHost host, CapturingNotificationEmitter notifications)
    {
        return new AgentOsRuntimeExecutor(
            host,
            new ResultEmitter(notifications),
            new ErrorEmitter(notifications));
    }

    private static RunDispatchRequestParams CreateRequest(string runId)
    {
        return new RunDispatchRequestParams
        {
            RunId = runId,
            SessionId = $"session-{runId}",
            Prompt = $"Prompt for {runId}",
        };
    }

    private sealed class CapturingNotificationEmitter : INotificationEmitter
    {
        private readonly object _gate = new();
        private readonly List<CapturedNotification> _notifications = new();

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

        public IReadOnlyList<CapturedNotification> TerminalNotificationsFor(string runId)
        {
            lock (_gate)
            {
                return _notifications
                    .Where(notification =>
                        (notification.Method == OmoNotificationNames.RunResult ||
                         notification.Method == OmoNotificationNames.RunError) &&
                        notification.Params.TryGetProperty("runId", out var runIdElement) &&
                        runIdElement.GetString() == runId)
                    .ToArray();
            }
        }
    }

    private sealed record CapturedNotification(string Method, JsonElement Params);

    private sealed class ConfigurableUlwHost : UlwHost
    {
        private readonly Func<UlwHostPromptRequest, Task<UlwHostPromptReceipt>> _dispatch;

        public ConfigurableUlwHost(Func<UlwHostPromptRequest, Task<UlwHostPromptReceipt>> dispatch)
        {
            _dispatch = dispatch;
        }

        public string? LastAbortSessionId { get; private set; }

        public Task AbortAsync(string sessionId)
        {
            LastAbortSessionId = sessionId;
            return Task.CompletedTask;
        }

        public Task<UlwHostPromptReceipt> DispatchPromptAsync(UlwHostPromptRequest request) => _dispatch(request);

        public Action OnEvent(Action<UlwHostSessionEvent> listener) => static () => { };

        public Task<IReadOnlyList<UlwHostMessage>> ReadMessagesAsync(string sessionId)
            => Task.FromResult<IReadOnlyList<UlwHostMessage>>(Array.Empty<UlwHostMessage>());

        public Task<string> ReadStatusAsync(string sessionId) => Task.FromResult("idle");

        public Task<IReadOnlyList<UlwHostTodo>> ReadTodosAsync(string sessionId)
            => Task.FromResult<IReadOnlyList<UlwHostTodo>>(Array.Empty<UlwHostTodo>());
    }
}
