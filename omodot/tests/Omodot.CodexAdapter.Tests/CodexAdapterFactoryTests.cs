using Xunit;
using Omodot.UlwHostContract;

namespace Omodot.CodexAdapter.Tests;

public sealed class CodexAdapterFactoryTests
{
    private static string CreateScript(string name, string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{name}-{Guid.NewGuid():N}.sh");
        File.WriteAllText(path, content);
        System.Diagnostics.Process.Start("chmod", $"+x {path}")!.WaitForExit();
        return path;
    }

    [Fact]
    public void Create_ReturnsRuntimeWithHost()
    {
        var scriptPath = CreateScript("factory-ok", "#!/bin/sh\necho ok\n");
        try
        {
            var options = new CodexAdapterOptions { CodexBinaryPath = scriptPath };
            using var runtime = CodexAdapterFactory.Create(options);
            Assert.NotNull(runtime.Host);
            Assert.NotNull(runtime.ResolvedConfig);
            Assert.Equal(scriptPath, runtime.ResolvedConfig.BinaryPath);
        }
        finally { File.Delete(scriptPath); }
    }

    [Fact]
    public void Create_ThrowsOnNullOptions()
    {
        Assert.Throws<ArgumentNullException>(() => CodexAdapterFactory.Create(null!));
    }

    [Fact]
    public async Task Create_HostIsFunctional()
    {
        var scriptPath = CreateScript("factory-fn", "#!/bin/sh\necho '{\"type\":\"completed\",\"session_id\":\"s1\"}'\n");
        try
        {
            var options = new CodexAdapterOptions { CodexBinaryPath = scriptPath };
            using var runtime = CodexAdapterFactory.Create(options);
            var receipt = await runtime.Host.DispatchPromptAsync(new UlwPromptRequest("s1", "test"));
            Assert.True(receipt.Accepted);
        }
        finally { File.Delete(scriptPath); }
    }

    [Fact]
    public async Task CreateWithOverride_AppliesConfigChanges()
    {
        var scriptPath = CreateScript("factory-override", "#!/bin/sh\necho '{\"type\":\"completed\",\"session_id\":\"s1\"}'\n");
        try
        {
            var options = new CodexAdapterOptions { CodexBinaryPath = scriptPath };
            using var runtime = CodexAdapterFactory.CreateWithOverride(options, c => c with { TimeoutMs = 10_000 });
            Assert.Equal(10_000, runtime.ResolvedConfig.TimeoutMs);
            var receipt = await runtime.Host.DispatchPromptAsync(new UlwPromptRequest("s1", "test"));
            Assert.True(receipt.Accepted);
        }
        finally { File.Delete(scriptPath); }
    }
}
