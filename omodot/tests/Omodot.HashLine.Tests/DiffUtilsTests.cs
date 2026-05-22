using Omodot.HashLine;

namespace Omodot.HashLine.Tests;

public sealed class DiffUtilsTests
{
    [Fact]
    public void ToHashlineContentPreservesTrailingNewline()
    {
        var result = DiffUtils.ToHashlineContent("alpha\nbeta\n");
        Assert.EndsWith("\n", result, StringComparison.Ordinal);
        Assert.Contains("1#", result, StringComparison.Ordinal);
        Assert.Contains("2#", result, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateUnifiedDiffIncludesHeadersAndContext()
    {
        var oldContent = string.Join("\n", Enumerable.Range(1, 20).Select(index => $"line {index}"));
        var newLines = oldContent.Split('\n');
        newLines[9] = "line 10 updated";
        var diff = DiffUtils.GenerateUnifiedDiff(oldContent, string.Join("\n", newLines), "sample.txt");

        Assert.Contains("Index: sample.txt", diff, StringComparison.Ordinal);
        Assert.Contains("--- sample.txt", diff, StringComparison.Ordinal);
        Assert.Contains("+++ sample.txt", diff, StringComparison.Ordinal);
        Assert.Contains(" line 7", diff, StringComparison.Ordinal);
        Assert.Contains(" line 13", diff, StringComparison.Ordinal);
        Assert.DoesNotContain(" line 6", diff, StringComparison.Ordinal);
        Assert.DoesNotContain(" line 14", diff, StringComparison.Ordinal);
    }

    [Fact]
    public void CountLineDiffsTracksDuplicates()
    {
        var counts = DiffUtils.CountLineDiffs("a\na\nb", "a\nb\nb\nc");
        Assert.Equal((2, 1), counts);
    }

    [Fact]
    public void GenerateHashlineDiffShowsAdditionsAndDeletions()
    {
        var diff = HashlineEditDiff.GenerateHashlineDiff("alpha\nbeta", "alpha\ngamma\ndelta", "sample.txt");
        Assert.Contains("+++ sample.txt", diff, StringComparison.Ordinal);
        Assert.Contains("- 2#  |beta", diff, StringComparison.Ordinal);
        Assert.Contains("+ 2#", diff, StringComparison.Ordinal);
        Assert.Contains("+ 3#", diff, StringComparison.Ordinal);
    }
}
