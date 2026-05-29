using System.Text.Json.Serialization;

namespace Lfe.AstGrep;

public enum SgTruncatedReason
{
    MaxMatches,
    MaxOutputBytes,
    Timeout,
}

public sealed record Position
{
    [JsonPropertyName("line")]
    public int Line { get; init; }

    [JsonPropertyName("column")]
    public int Column { get; init; }
}

public sealed record ByteOffsetRange
{
    [JsonPropertyName("start")]
    public int Start { get; init; }

    [JsonPropertyName("end")]
    public int End { get; init; }
}

public sealed record CliRange
{
    [JsonPropertyName("byteOffset")]
    public ByteOffsetRange ByteOffset { get; init; } = new();

    [JsonPropertyName("start")]
    public Position Start { get; init; } = new();

    [JsonPropertyName("end")]
    public Position End { get; init; } = new();
}

public sealed record CharCount
{
    [JsonPropertyName("leading")]
    public int Leading { get; init; }

    [JsonPropertyName("trailing")]
    public int Trailing { get; init; }
}

public sealed record CliMatch
{
    [JsonPropertyName("text")]
    public string Text { get; init; } = string.Empty;

    [JsonPropertyName("range")]
    public CliRange Range { get; init; } = new();

    [JsonPropertyName("file")]
    public string File { get; init; } = string.Empty;

    [JsonPropertyName("lines")]
    public string Lines { get; init; } = string.Empty;

    [JsonPropertyName("charCount")]
    public CharCount CharCount { get; init; } = new();

    [JsonPropertyName("language")]
    public string Language { get; init; } = string.Empty;
}

public sealed record SgResult
{
    public IReadOnlyList<CliMatch> Matches { get; init; } = Array.Empty<CliMatch>();

    public int TotalMatches { get; init; }

    public bool Truncated { get; init; }

    public SgTruncatedReason? TruncatedReason { get; init; }

    public string? Error { get; init; }
}
