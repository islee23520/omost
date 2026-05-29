namespace Lfe.HashLine;

public static class DiffUtils
{
    public static string ToHashlineContent(string content)
    {
        if (content.Length == 0)
        {
            return content;
        }

        var lines = content.Split('\n');
        var hasTrailingNewline = lines[^1] == string.Empty;
        var contentLines = hasTrailingNewline ? lines[..^1] : lines;
        var hashLined = contentLines.Select((line, index) => $"{index + 1}#{HashComputation.ComputeLineHash(index + 1, line)}|{line}");
        return hasTrailingNewline ? string.Join("\n", hashLined) + "\n" : string.Join("\n", hashLined);
    }

    public static string GenerateUnifiedDiff(string oldContent, string newContent, string filePath)
    {
        var oldLines = SplitLines(oldContent);
        var newLines = SplitLines(newContent);
        var diff = BuildDiff(oldLines.Lines, newLines.Lines);
        var hunks = BuildHunks(diff, contextLines: 3);

        var output = new List<string>
        {
            $"Index: {filePath}",
            "===================================================================",
            $"--- {filePath}",
            $"+++ {filePath}",
        };

        foreach (var hunk in hunks)
        {
            output.Add($"@@ -{FormatRange(hunk.OldStart, hunk.OldCount)} +{FormatRange(hunk.NewStart, hunk.NewCount)} @@");
            foreach (var line in hunk.Lines)
            {
                output.Add(line.Text);
                if (line.NoNewlineMarker)
                {
                    output.Add("\\ No newline at end of file");
                }
            }
        }

        return string.Join("\n", output) + "\n";
    }

    public static (int Additions, int Deletions) CountLineDiffs(string oldContent, string newContent)
    {
        var oldCounts = CountLines(oldContent.Split('\n'));
        var newCounts = CountLines(newContent.Split('\n'));
        var deletions = oldCounts.Sum(entry => Math.Max(0, entry.Value - newCounts.GetValueOrDefault(entry.Key, 0)));
        var additions = newCounts.Sum(entry => Math.Max(0, entry.Value - oldCounts.GetValueOrDefault(entry.Key, 0)));
        return (additions, deletions);
    }

    private static Dictionary<string, int> CountLines(IEnumerable<string> lines)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var line in lines)
        {
            counts[line] = counts.GetValueOrDefault(line, 0) + 1;
        }

        return counts;
    }

    private static (string[] Lines, bool NoNewlineAtEnd) SplitLines(string content)
    {
        if (content.Length == 0)
        {
            return ([], false);
        }

        var noNewlineAtEnd = !content.EndsWith('\n');
        var lines = content.Split('\n');
        if (!noNewlineAtEnd)
        {
            lines = lines[..^1];
        }

        return (lines, noNewlineAtEnd);
    }

    private static IReadOnlyList<DiffOp> BuildDiff(IReadOnlyList<string> oldLines, IReadOnlyList<string> newLines)
    {
        var lengths = new int[oldLines.Count + 1, newLines.Count + 1];
        for (var oldIndex = oldLines.Count - 1; oldIndex >= 0; oldIndex -= 1)
        {
            for (var newIndex = newLines.Count - 1; newIndex >= 0; newIndex -= 1)
            {
                lengths[oldIndex, newIndex] = oldLines[oldIndex] == newLines[newIndex]
                    ? lengths[oldIndex + 1, newIndex + 1] + 1
                    : Math.Max(lengths[oldIndex + 1, newIndex], lengths[oldIndex, newIndex + 1]);
            }
        }

        var diff = new List<DiffOp>();
        var oldCursor = 0;
        var newCursor = 0;
        var oldLineNumber = 1;
        var newLineNumber = 1;

        while (oldCursor < oldLines.Count && newCursor < newLines.Count)
        {
            if (oldLines[oldCursor] == newLines[newCursor])
            {
                diff.Add(new DiffOp(' ', oldLines[oldCursor], oldLineNumber, newLineNumber));
                oldCursor += 1;
                newCursor += 1;
                oldLineNumber += 1;
                newLineNumber += 1;
            }
            else if (lengths[oldCursor + 1, newCursor] >= lengths[oldCursor, newCursor + 1])
            {
                diff.Add(new DiffOp('-', oldLines[oldCursor], oldLineNumber, null));
                oldCursor += 1;
                oldLineNumber += 1;
            }
            else
            {
                diff.Add(new DiffOp('+', newLines[newCursor], null, newLineNumber));
                newCursor += 1;
                newLineNumber += 1;
            }
        }

        while (oldCursor < oldLines.Count)
        {
            diff.Add(new DiffOp('-', oldLines[oldCursor], oldLineNumber, null));
            oldCursor += 1;
            oldLineNumber += 1;
        }

        while (newCursor < newLines.Count)
        {
            diff.Add(new DiffOp('+', newLines[newCursor], null, newLineNumber));
            newCursor += 1;
            newLineNumber += 1;
        }

        return diff;
    }

    private static IReadOnlyList<DiffHunk> BuildHunks(IReadOnlyList<DiffOp> ops, int contextLines)
    {
        var changed = ops.Select((op, index) => (op, index)).Where(item => item.op.Kind != ' ').Select(item => item.index).ToArray();
        if (changed.Length == 0)
        {
            return [CreateHunk(ops, 0, ops.Count)];
        }

        var hunks = new List<DiffHunk>();
        var start = Math.Max(0, changed[0] - contextLines);
        var end = Math.Min(ops.Count - 1, changed[0] + contextLines);
        for (var index = 1; index < changed.Length; index += 1)
        {
            var change = changed[index];
            if (change <= end + contextLines)
            {
                end = Math.Min(ops.Count - 1, change + contextLines);
                continue;
            }

            hunks.Add(CreateHunk(ops, start, end + 1));
            start = Math.Max(0, change - contextLines);
            end = Math.Min(ops.Count - 1, change + contextLines);
        }

        hunks.Add(CreateHunk(ops, start, end + 1));
        return hunks;
    }

    private static DiffHunk CreateHunk(IReadOnlyList<DiffOp> ops, int startInclusive, int endExclusive)
    {
        var slice = ops.Skip(startInclusive).Take(endExclusive - startInclusive).ToArray();
        var oldNumbers = slice.Where(op => op.OldLineNumber.HasValue).Select(op => op.OldLineNumber!.Value).ToArray();
        var newNumbers = slice.Where(op => op.NewLineNumber.HasValue).Select(op => op.NewLineNumber!.Value).ToArray();
        var oldStart = oldNumbers.FirstOrDefault(1);
        var newStart = newNumbers.FirstOrDefault(1);
        var lines = slice.Select(op => new DiffLine($"{op.Kind}{op.Text}", false)).ToList();
        return new DiffHunk(oldStart, oldNumbers.Length, newStart, newNumbers.Length, lines);
    }

    private static string FormatRange(int start, int count)
    {
        return count == 1 ? start.ToString() : $"{start},{count}";
    }

    private sealed record DiffOp(char Kind, string Text, int? OldLineNumber, int? NewLineNumber);

    private sealed record DiffLine(string Text, bool NoNewlineMarker);

    private sealed record DiffHunk(int OldStart, int OldCount, int NewStart, int NewCount, IReadOnlyList<DiffLine> Lines);
}
