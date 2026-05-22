using System.Text.Json;

using Omodot.Protocol.JsonRpc;
using Omodot.Protocol.Types;

namespace Omodot.Protocol.Tests;

public sealed class ContentLengthFramingTests
{
    [Fact]
    public void ParseContentLengthReadsTheCanonicalHeader()
    {
        var contentLength = ContentLengthFraming.ParseContentLength("Content-Length: 42\r\nX-Test: ok");
        Assert.Equal(42, contentLength);
    }

    [Fact]
    public async Task WriteAndReadRoundTripPreservesTheJsonRpcBody()
    {
        await using var stream = new MemoryStream();

        await ContentLengthFraming.WriteMessageAsync(
            stream,
            new JsonRpcNotificationMessage<RunProgressParams>
            {
                Method = OmoNotificationNames.RunProgress,
                Params = new RunProgressParams
                {
                    Message = "Working",
                    Phase = OmoRunPhaseValues.Running,
                    RunId = "run-1",
                },
            },
            JsonRpcProtocol.SerializerOptions);

        stream.Position = 0;

        var body = await ContentLengthFraming.ReadBodyAsync(stream);

        Assert.NotNull(body);
        using var document = JsonDocument.Parse(body!);
        Assert.Equal("2.0", document.RootElement.GetProperty("jsonrpc").GetString());
        Assert.Equal("omo.run.progress", document.RootElement.GetProperty("method").GetString());
        Assert.Equal("run-1", document.RootElement.GetProperty("params").GetProperty("runId").GetString());
    }
}
