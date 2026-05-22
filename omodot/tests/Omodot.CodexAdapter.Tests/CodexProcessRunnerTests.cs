using System.Diagnostics;
using Xunit;

namespace Omodot.CodexAdapter.Tests;

public sealed class CodexProcessRunnerTests : IDisposable
{
    private readonly CodexResolvedConfig _config;
    private readonly CodexProcessRunner _runner;

    public CodexProcessRunnerTests()
    {
        _config = new CodexResolvedConfig(
            BinaryPath: "/usr/bin/echo",
            WorkingDirectory: Path.GetTempPath(),
            TimeoutMs: 5000,
            EnvironmentOverrides: new Dictionary<string, string>(),
            SessionOptions: new CodexSessionOptions());
        _runner = new CodexProcessRunner(_config);
    }

    [Fact]
    public void BuildStartInfo_ContainsExecAndExperimentalJson()
    {
        var si = _runner.BuildStartInfo("hello world");
        Assert.Contains(CodexTransportConstants.ExecArg, si.ArgumentList);
        Assert.Contains(CodexTransportConstants.ExperimentalJsonFlag, si.ArgumentList);
    }

    [Fact]
    public void BuildStartInfo_ContainsPromptAfterDoubleDash()
    {
        var si = _runner.BuildStartInfo("test prompt");
        var args = si.ArgumentList;
        var dashIndex = args.IndexOf("--");
        Assert.True(dashIndex >= 0, "Missing '--' separator");
        Assert.Equal("test prompt", args[dashIndex + 1]);
    }

    [Fact]
    public void BuildStartInfo_IncludesModel_WhenSet()
    {
        var config = _config with
        {
            SessionOptions = new CodexSessionOptions { ModelId = "gpt-5" }
        };
        var runner = new CodexProcessRunner(config);
        var si = runner.BuildStartInfo("hi");
        var args = si.ArgumentList;
        var modelIndex = args.IndexOf("--model");
        Assert.True(modelIndex >= 0, "Missing '--model' flag");
        Assert.Equal("gpt-5", args[modelIndex + 1]);
    }

    [Fact]
    public void BuildStartInfo_IncludesAgent_WhenSet()
    {
        var config = _config with
        {
            SessionOptions = new CodexSessionOptions { AgentName = "builder" }
        };
        var runner = new CodexProcessRunner(config);
        var si = runner.BuildStartInfo("hi");
        var args = si.ArgumentList;
        var agentIndex = args.IndexOf("--agent");
        Assert.True(agentIndex >= 0, "Missing '--agent' flag");
        Assert.Equal("builder", args[agentIndex + 1]);
    }

    [Fact]
    public void BuildStartInfo_SetsWorkingDirectory()
    {
        var si = _runner.BuildStartInfo("hi");
        Assert.Equal(_config.WorkingDirectory, si.WorkingDirectory);
    }

    [Fact]
    public void BuildStartInfo_AppliesEnvironmentOverrides()
    {
        var config = _config with
        {
            EnvironmentOverrides = new Dictionary<string, string> { ["MY_VAR"] = "my_value" }
        };
        var runner = new CodexProcessRunner(config);
        var si = runner.BuildStartInfo("hi");
        Assert.Equal("my_value", si.Environment["MY_VAR"]);
    }

    [Fact]
    public void BuildStartInfo_RedirectsStdoutAndStderr()
    {
        var si = _runner.BuildStartInfo("hi");
        Assert.True(si.RedirectStandardOutput);
        Assert.True(si.RedirectStandardError);
        Assert.False(si.UseShellExecute);
    }

    [Fact]
    public void BuildStartInfo_ThrowsOnEmptyPrompt()
    {
        Assert.Throws<ArgumentException>(() => _runner.BuildStartInfo(""));
    }

    [Fact]
    public async Task ExecuteAsync_ParsesJsonlFromRealProcess()
    {
        var scriptPath = Path.Combine(Path.GetTempPath(), $"codex-test-{Guid.NewGuid():N}.sh");
        var jsonl = """
            {"type":"message","role":"assistant","content":"hi"}
            {"type":"completed","session_id":"s1"}
            """;
        await File.WriteAllTextAsync(scriptPath, $"#!/bin/sh\necho '{jsonl}'\n");
        Process.Start("chmod", $"+x {scriptPath}")!.WaitForExit();

        try
        {
            var config = _config with { BinaryPath = scriptPath };
            using var runner = new CodexProcessRunner(config);
            var result = await runner.ExecuteAsync("test");

            Assert.Equal(0, result.ExitCode);
            Assert.False(result.TimedOut);
            Assert.Equal(2, result.Events.Count);
            Assert.Equal(CodexAdapterEventType.Message, result.Events[0].EventType);
            Assert.Equal(CodexAdapterEventType.Completed, result.Events[1].EventType);
        }
        finally
        {
            File.Delete(scriptPath);
        }
    }

    [Fact]
    public async Task ExecuteAsync_SurfacesNonZeroExit()
    {
        var scriptPath = Path.Combine(Path.GetTempPath(), $"codex-fail-{Guid.NewGuid():N}.sh");
        await File.WriteAllTextAsync(scriptPath, "#!/bin/sh\necho 'fatal error' >&2\nexit 1\n");
        Process.Start("chmod", $"+x {scriptPath}")!.WaitForExit();

        try
        {
            var config = _config with { BinaryPath = scriptPath };
            using var runner = new CodexProcessRunner(config);
            var result = await runner.ExecuteAsync("test");

            Assert.Equal(1, result.ExitCode);
            Assert.Contains("fatal error", result.Stderr);
        }
        finally
        {
            File.Delete(scriptPath);
        }
    }

    [Fact]
    public async Task ExecuteAsync_Timeout_KillsProcess()
    {
        var scriptPath = Path.Combine(Path.GetTempPath(), $"codex-hang-{Guid.NewGuid():N}.sh");
        await File.WriteAllTextAsync(scriptPath, "#!/bin/sh\nsleep 60\necho 'done'\n");
        Process.Start("chmod", $"+x {scriptPath}")!.WaitForExit();

        try
        {
            var config = _config with { BinaryPath = scriptPath, TimeoutMs = 500 };
            using var runner = new CodexProcessRunner(config);
            var result = await runner.ExecuteAsync("test");

            Assert.True(result.TimedOut);
        }
        finally
        {
            File.Delete(scriptPath);
        }
    }

    public void Dispose() => _runner.Dispose();
}
