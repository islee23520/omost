using System.Text.Json.Nodes;

namespace Lfe.Utils.Tests;

public sealed class DeepMergeTests
{
    [Fact]
    public void IsPlainObject_detects_json_object()
    {
        Assert.True(DeepMerge.IsPlainObject(new JsonObject()));
    }

    [Fact]
    public void Merge_recursively_merges_objects()
    {
        JsonObject baseValue = new() { ["a"] = 1, ["nested"] = new JsonObject { ["x"] = 1 } };
        JsonObject overrideValue = new() { ["nested"] = new JsonObject { ["y"] = 2 } };

        var result = DeepMerge.Merge(baseValue, overrideValue);

        Assert.NotNull(result);
        Assert.Equal(1, result!["a"]!.GetValue<int>());
        Assert.Equal(1, result["nested"]!["x"]!.GetValue<int>());
        Assert.Equal(2, result["nested"]!["y"]!.GetValue<int>());
    }
}
