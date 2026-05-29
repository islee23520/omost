using System.Text.Json;

using Lfe.Protocol.JsonRpc;
using Lfe.Protocol.Notifications;
using Lfe.Protocol.Types;
using Lfe.Sidecar.Execution;

using UlwHost = Lfe.UlwHostContract.IUlwHost;
using UlwHostMessage = Lfe.UlwHostContract.UlwMessage;
using UlwHostPromptReceipt = Lfe.UlwHostContract.UlwPromptReceipt;
using UlwHostPromptRequest = Lfe.UlwHostContract.UlwPromptRequest;
using UlwHostSessionEvent = Lfe.UlwHostContract.UlwSessionEvent;
using UlwHostTodo = Lfe.UlwHostContract.UlwTodo;

namespace Lfe.Protocol.Tests;

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
        Assert.Equal(LfeNotificationNames.RunResult, result.Method);
        Assert.Equal(LfeRunStatusValues.Completed, result.Params.GetProperty("status").GetString());
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
        Assert.Equal(LfeNotificationNames.RunError, error.Method);
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
        Assert.Contains(terminal.Method, new[] { LfeNotificationNames.RunResult, LfeNotificationNames.RunError });
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
        Assert.Equal(1, terminals.Count(static notification => notification.Method == LfeNotificationNames.RunResult));
        Assert.DoesNotContain(terminals, static notification => notification.Method == LfeNotificationNames.RunError);
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
                        (notification.Method == LfeNotificationNames.RunResult ||
                         notification.Method == LfeNotificationNames.RunError) &&
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
