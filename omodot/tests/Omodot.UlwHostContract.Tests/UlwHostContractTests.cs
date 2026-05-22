using System.Text.Json;

using Omodot.UlwHostContract;

using Xunit;

namespace Omodot.UlwHostContract.Tests;

public class UlwHostContractTests
{
    [Fact]
    public async Task HostSurface_SupportsPromptDispatchAndEventSubscription()
    {
        var events = new List<UlwSessionEvent>();
        IUlwHost host = new TestUlwHost
        {
            DispatchPromptAsyncHandler = request => Task.FromResult(new UlwPromptReceipt(request.Message.Length > 0, request.SessionId, "dispatch-1")),
            ReadMessagesAsyncHandler = _ => Task.FromResult<IReadOnlyList<UlwMessage>>([new UlwMessage("assistant", "ok")]),
            ReadTodosAsyncHandler = _ => Task.FromResult<IReadOnlyList<UlwTodo>>([new UlwTodo("finish", "completed")]),
            ReadStatusAsyncHandler = _ => Task.FromResult("idle"),
            AbortAsyncHandler = _ => Task.CompletedTask,
            OnEventHandler = listener =>
            {
                listener(new UlwSessionEvent(UlwSessionEventType.Idle, "ses_1"));
                return () => events.Add(new UlwSessionEvent(UlwSessionEventType.Deleted, "ses_1"));
            },
        };

        var dispose = host.OnEvent(events.Add);
        var receipt = await host.DispatchPromptAsync(new UlwPromptRequest("ses_1", "ulw"));
        dispose();

        Assert.True(receipt.Accepted);
        Assert.Equal(
            [new UlwSessionEvent(UlwSessionEventType.Idle, "ses_1"), new UlwSessionEvent(UlwSessionEventType.Deleted, "ses_1")],
            events);
    }

    [Fact]
    public async Task HostSurface_SupportsMultiTurnContinuationFields()
    {
        IUlwHost host = new TestUlwHost
        {
            DispatchPromptAsyncHandler = request =>
            {
                var isContinuation = !string.IsNullOrEmpty(request.PreviousResponseId);
                return Task.FromResult(new UlwPromptReceipt(true, request.SessionId, "dispatch-mt", isContinuation ? "resp-2" : "resp-1", AgenticStatePreserved: true));
            },
        };

        var first = await host.DispatchPromptAsync(new UlwPromptRequest("s1", "start ulw"));
        var second = await host.DispatchPromptAsync(new UlwPromptRequest("s1", "continue", PreviousResponseId: first.ResponseId));

        Assert.Equal("resp-1", first.ResponseId);
        Assert.Equal("resp-2", second.ResponseId);
        Assert.True(second.AgenticStatePreserved);
    }

    [Fact]
    public void GetAcceptedCapabilities_FiltersUnsupportedAndDuplicateEntries()
    {
        var accepted = OmoProtocol.GetAcceptedCapabilities([
            "phase1.initialize",
            "phase1.run-progress",
            "phase1.initialize",
            "ignored",
        ]);

        Assert.Equal(["phase1.initialize", "phase1.run-progress"], accepted);
    }

    [Fact]
    public void ParseContentLength_ReturnsParsedLength()
    {
        var length = OmoProtocol.ParseContentLength("Content-Type: application/json\r\nContent-Length: 42\r\n");
        Assert.Equal(42, length);
    }

    [Fact]
    public void CreateContentLengthFrame_UsesUtf8BodyLength()
    {
        var frame = OmoProtocol.CreateContentLengthFrame(new { message = "한글" });
        var parts = frame.Split(OmoProtocolConstants.HeaderSeparator);

        Assert.Equal(2, parts.Length);
        Assert.Contains("Content-Length: ", parts[0]);
        Assert.Equal(JsonSerializer.Serialize(new { message = "한글" }), parts[1]);
        Assert.Equal(EncodingUtf8Length(parts[1]), OmoProtocol.ParseContentLength(parts[0] + "\r\n"));
    }

    [Fact]
    public void ValidationHelpers_ReturnExpectedErrors()
    {
        Assert.Equal(
            "clientKind must be host-bridge or implementation-toolkit",
            OmoProtocol.GetInitializeValidationError(new Dictionary<string, object?>
            {
                ["protocolVersion"] = "1.0.0",
                ["hostName"] = "host",
                ["hostVersion"] = "1.2.3",
                ["clientKind"] = "bad",
                ["requestedCapabilities"] = new[] { "phase1.initialize" },
            }));

        Assert.Null(OmoProtocol.GetRunDispatchValidationError(new Dictionary<string, object?>
        {
            ["runId"] = "run-1",
            ["sessionId"] = "ses-1",
            ["prompt"] = "ship it",
        }));

        Assert.Equal("reason must be a non-empty string", OmoProtocol.GetRunCancelValidationError(JsonDocument.Parse("""
            {"runId":"run-1","reason":"   "}
            """).RootElement));
    }

    [Fact]
    public void TerminalChecks_RecognizeExpectedStates()
    {
        Assert.True(OmoProtocol.IsTerminalRunLifecycleState(OmotsRunLifecycleState.Completed));
        Assert.False(OmoProtocol.IsTerminalRunLifecycleState(OmotsRunLifecycleState.Running));
        Assert.True(OmoProtocol.IsTerminalRunStatus(OmotsRunStatus.Cancelled));
        Assert.True(OmoProtocol.IsSupportedProtocolVersion("1.0.0"));
    }

    private static int EncodingUtf8Length(string value) => System.Text.Encoding.UTF8.GetByteCount(value);

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
