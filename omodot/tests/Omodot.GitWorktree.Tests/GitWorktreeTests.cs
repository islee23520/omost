using Omodot.GitWorktree;
using Xunit;

namespace Omodot.GitWorktree.Tests;

public class GitWorktreeTests
{
    [Fact]
    public void ParseStatusPorcelainLine_Modified() =>
        Assert.Equal(new ParsedGitStatusPorcelainLine("src/a.ts", GitFileStatus.Modified), ParseStatusPorcelainLine.Parse(" M src/a.ts"));

    [Fact]
    public void ParseStatusPorcelainLine_Added() =>
        Assert.Equal(new ParsedGitStatusPorcelainLine("src/b.ts", GitFileStatus.Added), ParseStatusPorcelainLine.Parse("A  src/b.ts"));

    [Fact]
    public void ParseStatusPorcelainLine_Untracked() =>
        Assert.Equal(new ParsedGitStatusPorcelainLine("src/c.ts", GitFileStatus.Added), ParseStatusPorcelainLine.Parse("?? src/c.ts"));

    [Fact]
    public void ParseStatusPorcelainLine_Deleted() =>
        Assert.Equal(new ParsedGitStatusPorcelainLine("src/d.ts", GitFileStatus.Deleted), ParseStatusPorcelainLine.Parse("D  src/d.ts"));

    [Fact]
    public void ParseStatusPorcelainLine_Empty() => Assert.Null(ParseStatusPorcelainLine.Parse(""));

    [Fact]
    public void ParseStatusPorcelainLine_NoPath() => Assert.Null(ParseStatusPorcelainLine.Parse(" M "));

    [Fact]
    public void ParseStatusPorcelain_EmptyOutput() => Assert.Empty(ParseStatusPorcelain.Parse(""));

    [Fact]
    public void ParseStatusPorcelain_MapsPaths()
    {
        var map = ParseStatusPorcelain.Parse(" M src/a.ts\nA  src/b.ts\n?? src/c.ts\nD  src/d.ts");
        Assert.Equal(GitFileStatus.Modified, map["src/a.ts"]);
        Assert.Equal(GitFileStatus.Added, map["src/b.ts"]);
        Assert.Equal(GitFileStatus.Added, map["src/c.ts"]);
        Assert.Equal(GitFileStatus.Deleted, map["src/d.ts"]);
    }

    [Fact]
    public void ParseDiffNumstat_Empty() => Assert.Empty(ParseDiffNumstat.Parse("", new Dictionary<string, GitFileStatus>()));

    [Fact]
    public void ParseDiffNumstat_InvalidLine() => Assert.Empty(ParseDiffNumstat.Parse("not-enough-parts", new Dictionary<string, GitFileStatus>()));

    [Fact]
    public void ParseDiffNumstat_ParsesStats()
    {
        var statusMap = new Dictionary<string, GitFileStatus> { ["src/a.ts"] = GitFileStatus.Modified, ["src/b.ts"] = GitFileStatus.Added };
        var stats = ParseDiffNumstat.Parse("1\t2\tsrc/a.ts\n3\t0\tsrc/b.ts\n-\t-\tbin.dat", statusMap);
        Assert.Equal(3, stats.Count);
        Assert.Equal(new GitFileStat("src/a.ts", 1, 2, GitFileStatus.Modified), stats[0]);
        Assert.Equal(new GitFileStat("src/b.ts", 3, 0, GitFileStatus.Added), stats[1]);
        Assert.Equal(new GitFileStat("bin.dat", 0, 0, GitFileStatus.Modified), stats[2]);
    }

    [Fact]
    public void FormatFileChanges_NoChanges() => Assert.Equal("[FILE CHANGES SUMMARY]\nNo file changes detected.\n", FormatFileChanges.Format([]));

    [Fact]
    public void FormatFileChanges_WithChanges()
    {
        var summary = FormatFileChanges.Format([
            new("src/a.ts", 1, 2, GitFileStatus.Modified),
            new("src/b.ts", 3, 0, GitFileStatus.Added),
            new("src/c.ts", 0, 4, GitFileStatus.Deleted)
        ]);
        Assert.Contains("[FILE CHANGES SUMMARY]", summary);
        Assert.Contains("Modified files:", summary);
        Assert.Contains("Created files:", summary);
        Assert.Contains("Deleted files:", summary);
        Assert.Contains("src/a.ts  (+1, -2)", summary);
        Assert.Contains("src/b.ts  (+3)", summary);
        Assert.Contains("src/c.ts  (-4)", summary);
    }

    [Fact]
    public void FormatFileChanges_NotepadMatch()
    {
        var summary = FormatFileChanges.Format([new(".omo/notepads/work/notes.md", 1, 0, GitFileStatus.Modified)], ".omo/notepads/work/notes.md");
        Assert.Contains("[NOTEPAD UPDATED]", summary);
    }

    [Fact]
    public void FormatFileChanges_NotepadNoMatch()
    {
        var summary = FormatFileChanges.Format([new(".omo/plans/work.md", 1, 0, GitFileStatus.Modified)], ".omo/notepads/work/notes.md");
        Assert.DoesNotContain("[NOTEPAD UPDATED]", summary);
    }
}
