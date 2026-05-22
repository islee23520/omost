namespace Omodot.HashLine;

public static class EditOrdering
{
    public static double GetEditLineNumber(HashlineEdit edit)
    {
        return edit switch
        {
            ReplaceEdit replace => Validation.ParseLineRef(replace.End ?? replace.Pos).Line,
            AppendEdit append when append.Pos is not null => Validation.ParseLineRef(append.Pos).Line,
            AppendEdit => double.NegativeInfinity,
            PrependEdit prepend when prepend.Pos is not null => Validation.ParseLineRef(prepend.Pos).Line,
            PrependEdit => double.NegativeInfinity,
            _ => double.PositiveInfinity,
        };
    }

    public static IReadOnlyList<string> CollectLineRefs(IReadOnlyList<HashlineEdit> edits)
    {
        var refs = new List<string>();
        foreach (var edit in edits)
        {
            switch (edit)
            {
                case ReplaceEdit replace when replace.End is not null:
                    refs.Add(replace.Pos);
                    refs.Add(replace.End);
                    break;
                case ReplaceEdit replace:
                    refs.Add(replace.Pos);
                    break;
                case AppendEdit append when append.Pos is not null:
                    refs.Add(append.Pos);
                    break;
                case PrependEdit prepend when prepend.Pos is not null:
                    refs.Add(prepend.Pos);
                    break;
            }
        }

        return refs;
    }

    public static string? DetectOverlappingRanges(IReadOnlyList<HashlineEdit> edits)
    {
        var ranges = new List<(int Start, int End, int Index)>();
        for (var index = 0; index < edits.Count; index += 1)
        {
            if (edits[index] is not ReplaceEdit { End: not null } replace)
            {
                continue;
            }

            ranges.Add((Validation.ParseLineRef(replace.Pos).Line, Validation.ParseLineRef(replace.End!).Line, index));
        }

        if (ranges.Count < 2)
        {
            return null;
        }

        var ordered = ranges.OrderBy(range => range.Start).ThenBy(range => range.End).ToArray();
        for (var index = 1; index < ordered.Length; index += 1)
        {
            var previous = ordered[index - 1];
            var current = ordered[index];
            if (current.Start <= previous.End)
            {
                return $"Overlapping range edits detected: edit {previous.Index + 1} (lines {previous.Start}-{previous.End}) overlaps with edit {current.Index + 1} (lines {current.Start}-{current.End}). Use pos-only replace for single-line edits.";
            }
        }

        return null;
    }
}
