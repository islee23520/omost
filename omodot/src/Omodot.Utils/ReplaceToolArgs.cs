using System.Text.Json.Nodes;

namespace Omodot.Utils;

public sealed class ToolArgsContainer
{
    public ToolArgsContainer(JsonObject args)
    {
        Args = args;
    }

    public JsonObject Args { get; set; }
}

public static class ReplaceToolArgs
{
    public static void Apply(ToolArgsContainer output, JsonObject patch)
    {
        var merged = new JsonObject();

        foreach (var pair in output.Args)
        {
            merged[pair.Key] = JsonNodeHelpers.Clone(pair.Value);
        }

        foreach (var pair in patch)
        {
            merged[pair.Key] = JsonNodeHelpers.Clone(pair.Value);
        }

        output.Args = merged;
    }

    public static ToolArgsContainer Replace(ToolArgsContainer output, JsonObject patch)
    {
        var merged = new JsonObject();

        foreach (var pair in output.Args)
        {
            merged[pair.Key] = JsonNodeHelpers.Clone(pair.Value);
        }

        foreach (var pair in patch)
        {
            merged[pair.Key] = JsonNodeHelpers.Clone(pair.Value);
        }

        return new ToolArgsContainer(merged);
    }
}
