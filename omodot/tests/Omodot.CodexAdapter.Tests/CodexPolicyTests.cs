using Xunit;
using Omodot.UlwHostContract;

namespace Omodot.CodexAdapter.Tests;

public sealed class CodexPolicyTests
{
    private static string CreateScript(string name, string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{name}-{Guid.NewGuid():N}.sh");
        File.WriteAllText(path, content);
        System.Diagnostics.Process.Start("chmod", $"+x {path}")!.WaitForExit();
        return path;
    }

    private static CodexResolvedConfig Config(string scriptPath)
        => new(scriptPath, Path.GetTempPath(), 5000, new Dictionary<string, string>(), new CodexSessionOptions());

    [Fact]
    public async Task TodoPolicy_AlwaysReturnsEmpty()
    {
        using var host = new CodexUlwHost(Config("/bin/true"));
        var todos = await host.ReadTodosAsync("any");
        Assert.Empty(todos);
    }

    [Fact]
    public async Task StatusPolicy_TimedOut_ReturnsTimedOut()
    {
        var scriptPath = CreateScript("policy-timeout", "#!/bin/sh\nsleep 60\n");
        try
        {
            using var host = new CodexUlwHost(Config(scriptPath) with { TimeoutMs = 200 });
            await host.DispatchPromptAsync(new UlwPromptRequest("s1", "test"));
            var status = await host.ReadStatusAsync("s1");
            Assert.Equal("timed_out", status);
        }
        finally { File.Delete(scriptPath); }
    }

    [Fact]
    public async Task StatusPolicy_FailedExit_ReturnsFailed()
    {
        var scriptPath = CreateScript("policy-fail", "#!/bin/sh\nexit 1\n");
        try
        {
            using var host = new CodexUlwHost(Config(scriptPath));
            await host.DispatchPromptAsync(new UlwPromptRequest("s1", "test"));
            var status = await host.ReadStatusAsync("s1");
            Assert.Equal("failed", status);
        }
        finally { File.Delete(scriptPath); }
    }

    [Fact]
    public async Task StatusPolicy_CompletedEvent_ReturnsCompleted()
    {
        var scriptPath = CreateScript("policy-done", "#!/bin/sh\necho '{\"type\":\"completed\",\"session_id\":\"s1\"}'\n");
        try
        {
            using var host = new CodexUlwHost(Config(scriptPath));
            await host.DispatchPromptAsync(new UlwPromptRequest("s1", "test"));
            var status = await host.ReadStatusAsync("s1");
            Assert.Equal("completed", status);
        }
        finally { File.Delete(scriptPath); }
    }

    [Fact]
    public async Task StatusPolicy_IdleWithoutCompleted_ReturnsIdle()
    {
        var scriptPath = CreateScript("policy-idle", "#!/bin/sh\necho '{\"type\":\"idle\",\"session_id\":\"s1\"}'\n");
        try
        {
            using var host = new CodexUlwHost(Config(scriptPath));
            await host.DispatchPromptAsync(new UlwPromptRequest("s1", "test"));
            var status = await host.ReadStatusAsync("s1");
            Assert.Equal("idle", status);
        }
        finally { File.Delete(scriptPath); }
    }

    [Fact]
    public async Task StatusPolicy_UnknownSession_ReturnsUnknown()
    {
        using var host = new CodexUlwHost(Config("/bin/true"));
        var status = await host.ReadStatusAsync("never-dispatched");
        Assert.Equal("unknown", status);
    }

    [Fact]
    public async Task EventPolicy_IdleForwardsAsIdle()
    {
        var scriptPath = CreateScript("policy-evt-idle", "#!/bin/sh\necho '{\"type\":\"idle\",\"session_id\":\"s1\"}'\n");
        try
        {
            using var host = new CodexUlwHost(Config(scriptPath));
            var events = new List<UlwSessionEvent>();
            host.OnEvent(events.Add);
            await host.DispatchPromptAsync(new UlwPromptRequest("s1", "test"));
            Assert.Contains(events, e => e.Type == UlwSessionEventType.Idle);
        }
        finally { File.Delete(scriptPath); }
    }

    [Fact]
    public async Task EventPolicy_ErrorForwardsWithError()
    {
        var scriptPath = CreateScript("policy-evt-err", "#!/bin/sh\necho '{\"type\":\"error\",\"error\":\"boom\"}'\nexit 1\n");
        try
        {
            using var host = new CodexUlwHost(Config(scriptPath));
            var events = new List<UlwSessionEvent>();
            host.OnEvent(events.Add);
            await host.DispatchPromptAsync(new UlwPromptRequest("s1", "test"));
            Assert.Contains(events, e => e.Type == UlwSessionEventType.Error && e.Error == "boom");
        }
        finally { File.Delete(scriptPath); }
    }

    [Fact]
    public async Task EventPolicy_StatusEventsNotForwarded()
    {
        var scriptPath = CreateScript("policy-evt-status", "#!/bin/sh\necho '{\"type\":\"status\",\"session_id\":\"s1\",\"status\":\"running\"}'\nexit 0\n");
        try
        {
            using var host = new CodexUlwHost(Config(scriptPath));
            var events = new List<UlwSessionEvent>();
            host.OnEvent(events.Add);
            await host.DispatchPromptAsync(new UlwPromptRequest("s1", "test"));
            Assert.Empty(events);
        }
        finally { File.Delete(scriptPath); }
    }
}
