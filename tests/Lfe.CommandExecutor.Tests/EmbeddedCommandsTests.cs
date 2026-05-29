namespace Lfe.CommandExecutor.Tests;

public sealed class EmbeddedCommandsTests
{
    [Fact]
    public void FindEmbeddedCommands_returns_all_matches_with_ranges()
    {
        var text = "before !`echo one` middle !`printf two` after";

        Assert.Equal(
            [
                new CommandMatch("!`echo one`", "echo one", 7, 18),
                new CommandMatch("!`printf two`", "printf two", 26, 39),
            ],
            EmbeddedCommands.FindEmbeddedCommands(text));
    }

    [Fact]
    public void FindEmbeddedCommands_returns_empty_when_no_matches_exist()
    {
        Assert.Empty(EmbeddedCommands.FindEmbeddedCommands("plain text"));
    }
}
