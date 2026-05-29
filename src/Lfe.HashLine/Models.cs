namespace Lfe.HashLine;

public sealed record LineRef(int Line, string Hash);

public sealed record FileTextEnvelope(string Content, bool HadBom, string LineEnding);

public sealed record HashlineApplyReport(string Content, int NoopEdits, int DeduplicatedEdits);

public sealed record HashlineStreamOptions
{
    public int StartLine { get; init; } = 1;
    public int MaxChunkLines { get; init; } = 200;
    public int MaxChunkBytes { get; init; } = 64 * 1024;
}

public sealed record RawHashlineEdit
{
    private static readonly object Missing = new();
    private object? _lines = Missing;

    public string? Op { get; init; }
    public string? Pos { get; init; }
    public string? End { get; init; }
    public object? Lines
    {
        get => ReferenceEquals(_lines, Missing) ? null : _lines;
        init => _lines = value;
    }

    internal bool HasLines => !ReferenceEquals(_lines, Missing);
}

public abstract record HashlineEdit
{
    public abstract string Op { get; }
    public object Lines { get; init; } = null!;
}

public sealed record ReplaceEdit : HashlineEdit
{
    public override string Op => "replace";
    public string Pos { get; init; } = null!;
    public string? End { get; init; }

    [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
    public ReplaceEdit()
    {
    }

    [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
    public ReplaceEdit(string pos, string lines, string? end = null)
    {
        Pos = pos;
        End = end;
        Lines = lines;
    }

    [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
    public ReplaceEdit(string pos, IReadOnlyList<string> lines, string? end = null)
    {
        Pos = pos;
        End = end;
        Lines = lines;
    }
}

public sealed record AppendEdit : HashlineEdit
{
    public override string Op => "append";
    public string? Pos { get; init; }

    [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
    public AppendEdit()
    {
    }

    [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
    public AppendEdit(string lines)
    {
        Lines = lines;
    }

    [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
    public AppendEdit(IReadOnlyList<string> lines)
    {
        Lines = lines;
    }

    [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
    public AppendEdit(string pos, string lines)
    {
        Pos = pos;
        Lines = lines;
    }

    [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
    public AppendEdit(string pos, IReadOnlyList<string> lines)
    {
        Pos = pos;
        Lines = lines;
    }
}

public sealed record PrependEdit : HashlineEdit
{
    public override string Op => "prepend";
    public string? Pos { get; init; }

    [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
    public PrependEdit()
    {
    }

    [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
    public PrependEdit(string lines)
    {
        Lines = lines;
    }

    [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
    public PrependEdit(IReadOnlyList<string> lines)
    {
        Lines = lines;
    }

    [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
    public PrependEdit(string pos, string lines)
    {
        Pos = pos;
        Lines = lines;
    }

    [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
    public PrependEdit(string pos, IReadOnlyList<string> lines)
    {
        Pos = pos;
        Lines = lines;
    }
}
