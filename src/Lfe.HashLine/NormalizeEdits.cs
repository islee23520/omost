namespace Lfe.HashLine;

public static class NormalizeEdits
{
    public static IReadOnlyList<HashlineEdit> NormalizeHashlineEdits(IReadOnlyList<RawHashlineEdit> rawEdits)
    {
        return rawEdits.Select((rawEdit, index) => Normalize(rawEdit ?? new RawHashlineEdit(), index)).ToArray();
    }

    private static HashlineEdit Normalize(RawHashlineEdit edit, int index)
    {
        return edit.Op switch
        {
            "replace" => NormalizeReplaceEdit(edit, index),
            "append" => NormalizeAppendEdit(edit, index),
            "prepend" => NormalizePrependEdit(edit, index),
            _ => throw new ArgumentException($"Edit {index}: unsupported op \"{edit.Op}\". Legacy format was removed; use op/pos/end/lines."),
        };
    }

    private static ReplaceEdit NormalizeReplaceEdit(RawHashlineEdit edit, int index)
    {
        var pos = NormalizeAnchor(edit.Pos);
        var end = NormalizeAnchor(edit.End);
        var anchor = RequireLine(pos ?? end, index, "replace");
        var lines = RequireLines(edit, index);
        return new ReplaceEdit
        {
            Pos = anchor,
            End = end,
            Lines = lines,
        };
    }

    private static AppendEdit NormalizeAppendEdit(RawHashlineEdit edit, int index)
    {
        var anchor = NormalizeAnchor(edit.Pos) ?? NormalizeAnchor(edit.End);
        return new AppendEdit
        {
            Pos = anchor,
            Lines = RequireLines(edit, index),
        };
    }

    private static PrependEdit NormalizePrependEdit(RawHashlineEdit edit, int index)
    {
        var anchor = NormalizeAnchor(edit.Pos) ?? NormalizeAnchor(edit.End);
        return new PrependEdit
        {
            Pos = anchor,
            Lines = RequireLines(edit, index),
        };
    }

    private static string? NormalizeAnchor(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static object RequireLines(RawHashlineEdit edit, int index)
    {
        if (!edit.HasLines)
        {
            throw new ArgumentException($"Edit {index}: lines is required for {edit.Op ?? "unknown"}");
        }

        if (edit.Lines is null)
        {
            return Array.Empty<string>();
        }

        return edit.Lines;
    }

    private static string RequireLine(string? anchor, int index, string op)
    {
        if (!string.IsNullOrEmpty(anchor))
        {
            return anchor;
        }

        throw new ArgumentException($"Edit {index}: {op} requires at least one anchor line reference (pos or end)");
    }
}
