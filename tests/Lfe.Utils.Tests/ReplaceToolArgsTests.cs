using System.Text.Json.Nodes;

namespace Lfe.Utils.Tests;

public sealed class ReplaceToolArgsTests
{
    [Fact]
    public void Apply_merges_patch_into_existing_args()
    {
        var output = new ToolArgsContainer(new JsonObject { ["command"] = "git status", ["timeout"] = 30 });
        ReplaceToolArgs.Apply(output, new JsonObject { ["command"] = "git log" });
        Assert.Equal("git log", output.Args["command"]!.GetValue<string>());
        Assert.Equal(30, output.Args["timeout"]!.GetValue<int>());
    }

    [Fact]
    public void Replace_returns_new_container()
    {
        var output = new ToolArgsContainer(new JsonObject { ["command"] = "git status" });
        var replaced = ReplaceToolArgs.Replace(output, new JsonObject { ["command"] = "git log" });
        Assert.Equal("git status", output.Args["command"]!.GetValue<string>());
        Assert.Equal("git log", replaced.Args["command"]!.GetValue<string>());
    }
}
