using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using Lfe.Protocol.JsonRpc;
using Lfe.Protocol.Types;
using Lfe.Sidecar;

namespace Lfe.Protocol.Tests;

public sealed class Phase1ConformanceTests
{
    [Fact]
    public async Task Phase1_Initialize_WithValidVersion_Succeeds()
    {
        using var harness = new Phase1Harness();

        var response = await harness.SendRequestAsync(LoadNode("protocol-fixtures/phase1/initialize.request.json"), splitAt: 32);

        var expected = LoadNode("protocol-fixtures/phase1/initialize.response.json").DeepClone();
        expected["result"]!["implementationName"] = LfeProtocolInfo.ImplementationName;

        AssertJsonEqual(expected, response);
    }

    [Fact]
    public async Task Phase1_Initialize_WithInvalidVersion_ReturnsVersionMismatch()
    {
        using var harness = new Phase1Harness();

        var response = await harness.SendRequestAsync(JsonNode.Parse(
            """
            {
              "jsonrpc": "2.0",
              "id": 1,
              "method": "lfe.initialize",
              "params": {
                "protocolVersion": "9.9.9",
                "hostName": "opencode",
                "hostVersion": "0.1.0",
                "clientKind": "host-bridge",
                "requestedCapabilities": ["phase1.initialize"]
              }
            }
            """)!);

        AssertJsonEqual(LoadNode("protocol-fixtures/phase1/error.version-mismatch.response.json"), response);
        AssertJsonEqual(LoadNode(".lfe/evidence/task-11-lfe-version-mismatch.json"), response);
    }

    [Fact]
    public async Task Phase1_SessionStart_AcceptsSession()
    {
        using var harness = new Phase1Harness();

        await harness.SendRequestAsync(LoadNode("protocol-fixtures/phase1/initialize.request.json"));
        var response = await harness.SendRequestAsync(LoadNode("protocol-fixtures/phase1/session.start.request.json"));

        AssertJsonEqual(LoadNode("protocol-fixtures/phase1/session.start.response.json"), response);
    }

    [Fact]
    public async Task Phase1_RunDispatch_ProducesProgressAndResult_AndMatchesGoldenShape()
    {
        using var harness = new Phase1Harness();

        await harness.SendRequestAsync(LoadNode("protocol-fixtures/phase1/initialize.request.json"));
        await harness.SendRequestAsync(LoadNode("protocol-fixtures/phase1/session.start.request.json"));
        await harness.SendRequestAsync(LoadNode("protocol-fixtures/phase1/run.dispatch.request.json"));
        await harness.WaitForTerminalAsync("run-phase1");

        var transcript = harness.CreateTranscriptNode();
        AssertJsonEqual(LoadNode(".lfe/evidence/task-11-lfe-phase1-success.json"), transcript);

        AssertJsonEqual(LoadNode("protocol-fixtures/golden/lfe-phase1-success.json"), transcript);

        var progressMessages = harness.ServerMessages
            .Where(message => string.Equals(message["method"]?.GetValue<string>(), LfeNotificationNames.RunProgress, StringComparison.Ordinal))
            .ToArray();

        Assert.Equal(3, progressMessages.Length);
        Assert.Equal(
            new[] { LfeRunPhaseValues.Queued, LfeRunPhaseValues.Running, LfeRunPhaseValues.Completed },
            progressMessages.Select(message => message["params"]!["phase"]!.GetValue<string>()).ToArray());

        Assert.Equal(LfeNotificationNames.RunResult, harness.ServerMessages.Last()["method"]!.GetValue<string>());
        Assert.Equal(LfeRunStatusValues.Completed, harness.ServerMessages.Last()["params"]!["status"]!.GetValue<string>());
    }

    [Fact]
    public async Task Phase1_RunCancel_ProducesTerminalCancelledState()
    {
        using var harness = new Phase1Harness();

        await harness.SendRequestAsync(LoadNode("protocol-fixtures/phase1/initialize.request.json"));
        await harness.SendRequestAsync(LoadNode("protocol-fixtures/phase1/session.start.request.json"));
        await harness.SendRequestAsync(JsonNode.Parse(
            """
            {
              "jsonrpc": "2.0",
              "id": 3,
              "method": "lfe.run.dispatch",
              "params": {
                "runId": "run-cancel",
                "sessionId": "session-phase1",
                "prompt": "Cancel this Phase 1 run.",
            "agent": "lfets-agent",
                "model": "gpt-5.4",
                "continuationToken": "continue-cancel"
              }
            }
            """)!);

        var cancelResponse = await harness.SendRequestAsync(LoadNode("protocol-fixtures/phase1/run.cancel.request.json"));
        await harness.WaitForTerminalAsync("run-cancel");

        AssertJsonEqual(LoadNode("protocol-fixtures/phase1/run.cancel.response.json"), cancelResponse);

        var terminal = harness.ServerMessages.Last(message =>
            string.Equals(message["method"]?.GetValue<string>(), LfeNotificationNames.RunResult, StringComparison.Ordinal));

        Assert.Equal(LfeRunStatusValues.Cancelled, terminal["params"]!["status"]!.GetValue<string>());
        Assert.Equal("Run cancelled: User requested cancellation", terminal["params"]!["outputText"]!.GetValue<string>());
        AssertJsonEqual(LoadNode("protocol-fixtures/golden/phase1-cancel.json")["transcript"]![(int)7]!["message"]!, cancelResponse);
    }

    [Fact]
    public void Phase1_MethodNames_NotificationNames_AndErrorCodes_MatchFrozenContract()
    {
        Assert.Equal(new[]
        {
            "lfe.initialize",
            "lfe.session.start",
            "lfe.run.dispatch",
            "lfe.run.cancel",
        }, LfeMethodNames.All);

        Assert.Equal(new[]
        {
            "lfe.run.progress",
            "lfe.run.result",
            "lfe.run.error",
        }, LfeNotificationNames.All);

        Assert.Equal(-32600, ErrorCode.InvalidRequest);
        Assert.Equal(-32001, ErrorCode.VersionMismatch);
        Assert.Equal(-32010, ErrorCode.RunFailure);
        Assert.Equal("LFE_INVALID_REQUEST", LfeErrorCode.InvalidRequest);
        Assert.Equal("LFE_VERSION_MISMATCH", LfeErrorCode.VersionMismatch);
        Assert.Equal("LFE_RUN_FAILED", LfeErrorCode.RunFailed);
    }

    private static void AssertJsonEqual(JsonNode? expected, JsonNode? actual)
    {
        Assert.True(JsonNode.DeepEquals(expected, actual), $"Expected:\n{expected}\nActual:\n{actual}");
    }

    private static JsonNode LoadNode(string relativePath)
    {
        return JsonNode.Parse(File.ReadAllText(Path.Combine(GetRepositoryRoot(), relativePath)))
            ?? throw new InvalidDataException($"Failed to parse JSON fixture: {relativePath}");
    }

    private static string GetRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "protocol-fixtures")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }

    private sealed class Phase1Harness : IDisposable
    {
        private readonly StringWriter _errorWriter = new();
        private readonly MemoryStream _output = new();
        private readonly JsonRpcServer _server;
        private long _readOffset;

        public Phase1Harness()
        {
            _server = new JsonRpcServer(Stream.Null, _output, _errorWriter);
        }

        public List<JsonNode> ServerMessages { get; } = new();

        public List<TranscriptEntry> Transcript { get; } = new();

        public void Dispose()
        {
            _output.Dispose();
            _errorWriter.Dispose();
        }

        public JsonNode CreateTranscriptNode(Func<JsonNode, bool>? includeMessage = null)
        {
            var transcript = new JsonArray();
            foreach (var entry in Transcript)
            {
                if (includeMessage is not null && !includeMessage(entry.Message))
                {
                    continue;
                }

                transcript.Add(new JsonObject
                {
                    ["direction"] = entry.Direction,
                    ["message"] = entry.Message.DeepClone(),
                });
            }

            return new JsonObject
            {
                ["name"] = "lfe-phase1-success",
                ["transcript"] = transcript,
            };
        }

        public async Task<JsonNode> SendRequestAsync(JsonNode request, int? splitAt = null)
        {
            Transcript.Add(new TranscriptEntry("client->server", request.DeepClone()));

            var responseId = request["id"]?.ToJsonString() ?? "null";
            var frame = CreateFrame(request);

            if (splitAt.HasValue && splitAt.Value > 0 && splitAt.Value < frame.Length)
            {
                await _server.AcceptChunkAsync(frame.AsMemory(0, splitAt.Value));
                await _server.AcceptChunkAsync(frame.AsMemory(splitAt.Value));
            }
            else
            {
                await _server.AcceptChunkAsync(frame);
            }

            return await WaitForResponseAsync(responseId);
        }

        public async Task WaitForTerminalAsync(string runId)
        {
            var start = DateTime.UtcNow;
            while (true)
            {
                DrainOutput();
                if (ServerMessages.Any(message =>
                        string.Equals(message["method"]?.GetValue<string>(), LfeNotificationNames.RunResult, StringComparison.Ordinal) &&
                        string.Equals(message["params"]?["runId"]?.GetValue<string>(), runId, StringComparison.Ordinal)) ||
                    ServerMessages.Any(message =>
                        string.Equals(message["method"]?.GetValue<string>(), LfeNotificationNames.RunError, StringComparison.Ordinal) &&
                        string.Equals(message["params"]?["runId"]?.GetValue<string>(), runId, StringComparison.Ordinal)))
                {
                    return;
                }

                if (DateTime.UtcNow - start > TimeSpan.FromSeconds(2))
                {
                    throw new TimeoutException($"Timed out waiting for terminal notification for run '{runId}'.");
                }

                await Task.Delay(10);
            }
        }

        private static byte[] CreateFrame(JsonNode request)
        {
            using var stream = new MemoryStream();
            ContentLengthFraming.WriteRawBodyAsync(stream, request.ToJsonString(), default).GetAwaiter().GetResult();
            return stream.ToArray();
        }

        private void DrainOutput()
        {
            var bytes = _output.ToArray();
            while (_readOffset < bytes.Length)
            {
                var headerEnd = IndexOfHeaderSeparator(bytes, (int)_readOffset);
                if (headerEnd < 0)
                {
                    return;
                }

                var headers = Encoding.ASCII.GetString(bytes, (int)_readOffset, headerEnd - (int)_readOffset);
                var contentLength = ContentLengthFraming.ParseContentLength(headers);
                Assert.NotNull(contentLength);

                var bodyStart = headerEnd + 4;
                var bodyEnd = bodyStart + contentLength!.Value;
                if (bytes.Length < bodyEnd)
                {
                    return;
                }

                var body = Encoding.UTF8.GetString(bytes, bodyStart, contentLength.Value);
                var message = JsonNode.Parse(body)!;
                ServerMessages.Add(message);
                Transcript.Add(new TranscriptEntry("server->client", message.DeepClone()));
                _readOffset = bodyEnd;
            }
        }

        private async Task<JsonNode> WaitForResponseAsync(string responseId)
        {
            var start = DateTime.UtcNow;
            while (true)
            {
                DrainOutput();
                var response = ServerMessages.LastOrDefault(message =>
                    message["id"]?.ToJsonString() == responseId);

                if (response is not null)
                {
                    return response.DeepClone();
                }

                if (DateTime.UtcNow - start > TimeSpan.FromSeconds(2))
                {
                    throw new TimeoutException($"Timed out waiting for response '{responseId}'. Errors: {_errorWriter}");
                }

                await Task.Delay(10);
            }
        }

        private static int IndexOfHeaderSeparator(byte[] bytes, int start)
        {
            for (var index = start; index <= bytes.Length - 4; index += 1)
            {
                if (bytes[index] == (byte)'\r' &&
                    bytes[index + 1] == (byte)'\n' &&
                    bytes[index + 2] == (byte)'\r' &&
                    bytes[index + 3] == (byte)'\n')
                {
                    return index;
                }
            }

            return -1;
        }

        public sealed record TranscriptEntry(string Direction, JsonNode Message);
    }
}
