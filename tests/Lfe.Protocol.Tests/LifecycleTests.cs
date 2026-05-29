using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using Lfe.Protocol.JsonRpc;
using Lfe.Sidecar;

namespace Lfe.Protocol.Tests;

public sealed class LifecycleTests
{
    [Fact]
    public async Task DisposeAsync_ClearsState_And_PreventsFurtherOperations()
    {
        using var harness = new LifecycleHarness();
        var closeCount = 0;
        var errorCount = 0;
        var notificationCount = 0;

        harness.Server.OnClose(() => closeCount += 1);
        harness.Server.OnError(_ => errorCount += 1);
        harness.Server.OnNotification("test/notification", _ => notificationCount += 1);

        await harness.Server.DisposeAsync();
        await harness.Server.AcceptChunkAsync(CreateFrame(new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["method"] = "test/notification",
            ["params"] = new JsonObject
            {
                ["value"] = 7,
            },
        }));

        harness.DrainOutput();

        Assert.Equal(1, closeCount);
        Assert.Equal(0, errorCount);
        Assert.Equal(0, notificationCount);
        Assert.Empty(harness.ServerMessages);
        Assert.Equal(string.Empty, harness.ErrorText);
    }

    [Fact]
    public async Task RunAsync_ThrowsOnDoubleListen()
    {
        using var harness = new LifecycleHarness(new BlockingReadStream());
        using var cancellation = new CancellationTokenSource();

        var firstRun = harness.Server.RunAsync(cancellation.Token);

        await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Server.RunAsync(CancellationToken.None));

        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await firstRun);
    }

    [Fact]
    public async Task AcceptChunkAsync_AfterDispose_ReturnsEarly()
    {
        using var harness = new LifecycleHarness();

        await harness.Server.DisposeAsync();
        await harness.Server.AcceptChunkAsync(CreateFrame(new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = 1,
            ["method"] = "lfe.initialize",
            ["params"] = new JsonObject
            {
                ["protocolVersion"] = "1.0.0",
                ["hostName"] = "opencode",
                ["hostVersion"] = "0.1.0",
                ["clientKind"] = "host-bridge",
                ["requestedCapabilities"] = new JsonArray("phase1.initialize"),
            },
        }));

        harness.DrainOutput();

        Assert.Empty(harness.ServerMessages);
        Assert.Equal(string.Empty, harness.ErrorText);
    }

    [Fact]
    public async Task OnClose_FiresWhenStreamEnds()
    {
        using var harness = new LifecycleHarness(new MemoryStream());
        var closeCount = 0;

        harness.Server.OnClose(() => closeCount += 1);

        await harness.Server.RunAsync();

        Assert.Equal(1, closeCount);
    }

    [Fact]
    public async Task OnError_FiresOnStreamException()
    {
        using var harness = new LifecycleHarness(new ThrowingReadStream(new IOException("boom")));
        Exception? captured = null;
        var closeCount = 0;

        harness.Server.OnError(exception => captured = exception);
        harness.Server.OnClose(() => closeCount += 1);

        await harness.Server.RunAsync();

        Assert.NotNull(captured);
        Assert.IsType<IOException>(captured);
        Assert.Equal("boom", captured!.Message);
        Assert.Equal(1, closeCount);
    }

    [Fact]
    public async Task OnNotification_DispatchesToRegisteredHandler()
    {
        using var harness = new LifecycleHarness();
        JsonElement? receivedParams = null;
        var callCount = 0;

        harness.Server.OnNotification("test/notification", parameters =>
        {
            callCount += 1;
            receivedParams = parameters.Clone();
        });

        await harness.Server.AcceptChunkAsync(CreateFrame(new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["method"] = "test/notification",
            ["params"] = new JsonObject
            {
                ["value"] = 7,
                ["message"] = "ok",
            },
        }));

        harness.DrainOutput();

        Assert.Equal(1, callCount);
        Assert.True(receivedParams.HasValue);
        Assert.Equal(7, receivedParams.Value.GetProperty("value").GetInt32());
        Assert.Equal("ok", receivedParams.Value.GetProperty("message").GetString());
        Assert.Empty(harness.ServerMessages);
        Assert.Equal(string.Empty, harness.ErrorText);
    }

    [Fact]
    public async Task UnknownNotification_IsSilentlyIgnored()
    {
        using var harness = new LifecycleHarness();

        await harness.Server.AcceptChunkAsync(CreateFrame(new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["method"] = "test/unknown",
            ["params"] = new JsonObject
            {
                ["value"] = 7,
            },
        }));

        harness.DrainOutput();

        Assert.Empty(harness.ServerMessages);
        Assert.Equal(string.Empty, harness.ErrorText);
    }

    [Fact]
    public async Task DisposeAsync_FiresOnCloseHandlers()
    {
        using var harness = new LifecycleHarness();
        var closeCount = 0;

        harness.Server.OnClose(() => closeCount += 1);

        await harness.Server.DisposeAsync();

        Assert.Equal(1, closeCount);
    }

    [Fact]
    public async Task EmitAsync_AfterDispose_CompletesWithoutError()
    {
        using var harness = new LifecycleHarness();

        await harness.Server.DisposeAsync();
        await harness.Server.EmitAsync("test/notification", new { value = 7 });

        harness.DrainOutput();

        Assert.Empty(harness.ServerMessages);
    }

    private static byte[] CreateFrame(JsonNode request)
    {
        using var stream = new MemoryStream();
        ContentLengthFraming.WriteRawBodyAsync(stream, request.ToJsonString(), default).GetAwaiter().GetResult();
        return stream.ToArray();
    }

    private sealed class LifecycleHarness : IDisposable
    {
        private readonly StringWriter _errorWriter = new();
        private readonly MemoryStream _output = new();
        private long _readOffset;

        public LifecycleHarness(Stream? reader = null)
        {
            Reader = reader ?? Stream.Null;
            Server = new JsonRpcServer(Reader, _output, _errorWriter);
        }

        public Stream Reader { get; }

        public JsonRpcServer Server { get; }

        public List<JsonNode> ServerMessages { get; } = new();

        public string ErrorText => _errorWriter.ToString();

        public void Dispose()
        {
            Reader.Dispose();
            _output.Dispose();
            _errorWriter.Dispose();
        }

        public void DrainOutput()
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
                ServerMessages.Add(JsonNode.Parse(body)!);
                _readOffset = bodyEnd;
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
    }

    private sealed class BlockingReadStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return 0;
        }
    }

    private sealed class ThrowingReadStream(Exception exception) : Stream
    {
        private readonly Exception _exception = exception;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            return ValueTask.FromException<int>(_exception);
        }
    }
}
