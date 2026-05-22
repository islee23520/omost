namespace Omodot.CommandExecutor.Tests;

public sealed class CommandResolutionTests
{
    [Fact]
    public async Task ResolveCommandsInTextAsync_replaces_embedded_commands_with_command_output()
    {
        var result = await CommandResolution.ResolveCommandsInTextAsync("Value: !`printf resolved`");

        Assert.Equal("Value: resolved", result);
    }

    [Fact]
    public async Task ResolveCommandsInTextAsync_returns_input_unchanged_without_embedded_commands()
    {
        Assert.Equal("plain text only", await CommandResolution.ResolveCommandsInTextAsync("plain text only"));
    }

    [Fact]
    public async Task ResolveCommandsInTextAsync_resolves_nested_commands_until_max_depth()
    {
        const string text = "Outer !`printf \"\\041\\140printf nested\\140\"`";

        var fullyResolved = await CommandResolution.ResolveCommandsInTextAsync(text);
        var depthLimited = await CommandResolution.ResolveCommandsInTextAsync(text, maxDepth: 1);

        Assert.Equal("Outer nested", fullyResolved);
        Assert.Equal("Outer !`printf nested`", depthLimited);
    }
}
