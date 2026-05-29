using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace Lfe.HashLine;

public static class HashComputation
{
    private static bool HasSignificantContent(string text)
    {
        foreach (var rune in text.EnumerateRunes())
        {
            if (Rune.IsLetterOrDigit(rune))
            {
                return true;
            }
        }

        return false;
    }

    private static string ComputeNormalizedLineHash(int lineNumber, string normalizedContent)
    {
        var seed = HasSignificantContent(normalizedContent) ? 0 : lineNumber;
        var hash = XxHash32Utility.HashXxh32(normalizedContent, seed);
        var index = (int)(hash % 256);
        return HashlineConstants.HashlineDictionary[index];
    }

    public static string ComputeLineHash(int lineNumber, string content)
    {
        return ComputeNormalizedLineHash(lineNumber, content.Replace("\r", string.Empty, StringComparison.Ordinal).TrimEnd());
    }

    public static string ComputeLegacyLineHash(int lineNumber, string content)
    {
        return ComputeNormalizedLineHash(lineNumber, RemoveAllWhitespace(content.Replace("\r", string.Empty, StringComparison.Ordinal)));
    }

    public static string FormatHashLine(int lineNumber, string content)
    {
        return $"{lineNumber}#{ComputeLineHash(lineNumber, content)}|{content}";
    }

    public static string FormatHashLines(string content)
    {
        if (content.Length == 0)
        {
            return string.Empty;
        }

        var lines = content.Split('\n');
        return string.Join("\n", lines.Select((line, index) => FormatHashLine(index + 1, line)));
    }

    public static async IAsyncEnumerable<string> StreamHashLinesFromUtf8Async(
        Stream source,
        HashlineStreamOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        options ??= new HashlineStreamOptions();
        await foreach (var chunk in StreamHashLinesFromUtf8Async(ReadStreamChunksAsync(source, cancellationToken), options, cancellationToken))
        {
            yield return chunk;
        }
    }

    public static async IAsyncEnumerable<string> StreamHashLinesFromUtf8Async(
        IAsyncEnumerable<ReadOnlyMemory<byte>> source,
        HashlineStreamOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        options ??= new HashlineStreamOptions();
        var lineNumber = options.StartLine;
        var pending = string.Empty;
        var sawAnyText = false;
        var endedWithNewline = false;
        var decoder = Encoding.UTF8.GetDecoder();
        var chunkFormatter = HashlineChunkFormatterFactory.CreateHashlineChunkFormatter(options.MaxChunkLines, options.MaxChunkBytes);

        await foreach (var chunk in source.WithCancellation(cancellationToken))
        {
            var text = DecodeChunk(decoder, chunk.Span, flush: false);
            foreach (var line in ConsumeText(text, ref pending, ref sawAnyText, ref endedWithNewline))
            {
                foreach (var output in chunkFormatter.Push(FormatHashLine(lineNumber, line)))
                {
                    yield return output;
                }

                lineNumber += 1;
            }
        }

        var finalText = DecodeChunk(decoder, ReadOnlySpan<byte>.Empty, flush: true);
        if (finalText.Length > 0)
        {
            sawAnyText = true;
            pending += finalText;
        }

        if (sawAnyText && (pending.Length > 0 || endedWithNewline))
        {
            foreach (var output in chunkFormatter.Push(FormatHashLine(lineNumber, pending)))
            {
                yield return output;
            }
        }

        var finalChunk = chunkFormatter.Flush();
        if (finalChunk is not null)
        {
            yield return finalChunk;
        }
    }

    public static async IAsyncEnumerable<string> StreamHashLinesFromLinesAsync(
        IEnumerable<string> lines,
        HashlineStreamOptions? options = null)
    {
        options ??= new HashlineStreamOptions();
        var lineNumber = options.StartLine;
        var chunkFormatter = HashlineChunkFormatterFactory.CreateHashlineChunkFormatter(options.MaxChunkLines, options.MaxChunkBytes);

        foreach (var line in lines)
        {
            foreach (var output in chunkFormatter.Push(FormatHashLine(lineNumber, line)))
            {
                yield return output;
            }

            lineNumber += 1;
        }

        var finalChunk = chunkFormatter.Flush();
        if (finalChunk is not null)
        {
            yield return finalChunk;
        }

        await Task.CompletedTask;
    }

    public static async IAsyncEnumerable<string> StreamHashLinesFromLinesAsync(
        IAsyncEnumerable<string> lines,
        HashlineStreamOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        options ??= new HashlineStreamOptions();
        var lineNumber = options.StartLine;
        var chunkFormatter = HashlineChunkFormatterFactory.CreateHashlineChunkFormatter(options.MaxChunkLines, options.MaxChunkBytes);

        await foreach (var line in lines.WithCancellation(cancellationToken))
        {
            foreach (var output in chunkFormatter.Push(FormatHashLine(lineNumber, line)))
            {
                yield return output;
            }

            lineNumber += 1;
        }

        var finalChunk = chunkFormatter.Flush();
        if (finalChunk is not null)
        {
            yield return finalChunk;
        }
    }

    private static async IAsyncEnumerable<ReadOnlyMemory<byte>> ReadStreamChunksAsync(
        Stream source,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];
        while (true)
        {
            var read = await source.ReadAsync(buffer.AsMemory(), cancellationToken);
            if (read == 0)
            {
                yield break;
            }

            var chunk = new byte[read];
            Array.Copy(buffer, chunk, read);
            yield return chunk;
        }
    }

    private static string DecodeChunk(Decoder decoder, ReadOnlySpan<byte> bytes, bool flush)
    {
        Span<char> initial = stackalloc char[Math.Max(1, Encoding.UTF8.GetMaxCharCount(bytes.Length))];
        if (initial.Length < Encoding.UTF8.GetMaxCharCount(bytes.Length))
        {
            var rented = new char[Encoding.UTF8.GetMaxCharCount(bytes.Length)];
            decoder.Convert(bytes, rented, flush, out _, out var charsUsed, out _);
            return new string(rented, 0, charsUsed);
        }

        decoder.Convert(bytes, initial, flush, out _, out var used, out _);
        return new string(initial[..used]);
    }

    private static IReadOnlyList<string> ConsumeText(string text, ref string pendingText, ref bool sawAny, ref bool endedNewline)
    {
        if (text.Length == 0)
        {
            return Array.Empty<string>();
        }

        sawAny = true;
        pendingText += text;
        var lines = new List<string>();
        var lastIndex = 0;
        while (true)
        {
            var nextIndex = pendingText.IndexOf('\n', lastIndex);
            if (nextIndex < 0)
            {
                break;
            }

            lines.Add(pendingText[lastIndex..nextIndex]);
            lastIndex = nextIndex + 1;
            endedNewline = true;
        }

        pendingText = pendingText[lastIndex..];
        if (pendingText.Length > 0)
        {
            endedNewline = false;
        }

        return lines;
    }

    private static string RemoveAllWhitespace(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            if (!char.IsWhiteSpace(character))
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }
}
