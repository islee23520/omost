using System.Text;

namespace Lfe.HashLine;

public interface IHashlineChunkFormatter
{
    IReadOnlyList<string> Push(string formattedLine);
    string? Flush();
}

public sealed class HashlineChunkFormatter : IHashlineChunkFormatter
{
    private readonly int _maxChunkLines;
    private readonly int _maxChunkBytes;
    private readonly List<string> _outputLines = [];
    private int _outputBytes;

    public HashlineChunkFormatter(int maxChunkLines, int maxChunkBytes)
    {
        _maxChunkLines = maxChunkLines;
        _maxChunkBytes = maxChunkBytes;
    }

    public IReadOnlyList<string> Push(string formattedLine)
    {
        var chunksToYield = new List<string>();
        var separatorBytes = _outputLines.Count == 0 ? 0 : 1;
        var lineBytes = Encoding.UTF8.GetByteCount(formattedLine);

        if (_outputLines.Count > 0 && (_outputLines.Count >= _maxChunkLines || _outputBytes + separatorBytes + lineBytes > _maxChunkBytes))
        {
            var flushed = Flush();
            if (flushed is not null)
            {
                chunksToYield.Add(flushed);
            }
        }

        _outputLines.Add(formattedLine);
        _outputBytes += (_outputLines.Count == 1 ? 0 : 1) + lineBytes;

        if (_outputLines.Count >= _maxChunkLines || _outputBytes >= _maxChunkBytes)
        {
            var flushed = Flush();
            if (flushed is not null)
            {
                chunksToYield.Add(flushed);
            }
        }

        return chunksToYield;
    }

    public string? Flush()
    {
        if (_outputLines.Count == 0)
        {
            return null;
        }

        var chunk = string.Join("\n", _outputLines);
        _outputLines.Clear();
        _outputBytes = 0;
        return chunk;
    }
}

public static class HashlineChunkFormatterFactory
{
    public static IHashlineChunkFormatter CreateHashlineChunkFormatter(int maxChunkLines, int maxChunkBytes)
    {
        return new HashlineChunkFormatter(maxChunkLines, maxChunkBytes);
    }
}
