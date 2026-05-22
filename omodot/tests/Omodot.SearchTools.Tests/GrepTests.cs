using Omodot.SearchTools;

namespace Omodot.SearchTools.Tests;

public sealed class GrepTests
{
    [Fact]
    public void BuildRipgrepArgsAddsExpectedFlags()
    {
        var args = Grep.BuildRipgrepArgs(new GrepOptions
        {
            Context = 20,
            CaseSensitive = true,
            WholeWord = true,
            FixedStrings = true,
            Multiline = true,
            Hidden = true,
            NoIgnore = true,
            FileType = new[] { "cs" },
            Globs = new[] { "*.cs" },
            ExcludeGlobs = new[] { "obj/**" },
            OutputMode = GrepOutputMode.FilesWithMatches,
        });

        Assert.Contains("-C10", args);
        Assert.Contains("--case-sensitive", args);
        Assert.Contains("-w", args);
        Assert.Contains("-F", args);
        Assert.Contains("-U", args);
        Assert.Contains("--hidden", args);
        Assert.Contains("--no-ignore", args);
        Assert.Contains("--type=cs", args);
        Assert.Contains("--glob=*.cs", args);
        Assert.Contains("--glob=!obj/**", args);
        Assert.Contains("--files-with-matches", args);
    }

    [Fact]
    public void BuildClassicGrepArgsMatchesFallbackShape()
    {
        var args = Grep.BuildClassicGrepArgs(new GrepOptions
        {
            CaseSensitive = false,
            WholeWord = true,
            FixedStrings = true,
            Globs = new[] { "*.cs" },
            ExcludeGlobs = new[] { "*.g.cs" },
        });

        Assert.Contains("-r", args);
        Assert.Contains("-i", args);
        Assert.Contains("-w", args);
        Assert.Contains("-F", args);
        Assert.Contains("--include=*.cs", args);
        Assert.Contains("--exclude=*.g.cs", args);
        Assert.Contains("--exclude-dir=.git", args);
    }

    [Fact]
    public void ParseOutputSupportsStandardAndWindowsPaths()
    {
        var matches = Grep.ParseOutput("src/a.cs:12: hello\nC:\\repo\\b.cs:4: world\n");

        Assert.Equal(2, matches.Count);
        Assert.Equal("src/a.cs", matches[0].File);
        Assert.Equal(12, matches[0].Line);
        Assert.Equal(" hello", matches[0].Text);
        Assert.Equal("C:\\repo\\b.cs", matches[1].File);
    }

    [Fact]
    public void ParseCountOutputSupportsStandardAndWindowsPaths()
    {
        var counts = Grep.ParseCountOutput("src/a.cs:2\nC:\\repo\\b.cs:8\n");

        Assert.Equal(2, counts.Count);
        Assert.Equal(new CountResult("src/a.cs", 2), counts[0]);
        Assert.Equal(new CountResult("C:\\repo\\b.cs", 8), counts[1]);
    }

    [Fact]
    public void FormattersMatchTypeScriptOutputStyle()
    {
        var grepText = Grep.FormatGrepResult(new GrepResult
        {
            Matches = new[]
            {
                new GrepMatch("a.cs", 1, " foo "),
                new GrepMatch("a.cs", 3, " bar "),
                new GrepMatch("b.cs", 0, string.Empty),
            },
            TotalMatches = 3,
            FilesSearched = 2,
            Truncated = true,
        });

        var countText = Grep.FormatCountResult(new[]
        {
            new CountResult("a.cs", 1),
            new CountResult("b.cs", 9),
        });

        Assert.Contains("Found 3 match(es) in 2 file(s)", grepText, StringComparison.Ordinal);
        Assert.Contains("[Output truncated due to size limit]", grepText, StringComparison.Ordinal);
        Assert.Contains("a.cs", grepText, StringComparison.Ordinal);
        Assert.Contains("  1: foo", grepText, StringComparison.Ordinal);
        Assert.Contains("Found 10 match(es) in 2 file(s):", countText, StringComparison.Ordinal);
        Assert.Contains("       9: b.cs", countText, StringComparison.Ordinal);
    }
}
