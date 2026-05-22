using Omodot.SlashCommand;
using Xunit;

namespace Omodot.SlashCommand.Tests;

public class DiscoveryTests
{
    [Fact]
    public void DiscoverSlashCommandsSync_NoDirectory_ReturnsEmpty()
    {
        var commands = Discovery.DiscoverSlashCommandsSync(new DiscoverSlashCommandsOptions(Directory: "/nonexistent"));
        Assert.Empty(commands);
    }

    [Fact]
    public void ToHookSlashCommandInfo_MapsCorrectly()
    {
        var cmd = new SlashCommandInfo("build", "/path", new CommandMetadata("build", "Build project", Model: "gpt-5"), "content", CommandScope.Project);
        var hook = Discovery.ToHookSlashCommandInfo(cmd);
        Assert.Equal("build", hook.Name);
        Assert.Equal("project", hook.Scope);
        Assert.Equal("Build project", hook.Description);
        Assert.Equal("gpt-5", hook.Model);
    }

    [Fact]
    public void ToHookSlashCommandInfos_MapsAll()
    {
        var cmds = new[] { new SlashCommandInfo("a", null, new CommandMetadata("a"), null, CommandScope.User) };
        var hooks = Discovery.ToHookSlashCommandInfos(cmds);
        Assert.Single(hooks);
        Assert.Equal("user", hooks[0].Scope);
    }

    [Fact]
    public void DiscoverSlashCommandsSync_ReadsProjectCommands()
    {
        var root = Path.Combine(Path.GetTempPath(), $"slash-test-{Guid.NewGuid():N}");
        var cmdsDir = Path.Join(root, ".claude", "commands");
        Directory.CreateDirectory(cmdsDir);
        try
        {
            File.WriteAllText(Path.Join(cmdsDir, "build.md"), "---\ndescription: Build project\nmodel: openai/gpt-5.4\n---\nRun build");
            File.WriteAllText(Path.Join(cmdsDir, ".hidden.md"), "hidden");
            File.WriteAllText(Path.Join(cmdsDir, "readme.txt"), "ignore");

            var commands = Discovery.DiscoverSlashCommandsSync(new DiscoverSlashCommandsOptions(Directory: root));
            Assert.Single(commands);
            Assert.Equal("build", commands[0].Name);
            Assert.Equal("Build project", commands[0].Metadata.Description);
            Assert.Equal("openai/gpt-5.4", commands[0].Metadata.Model);
            Assert.Equal(CommandScope.Project, commands[0].Scope);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void DiscoverSlashCommandsSync_PlainMarkdown_NoFrontmatter()
    {
        var root = Path.Combine(Path.GetTempPath(), $"slash-plain-{Guid.NewGuid():N}");
        var cmdsDir = Path.Join(root, ".claude", "commands");
        Directory.CreateDirectory(cmdsDir);
        try
        {
            File.WriteAllText(Path.Join(cmdsDir, "plain.md"), "plain body");
            var commands = Discovery.DiscoverSlashCommandsSync(new DiscoverSlashCommandsOptions(Directory: root));
            Assert.Single(commands);
            Assert.Equal("", commands[0].Metadata.Description);
            Assert.Equal("plain body", commands[0].Content);
        }
        finally { Directory.Delete(root, true); }
    }
}
