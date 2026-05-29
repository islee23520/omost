using System.Text.Json.Nodes;

namespace Lfe.Utils.Tests;

public sealed class SnakeCaseTests
{
    [Fact]
    public void CamelToSnake_converts_uppercase_boundaries()
    {
        Assert.Equal("simple_test_value", SnakeCase.CamelToSnake("simpleTestValue"));
    }

    [Fact]
    public void SnakeToCamel_converts_segments()
    {
        Assert.Equal("simpleTestValue", SnakeCase.SnakeToCamel("simple_test_value"));
    }

    [Fact]
    public void TransformObjectKeys_converts_nested_objects()
    {
        JsonObject value = new()
        {
            ["outerKey"] = new JsonObject { ["innerValue"] = 1 },
        };

        var result = SnakeCase.TransformObjectKeys(value, SnakeCase.CamelToSnake);

        Assert.NotNull(result["outer_key"]);
        Assert.Equal(1, result["outer_key"]!["inner_value"]!.GetValue<int>());
    }

    [Fact]
    public void ObjectToSnakeCase_converts_root_keys()
    {
        JsonObject value = new() { ["userName"] = "alice" };
        var result = SnakeCase.ObjectToSnakeCase(value);
        Assert.Equal("alice", result["user_name"]!.GetValue<string>());
    }

    [Fact]
    public void ObjectToCamelCase_converts_root_keys()
    {
        JsonObject value = new() { ["user_name"] = "alice" };
        var result = SnakeCase.ObjectToCamelCase(value);
        Assert.Equal("alice", result["userName"]!.GetValue<string>());
    }
}
