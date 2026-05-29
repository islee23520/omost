using System.Text;

using Lfe.SearchTools;

namespace Lfe.SearchTools.Tests;

public sealed class ProcessStreamReaderTests
{
    [Fact]
    public async Task ReadProcessStreamReadsUtf8Content()
    {
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("hello\nworld"));
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);

        var text = await ProcessStreamReader.ReadProcessStreamAsync(new ProcessReadableStream(reader));

        Assert.Equal("hello\nworld", text);
    }

    [Fact]
    public async Task CollectSearchProcessOutputReturnsStdoutStderrAndExitCode()
    {
        await using var stdoutStream = new MemoryStream(Encoding.UTF8.GetBytes("out"));
        await using var stderrStream = new MemoryStream(Encoding.UTF8.GetBytes("err"));
        using var stdoutReader = new StreamReader(stdoutStream, Encoding.UTF8, leaveOpen: true);
        using var stderrReader = new StreamReader(stderrStream, Encoding.UTF8, leaveOpen: true);
        using var process = new SpawnedProcess(
            Task.FromResult(3),
            new ProcessReadableStream(stdoutReader),
            new ProcessReadableStream(stderrReader));

        var output = await SearchProcessOutputCollector.CollectSearchProcessOutputAsync(process, 1_000, "timed out");

        Assert.Equal("out", output.Stdout);
        Assert.Equal("err", output.Stderr);
        Assert.Equal(3, output.ExitCode);
    }
}
