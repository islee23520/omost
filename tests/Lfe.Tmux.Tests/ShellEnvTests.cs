namespace Lfe.Tmux.Tests;

public sealed class ShellEnvTests
{
    [Fact]
    public void EscapesShellMetacharacters()
    {
        var escaped = ShellEnv.ShellEscapeForDoubleQuotedCommand("http://localhost:3000$(whoami);rm -rf / `x` \"q\"");

        Assert.Contains("\\$", escaped, StringComparison.Ordinal);
        Assert.Contains("\\;", escaped, StringComparison.Ordinal);
        Assert.Contains("\\`", escaped, StringComparison.Ordinal);
        Assert.Contains("\\\"", escaped, StringComparison.Ordinal);
    }

    [Fact]
    public void ParsesNullSeparatedShellEnvironmentOutput()
    {
        var parsed = ShellEnv.ParseEnvironmentVariables("FOO=bar\0BAR=baz=qux\0", nullSeparated: true);

        Assert.Equal("bar", parsed["FOO"]);
        Assert.Equal("baz=qux", parsed["BAR"]);
    }
}
