namespace Omodot.Utils.Tests;

public sealed class JsoncParserTypesTests
{
    [Fact]
    public void Type_records_roundtrip_values()
    {
        var error = new JsoncParseError("boom", 1, 2);
        var result = new JsoncParseResult<string>("ok", [error]);
        var detect = new DetectPluginConfigResult("jsonc", "/tmp/config.jsonc", "/tmp/legacy.jsonc");
        var options = new DetectPluginConfigFileOptions(["oh-my-openagent"], ["oh-my-opencode"]);

        Assert.Equal("ok", result.Data);
        Assert.Single(result.Errors);
        Assert.Equal("jsonc", detect.Format);
        Assert.Equal("oh-my-openagent", options.Basenames[0]);
    }
}
