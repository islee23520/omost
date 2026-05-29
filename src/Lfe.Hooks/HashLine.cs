namespace Lfe.Hooks;

using System.Text.RegularExpressions;

public static partial class HashLine
{
    #region Hash Line Functions

    private static readonly string HashlineNibbleStr = "ZPMQVRWSNKTXJBYH";
    private static readonly string[] HashlineDict = Enumerable.Range(0, 256)
        .Select(i => $"{HashlineNibbleStr[i >> 4]}{HashlineNibbleStr[i & 0x0f]}")
        .ToArray();

    [GeneratedRegex(@"^\s*(\d+): ?(.*)$")]
    private static partial Regex ColonReadLinePattern();

    [GeneratedRegex(@"^\s*(\d+)\| ?(.*)$")]
    private static partial Regex PipeReadLinePattern();

    private const string OpencodeLineTruncationSuffix = "... (line truncated to 2000 chars)";

    public static string ComputeLineHash(int lineNumber, string content)
    {
        var normalized = content.Replace("\r", "").TrimEnd();
        var seed = Regex.IsMatch(normalized, @"[\p{L}\p{N}]") ? 0 : lineNumber;
        return HashlineDict[XxHash32(normalized, seed) % 256];
    }

    public static string FormatHashLine(int lineNumber, string content) =>
        $"{lineNumber}#{ComputeLineHash(lineNumber, content)}|{content}";

    public static string TransformHashlineReadOutput(string output)
    {
        if (string.IsNullOrEmpty(output)) return output;
        var lines = output.Split('\n');
        var contentStart = Array.FindIndex(lines, l => l == "<content>" || l.StartsWith("<content>"));
        var contentEnd = Array.IndexOf(lines, "</content>");
        var fileStart = Array.FindIndex(lines, l => l == "<file>" || l.StartsWith("<file>"));
        var fileEnd = Array.IndexOf(lines, "</file>");
        var blockStart = contentStart != -1 ? contentStart : fileStart;
        var blockEnd = contentStart != -1 ? contentEnd : fileEnd;
        var openTag = contentStart != -1 ? "<content>" : "<file>";

        if (blockStart != -1 && blockEnd != -1 && blockEnd > blockStart)
        {
            var openLine = lines[blockStart] ?? "";
            var inlineFirst = openLine.StartsWith(openTag) && openLine != openTag
                ? openLine[openTag.Length..] : null;
            var fileLines = inlineFirst is not null
                ? [inlineFirst, .. lines[(blockStart + 1)..blockEnd]]
                : lines[(blockStart + 1)..blockEnd];

            if (fileLines.Length > 0 && !IsHashlineTextFile(fileLines[0] ?? "")) return output;
            var transformed = TransformHashlineLines(fileLines);
            var prefix = inlineFirst is not null
                ? [.. lines[..blockStart], openTag]
                : lines[..(blockStart + 1)];
            return string.Join("\n", [.. prefix, .. transformed, .. lines[blockEnd..]]);
        }

        return lines.Length > 0 && IsHashlineTextFile(lines[0] ?? "")
            ? string.Join("\n", TransformHashlineLines(lines))
            : output;
    }

    public static string BuildHashlineWriteSuccessOutput(string output, object? metadata)
    {
        if (output.StartsWith(HookDefinitions.WriteSuccessMarker) ||
            output.StartsWith("error", StringComparison.OrdinalIgnoreCase) ||
            output.Contains("failed", StringComparison.OrdinalIgnoreCase))
            return output;

        var lineCount = ExtractMetadataLineCount(metadata);
        return lineCount is null ? output : $"{HookDefinitions.WriteSuccessMarker} {lineCount} lines written.";
    }

    #endregion

    #region Private Helpers

    private static bool IsHashlineTextFile(string firstLine) =>
        ColonReadLinePattern().IsMatch(firstLine) || PipeReadLinePattern().IsMatch(firstLine);

    private static (int LineNumber, string Content)? ParseHashlineReadLine(string line)
    {
        var colonMatch = ColonReadLinePattern().Match(line);
        if (colonMatch.Success)
            return (int.Parse(colonMatch.Groups[1].Value), colonMatch.Groups[2].Value);
        var pipeMatch = PipeReadLinePattern().Match(line);
        if (pipeMatch.Success)
            return (int.Parse(pipeMatch.Groups[1].Value), pipeMatch.Groups[2].Value);
        return null;
    }

    private static string[] TransformHashlineLines(string[] lines)
    {
        var result = new List<string>();
        for (var i = 0; i < lines.Length; i++)
        {
            var parsed = ParseHashlineReadLine(lines[i] ?? "");
            if (parsed is null)
            {
                result.AddRange(lines[result.Count..]);
                break;
            }
            result.Add(parsed.Value.Content.EndsWith(OpencodeLineTruncationSuffix)
                ? lines[i] ?? ""
                : FormatHashLine(parsed.Value.LineNumber, parsed.Value.Content));
        }
        return result.ToArray();
    }

    private static int? ExtractMetadataLineCount(object? metadata)
    {
        if (metadata is null || metadata is not Dictionary<string, object> dict) return null;
        foreach (var key in new[] { "lineCount", "linesWritten", "lines" })
        {
            if (dict.TryGetValue(key, out var value) && value is int i && i >= 0)
                return i;
        }
        return null;
    }

    #endregion

    #region xxHash32

    public static uint XxHash32(string input, int seed)
    {
        var data = System.Text.Encoding.UTF8.GetBytes(input);
        var offset = 0;
        uint hash;

        if (data.Length >= 16)
        {
            var limit = data.Length - 16;
            var v1 = (uint)(seed + 0x9e3779b1 + 0x85ebca77);
            var v2 = (uint)(seed + 0x85ebca77);
            var v3 = (uint)seed;
            var v4 = (uint)(seed - 0x9e3779b1);

            while (offset <= limit)
            {
                v1 = Round32(v1, ReadUint32LittleEndian(data, offset)); offset += 4;
                v2 = Round32(v2, ReadUint32LittleEndian(data, offset)); offset += 4;
                v3 = Round32(v3, ReadUint32LittleEndian(data, offset)); offset += 4;
                v4 = Round32(v4, ReadUint32LittleEndian(data, offset)); offset += 4;
            }

            hash = RotateLeft32(v1, 1) + RotateLeft32(v2, 7);
            hash += RotateLeft32(v3, 12);
            hash += RotateLeft32(v4, 18);
        }
        else
        {
            hash = (uint)(seed + 0x165667b1);
        }

        hash += (uint)data.Length;

        while (offset + 4 <= data.Length)
        {
            hash += ReadUint32LittleEndian(data, offset) * 0xc2b2ae3du;
            hash = RotateLeft32(hash, 17) * 0x27d4eb2fu;
            offset += 4;
        }

        while (offset < data.Length)
        {
            hash += data[offset] * 0x165667b1u;
            hash = RotateLeft32(hash, 11) * 0x9e3779b1u;
            offset++;
        }

        hash = (hash ^ (hash >> 15)) * 0x85ebca77u;
        hash = (hash ^ (hash >> 13)) * 0xc2b2ae3du;
        return hash ^ (hash >> 16);
    }

    private static uint Round32(uint accumulator, uint value) =>
        RotateLeft32(accumulator + value * 0x85ebca77u, 13) * 0x9e3779b1u;

    private static uint RotateLeft32(uint value, int bits) =>
        (value << bits) | (value >> (32 - bits));

    private static uint ReadUint32LittleEndian(byte[] data, int offset) =>
        (uint)(data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16) | (data[offset + 3] << 24));

    #endregion
}
