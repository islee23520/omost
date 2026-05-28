using System.Text.Json;

using Omodot.Protocol.JsonRpc;
using Omodot.Protocol.Types;
using Omodot.Sidecar.Execution;

using UlwHost = Omodot.UlwHostContract.IUlwHost;
using UlwHostMessage = Omodot.UlwHostContract.UlwMessage;
using UlwHostPromptReceipt = Omodot.UlwHostContract.UlwPromptReceipt;
using UlwHostPromptRequest = Omodot.UlwHostContract.UlwPromptRequest;
using UlwHostSessionEvent = Omodot.UlwHostContract.UlwSessionEvent;
using UlwHostTodo = Omodot.UlwHostContract.UlwTodo;

namespace Omodot.Protocol.Tests;

public sealed class DtoMappingTests
{
    [Fact]
    public async Task MapsRunDispatchToUlwPromptRequestWithCorrectFieldOrder()
    {
        var host = new CapturingUlwHost("runtime-session", "dispatch-1");
        var executor = new AgentOsRuntimeExecutor(host);

        await executor.DispatchAsync(new RunDispatchRequestParams
        {
            RunId = "run-mapping",
            SessionId = "protocol-session",
            Prompt = "Map this prompt",
            Agent = "agent-one",
            Model = "model-one",
            ContinuationToken = "continuation-one",
        }, CancellationToken.None);

        Assert.NotNull(host.LastPromptRequest);
        var request = host.LastPromptRequest!;
        Assert.Equal("protocol-session", request.SessionId);
        Assert.Equal("Map this prompt", request.Message);
        Assert.Equal("agent-one", request.AgentName);
        Assert.Equal("model-one", request.ModelId);
        Assert.Equal("continuation-one", request.ContinuationToken);

        var serialized = JsonSerializer.SerializeToElement(request, JsonRpcProtocol.SerializerOptions);
        Assert.Equal(new[]
        {
            "sessionId",
            "message",
            "agentName",
            "modelId",
            "continuationToken",
        }, serialized.EnumerateObject().Select(static property => property.Name).ToArray());
    }

    [Fact]
    public async Task MapsAgentModelAndContinuationToken()
    {
        var host = new CapturingUlwHost("runtime-session", "dispatch-2");
        var executor = new AgentOsRuntimeExecutor(host);

        await executor.DispatchAsync(new RunDispatchRequestParams
        {
            RunId = "run-agent-model",
            SessionId = "session-agent-model",
            Prompt = "Use requested runtime details",
            Agent = "sisyphus",
            Model = "gpt-5.5",
            ContinuationToken = "prev-response-token",
        }, CancellationToken.None);

        Assert.NotNull(host.LastPromptRequest);
        var request = host.LastPromptRequest!;
        Assert.Equal("sisyphus", request.AgentName);
        Assert.Equal("gpt-5.5", request.ModelId);
        Assert.Equal("prev-response-token", request.ContinuationToken);
    }

    [Fact]
    public async Task DoesNotUseRunIdAsSessionId()
    {
        var host = new CapturingUlwHost("runtime-session", "dispatch-3");
        var executor = new AgentOsRuntimeExecutor(host);

        await executor.DispatchAsync(new RunDispatchRequestParams
        {
            RunId = "run-is-not-session",
            SessionId = "session-is-session",
            Prompt = "Keep ownership boundaries intact",
        }, CancellationToken.None);

        Assert.NotNull(host.LastPromptRequest);
        var request = host.LastPromptRequest!;
        Assert.Equal("session-is-session", request.SessionId);
        Assert.NotEqual("run-is-not-session", request.SessionId);
    }

    [Fact]
    public async Task RejectsOrReportsMappingFailureForInvalidProtocolParams()
    {
        var host = new CapturingUlwHost("runtime-session", "dispatch-4");
        var executor = new AgentOsRuntimeExecutor(host);

        var exception = await Assert.ThrowsAsync<OmoProtocolException>(() => executor.DispatchAsync(new RunDispatchRequestParams
        {
            RunId = "run-invalid",
            SessionId = string.Empty,
            Prompt = "Cannot map without a protocol session",
        }, CancellationToken.None));

        Assert.Equal(ErrorCode.InvalidParams, exception.Code);
        Assert.Null(host.LastPromptRequest);
    }

    private sealed class CapturingUlwHost : UlwHost
    {
        private readonly string _dispatchId;
        private readonly string _sessionId;

        public CapturingUlwHost(string sessionId, string dispatchId)
        {
            _sessionId = sessionId;
            _dispatchId = dispatchId;
        }

        public UlwHostPromptRequest? LastPromptRequest { get; private set; }

        public Task AbortAsync(string sessionId) => Task.CompletedTask;

        public Task<UlwHostPromptReceipt> DispatchPromptAsync(UlwHostPromptRequest request)
        {
            LastPromptRequest = request;
            return Task.FromResult(new UlwHostPromptReceipt(true, _sessionId, _dispatchId));
        }

        public Action OnEvent(Action<UlwHostSessionEvent> listener) => static () => { };

        public Task<IReadOnlyList<UlwHostMessage>> ReadMessagesAsync(string sessionId)
            => Task.FromResult<IReadOnlyList<UlwHostMessage>>(Array.Empty<UlwHostMessage>());

        public Task<string> ReadStatusAsync(string sessionId) => Task.FromResult("idle");

        public Task<IReadOnlyList<UlwHostTodo>> ReadTodosAsync(string sessionId)
            => Task.FromResult<IReadOnlyList<UlwHostTodo>>(Array.Empty<UlwHostTodo>());
    }
}
