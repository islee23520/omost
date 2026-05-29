using Lfe.SearchTools;

namespace Lfe.SearchTools.Tests;

public sealed class GlobTests
{
    [Fact]
    public void BuildGlobRgArgsMatchesTypeScriptBehavior()
    {
        var args = Glob.BuildGlobRgArgs(new GlobOptions
        {
            Pattern = "*.cs",
            Hidden = true,
            Follow = true,
            NoIgnore = true,
            Threads = 10,
            MaxDepth = 99,
        });

        Assert.Equal("--threads=4", args[4]);
        Assert.Equal("--max-depth=20", args[5]);
        Assert.Contains("--hidden", args);
        Assert.Contains("--follow", args);
        Assert.Contains("--no-ignore", args);
        Assert.Contains("--glob=*.cs", args);
    }

    [Fact]
    public void BuildFindArgsRespectsHiddenFlag()
    {
        var args = Glob.BuildFindArgs(new GlobOptions { Pattern = "*.cs", Hidden = false, Follow = false, MaxDepth = 2 });

        Assert.DoesNotContain("-L", args);
        Assert.Contains("*/.*", args);
        Assert.Contains("2", args);
    }

    [Fact]
    public void BuildPowerShellCommandEscapesSingleQuotes()
    {
        var command = Glob.BuildPowerShellCommand(new GlobOptions
        {
            Pattern = "*o'modot*.cs",
            Paths = new[] { "C:/te'st" },
            Hidden = true,
        });

        Assert.Equal("powershell.exe", command[0]);
        Assert.Contains("C:/te''st", command[3], StringComparison.Ordinal);
        Assert.Contains("*o''modot*.cs", command[3], StringComparison.Ordinal);
        Assert.Contains("-Force", command[3], StringComparison.Ordinal);
    }

    [Fact]
    public void FormatGlobResultShowsTruncationNotice()
    {
        var text = Glob.FormatGlobResult(new GlobResult
        {
            Files = new[] { new FileMatch("/tmp/a.cs", 1) },
            TotalFiles = 1,
            Truncated = true,
        });

        Assert.Contains("Found 1 file(s)", text, StringComparison.Ordinal);
        Assert.Contains("/tmp/a.cs", text, StringComparison.Ordinal);
        Assert.Contains("Results are truncated", text, StringComparison.Ordinal);
    }
}
