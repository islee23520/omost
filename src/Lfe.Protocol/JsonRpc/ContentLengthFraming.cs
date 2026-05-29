using System.Text;
using System.Text.Json;

namespace Lfe.Protocol.JsonRpc;

public static class ContentLengthFraming
{
    private static readonly byte[] HeaderTerminator = "\r\n\r\n"u8.ToArray();

    public static int? ParseContentLength(string headers)
    {
        foreach (var line in headers.Split("\r\n", StringSplitOptions.RemoveEmptyEntries))
        {
            var separatorIndex = line.IndexOf(':');
            if (separatorIndex < 0)
            {
                continue;
            }

            var headerName = line[..separatorIndex].Trim();
            if (!headerName.Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (int.TryParse(line[(separatorIndex + 1)..].Trim(), out var contentLength) && contentLength >= 0)
            {
                return contentLength;
            }

            return null;
        }

        return null;
    }

    public static async ValueTask<string?> ReadBodyAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        var headers = await ReadHeadersAsync(stream, cancellationToken).ConfigureAwait(false);
        if (headers is null)
        {
            return null;
        }

        var contentLength = ParseContentLength(headers);
        if (!contentLength.HasValue)
        {
            throw new InvalidDataException("Missing or invalid Content-Length header.");
        }

        var bodyBuffer = new byte[contentLength.Value];
        await ReadExactAsync(stream, bodyBuffer, cancellationToken).ConfigureAwait(false);
        return Encoding.UTF8.GetString(bodyBuffer);
    }

    public static ValueTask WriteMessageAsync<TMessage>(
        Stream stream,
        TMessage message,
        JsonSerializerOptions? serializerOptions = null,
        CancellationToken cancellationToken = default)
    {
        var body = JsonSerializer.SerializeToUtf8Bytes(message, serializerOptions ?? JsonRpcProtocol.SerializerOptions);
        return WriteBytesAsync(stream, body, cancellationToken);
    }

    public static async ValueTask WriteRawBodyAsync(
        Stream stream,
        string body,
        CancellationToken cancellationToken = default)
    {
        var bodyBytes = Encoding.UTF8.GetBytes(body);
        await WriteBytesAsync(stream, bodyBytes, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<string?> ReadHeadersAsync(Stream stream, CancellationToken cancellationToken)
    {
        var headerBytes = new List<byte>();
        var singleByte = new byte[1];

        while (true)
        {
            var bytesRead = await stream.ReadAsync(singleByte.AsMemory(0, 1), cancellationToken).ConfigureAwait(false);
            if (bytesRead == 0)
            {
                if (headerBytes.Count == 0)
                {
                    return null;
                }

                throw new EndOfStreamException("Stream ended before the Content-Length header block completed.");
            }

            headerBytes.Add(singleByte[0]);

            if (EndsWithTerminator(headerBytes))
            {
                return Encoding.ASCII.GetString(headerBytes.ToArray(), 0, headerBytes.Count - HeaderTerminator.Length);
            }
        }
    }

    private static async ValueTask ReadExactAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        var offset = 0;

        while (offset < buffer.Length)
        {
            var bytesRead = await stream.ReadAsync(buffer.AsMemory(offset, buffer.Length - offset), cancellationToken)
                .ConfigureAwait(false);

            if (bytesRead == 0)
            {
                throw new EndOfStreamException("Stream ended before the full JSON-RPC body was read.");
            }

            offset += bytesRead;
        }
    }

    private static async ValueTask WriteBytesAsync(Stream stream, byte[] bodyBytes, CancellationToken cancellationToken)
    {
        var headerBytes = Encoding.ASCII.GetBytes($"Content-Length: {bodyBytes.Length}\r\n\r\n");
        await stream.WriteAsync(headerBytes, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(bodyBytes, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static bool EndsWithTerminator(IReadOnlyList<byte> bytes)
    {
        if (bytes.Count < HeaderTerminator.Length)
        {
            return false;
        }

        for (var index = 0; index < HeaderTerminator.Length; index += 1)
        {
            var offset = bytes.Count - HeaderTerminator.Length + index;
            if (bytes[offset] != HeaderTerminator[index])
            {
                return false;
            }
        }

        return true;
    }
}
