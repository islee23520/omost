using System.Text;
using Omodot.HashLine;

namespace Omodot.HashLine.Tests;

public sealed class HashComputationTests
{
    [Fact]
    public void ComputeLineHashIsDeterministic()
    {
        var hash1 = HashComputation.ComputeLineHash(1, "function hello() {");
        var hash2 = HashComputation.ComputeLineHash(1, "function hello() {");

        Assert.Equal(hash1, hash2);
        Assert.Matches("^[ZPMQVRWSNKTXJBYH]{2}$", hash1);
    }

    [Fact]
    public void SignificantContentIgnoresLineNumber()
    {
        Assert.Equal(
            HashComputation.ComputeLineHash(1, "function hello() {"),
            HashComputation.ComputeLineHash(2, "function hello() {"));
    }

    [Fact]
    public void NonSignificantContentUsesLineNumberSeed()
    {
        Assert.NotEqual(HashComputation.ComputeLineHash(1, "{}"), HashComputation.ComputeLineHash(2, "{}"));
    }

    [Fact]
    public void LegacyHashIgnoresWhitespaceDifferences()
    {
        Assert.Equal(
            HashComputation.ComputeLegacyLineHash(1, "if (a && b) {"),
            HashComputation.ComputeLegacyLineHash(1, "if(a&&b){"));
    }

    [Fact]
    public async Task StreamingUtf8MatchesFormattedHashLinesAsync()
    {
        var content = "a\nb\nc";
        var chunks = ToUtf8Chunks(content, 1);
        var result = await CollectAsync(HashComputation.StreamHashLinesFromUtf8Async(chunks, new HashlineStreamOptions { MaxChunkLines = 1 }));
        Assert.Equal(HashComputation.FormatHashLines(content), result);
    }

    [Fact]
    public async Task StreamingLinesMatchesFormattedHashLinesAsync()
    {
        var content = "x\ny\n";
        var result = await CollectAsync(HashComputation.StreamHashLinesFromLinesAsync(new[] { "x", "y", string.Empty }, new HashlineStreamOptions { MaxChunkLines = 2 }));
        Assert.Equal(HashComputation.FormatHashLines(content), result);
    }

    private static async IAsyncEnumerable<ReadOnlyMemory<byte>> ToUtf8Chunks(string text, int chunkSize)
    {
        var encoded = Encoding.UTF8.GetBytes(text);
        for (var index = 0; index < encoded.Length; index += chunkSize)
        {
            yield return encoded.AsMemory(index, Math.Min(chunkSize, encoded.Length - index));
            await Task.Yield();
        }
    }

    private static async Task<string> CollectAsync(IAsyncEnumerable<string> stream)
    {
        var parts = new List<string>();
        await foreach (var chunk in stream)
        {
            parts.Add(chunk);
        }

        return string.Join("\n", parts);
    }
}
