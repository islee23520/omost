namespace Omodot.HashLine;

public static class EditOperations
{
    public static HashlineApplyReport ApplyHashlineEditsWithReport(string content, IReadOnlyList<HashlineEdit> edits)
    {
        if (edits.Count == 0)
        {
            return new HashlineApplyReport(content, 0, 0);
        }

        var dedupeResult = EditDeduplication.DedupeEdits(edits);
        var precedence = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["replace"] = 0,
            ["append"] = 1,
            ["prepend"] = 2,
        };

        var sortedEdits = dedupeResult.Edits
            .OrderByDescending(EditOrdering.GetEditLineNumber)
            .ThenBy(edit => precedence.TryGetValue(edit.Op, out var rank) ? rank : 3)
            .ToArray();

        var lines = content.Length == 0 ? [] : content.Split('\n').ToList();
        Validation.ValidateLineRefs(lines, EditOrdering.CollectLineRefs(sortedEdits));

        var overlapError = EditOrdering.DetectOverlappingRanges(sortedEdits);
        if (overlapError is not null)
        {
            throw new ArgumentException(overlapError);
        }

        var noopEdits = 0;
        foreach (var edit in sortedEdits)
        {
            switch (edit)
            {
                case ReplaceEdit replace:
                {
                    var next = replace.End is not null
                        ? EditOperationPrimitives.ApplyReplaceLines(lines, replace.Pos, replace.End, replace.Lines, skipValidation: true)
                        : EditOperationPrimitives.ApplySetLine(lines, replace.Pos, replace.Lines, skipValidation: true);
                    if (next.SequenceEqual(lines))
                    {
                        noopEdits += 1;
                    }
                    else
                    {
                        lines = next.ToList();
                    }

                    break;
                }
                case AppendEdit append:
                    lines = (append.Pos is not null
                        ? EditOperationPrimitives.ApplyInsertAfter(lines, append.Pos, append.Lines, skipValidation: true)
                        : EditOperationPrimitives.ApplyAppend(lines, append.Lines)).ToList();
                    break;
                case PrependEdit prepend:
                    lines = (prepend.Pos is not null
                        ? EditOperationPrimitives.ApplyInsertBefore(lines, prepend.Pos, prepend.Lines, skipValidation: true)
                        : EditOperationPrimitives.ApplyPrepend(lines, prepend.Lines)).ToList();
                    break;
            }
        }

        return new HashlineApplyReport(string.Join("\n", lines), noopEdits, dedupeResult.DeduplicatedEdits);
    }

    public static string ApplyHashlineEdits(string content, IReadOnlyList<HashlineEdit> edits)
    {
        return ApplyHashlineEditsWithReport(content, edits).Content;
    }
}
