namespace Lfe.Utils.Tests;

public sealed class JsoncParserTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), $"lfe-utils-jsonc-parser-{Guid.NewGuid():N}");

    public JsoncParserTests()
    {
        Directory.CreateDirectory(_tempDirectory);
        JsoncParser.ClearPluginConfigFileDetectionCache();
    }

    [Fact]
    public void ParseJsonc_parses_content()
    {
        var result = JsoncParser.ParseJsonc<JsoncDocument>("{\n // comment\n \"key\": \"value\"\n}");
        Assert.Equal("value", result.Key);
    }

    [Fact]
    public void ParseJsoncSafe_handles_invalid_content()
    {
        var result = JsoncParser.ParseJsoncSafe<JsoncDocument>("{ invalid }");
        Assert.Null(result.Data);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void ReadJsoncFile_reads_content()
    {
        var filePath = Path.Combine(_tempDirectory, "config.jsonc");
        File.WriteAllText(filePath, "{\n \"key\": \"value\"\n}");
        var result = JsoncParser.ReadJsoncFile<JsoncDocument>(filePath);
        Assert.Equal("value", result!.Key);
    }

    [Fact]
    public void DetectConfigFile_uses_wrapper()
    {
        var basePath = Path.Combine(_tempDirectory, "config");
        File.WriteAllText(basePath + ".jsonc", "{}");
        var result = JsoncParser.DetectConfigFile(basePath);
        Assert.Equal("jsonc", result.Format);
    }

    [Fact]
    public void ClearPluginConfigFileDetectionCache_clears_wrapper_cache()
    {
        File.WriteAllText(Path.Combine(_tempDirectory, "oh-my-openagent.jsonc"), "{}");
        var options = new DetectPluginConfigFileOptions(["oh-my-openagent"], ["oh-my-opencode"]);
        JsoncParser.DetectPluginConfigFile(_tempDirectory, options);
        JsoncParser.ClearPluginConfigFileDetectionCache();
        var result = JsoncParser.DetectPluginConfigFile(_tempDirectory, options);
        Assert.Equal("jsonc", result.Format);
    }

    [Fact]
    public void DetectPluginConfigFile_detects_canonical_file()
    {
        File.WriteAllText(Path.Combine(_tempDirectory, "oh-my-openagent.jsonc"), "{}");
        var options = new DetectPluginConfigFileOptions(["oh-my-openagent"], ["oh-my-opencode"]);
        var result = JsoncParser.DetectPluginConfigFile(_tempDirectory, options);
        Assert.Equal("jsonc", result.Format);
    }

    public void Dispose()
    {
        JsoncParser.ClearPluginConfigFileDetectionCache();
        Directory.Delete(_tempDirectory, true);
    }

    private sealed record JsoncDocument(string Key);
}
