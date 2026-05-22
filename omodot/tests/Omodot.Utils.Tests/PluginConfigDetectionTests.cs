namespace Omodot.Utils.Tests;

public sealed class PluginConfigDetectionTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), $"omodot-utils-plugin-{Guid.NewGuid():N}");

    public PluginConfigDetectionTests()
    {
        Directory.CreateDirectory(_tempDirectory);
        PluginConfigDetection.ClearCache();
    }

    [Fact]
    public void DetectConfigFile_prefers_jsonc()
    {
        var basePath = Path.Combine(_tempDirectory, "config");
        File.WriteAllText(basePath + ".json", "{}");
        File.WriteAllText(basePath + ".jsonc", "{}");

        var result = PluginConfigDetection.DetectConfigFile(basePath);
        Assert.Equal("jsonc", result.Format);
    }

    [Fact]
    public void DetectPluginConfigFile_returns_cached_result()
    {
        File.WriteAllText(Path.Combine(_tempDirectory, "oh-my-openagent.jsonc"), "{}");
        var options = new DetectPluginConfigFileOptions(["oh-my-openagent"], ["oh-my-opencode"]);

        var first = PluginConfigDetection.DetectPluginConfigFile(_tempDirectory, options);
        var second = PluginConfigDetection.DetectPluginConfigFile(_tempDirectory, options);

        Assert.Equal(first, second);
    }

    [Fact]
    public void ClearCache_removes_memoized_results()
    {
        File.WriteAllText(Path.Combine(_tempDirectory, "oh-my-openagent.jsonc"), "{}");
        var options = new DetectPluginConfigFileOptions(["oh-my-openagent"], ["oh-my-opencode"]);
        PluginConfigDetection.DetectPluginConfigFile(_tempDirectory, options);
        PluginConfigDetection.ClearCache();
        var result = PluginConfigDetection.DetectPluginConfigFile(_tempDirectory, options);
        Assert.Equal("jsonc", result.Format);
    }

    public void Dispose()
    {
        PluginConfigDetection.ClearCache();
        Directory.Delete(_tempDirectory, true);
    }
}
