using Omodot.HashLine;

namespace Omodot.HashLine.Tests;

public sealed class EditOperationsTests
{
    [Fact]
    public void ApplySetLineUsesHashAnchors()
    {
        var lines = new[] { "line 1", "line 2", "line 3" };
        var result = EditOperationPrimitives.ApplySetLine(lines, AnchorFor(lines, 2), "new line 2");
        Assert.Equal(["line 1", "new line 2", "line 3"], result);
    }

    [Fact]
    public void ApplyReplaceLinesStripsBoundaryEcho()
    {
        var lines = new[] { "before", "old 1", "old 2", "after" };
        var result = EditOperationPrimitives.ApplyReplaceLines(lines, AnchorFor(lines, 2), AnchorFor(lines, 3), new[] { "before", "new 1", "new 2", "after" });
        Assert.Equal(["before", "new 1", "new 2", "after"], result);
    }

    [Fact]
    public void ApplyHashlineEditsHandlesMixedOperations()
    {
        const string content = "line 1\nline 2\nline 3";
        var lines = content.Split('\n');
        var edits = new HashlineEdit[]
        {
            new AppendEdit { Pos = AnchorFor(lines, 1), Lines = "inserted" },
            new ReplaceEdit { Pos = AnchorFor(lines, 3), Lines = "modified" },
        };

        var result = EditOperations.ApplyHashlineEdits(content, edits);
        Assert.Equal("line 1\ninserted\nline 2\nmodified", result);
    }

    [Fact]
    public void ApplyHashlineEditsReportsNoopsAndDeduplication()
    {
        const string content = "line 1\nline 2";
        var lines = content.Split('\n');
        var edit = new AppendEdit { Pos = AnchorFor(lines, 1), Lines = new[] { "inserted" } };
        var report = EditOperations.ApplyHashlineEditsWithReport(content, [edit, edit]);
        Assert.Equal(1, report.DeduplicatedEdits);
        Assert.Equal("line 1\ninserted\nline 2", report.Content);
    }

    [Fact]
    public void ApplyHashlineEditsRejectsOverlappingRanges()
    {
        const string content = "line 1\nline 2\nline 3\nline 4\nline 5";
        var lines = content.Split('\n');
        var edits = new HashlineEdit[]
        {
            new ReplaceEdit { Pos = AnchorFor(lines, 1), End = AnchorFor(lines, 3), Lines = "replaced A" },
            new ReplaceEdit { Pos = AnchorFor(lines, 2), End = AnchorFor(lines, 4), Lines = "replaced B" },
        };

        var error = Assert.Throws<ArgumentException>(() => EditOperations.ApplyHashlineEdits(content, edits));
        Assert.Contains("Overlapping range edits", error.Message, StringComparison.Ordinal);
    }

    private static string AnchorFor(IReadOnlyList<string> lines, int line)
    {
        return $"{line}#{HashComputation.ComputeLineHash(line, lines[line - 1])}";
    }
}
