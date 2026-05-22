using Xunit;
using Omodot.UlwHostContract;

namespace Omodot.CodexAdapter.Tests;

public sealed class CodexUlwHostTests
{
    private static string CreateScript(string name, string scriptContent)
    {
        var scriptPath = Path.Combine(Path.GetTempPath(), $"{name}-{Guid.NewGuid():N}.sh");
        File.WriteAllText(scriptPath, scriptContent);
        System.Diagnostics.Process.Start("chmod", $"+x {scriptPath}")!.WaitForExit();
        return scriptPath;
    }

    private static CodexResolvedConfig MakeConfig(string binaryPath)
        => new(binaryPath, Path.GetTempPath(), 5000, new Dictionary<string, string>(), new CodexSessionOptions());

    [Fact]
    public async Task DispatchPromptAsync_ReturnsAcceptedOnSuccess()
    {
        var scriptPath = CreateScript("codex-ok", "#!/bin/sh\necho '{\"type\":\"completed\",\"session_id\":\"s1\"}'\n");
        try
        {
            using var host = new CodexUlwHost(MakeConfig(scriptPath));
            var receipt = await host.DispatchPromptAsync(new UlwPromptRequest("s1", "hello"));
            Assert.True(receipt.Accepted);
            Assert.Equal("s1", receipt.SessionId);
        }
        finally { File.Delete(scriptPath); }
    }

    [Fact]
    public async Task DispatchPromptAsync_ReturnsNonAcceptedOnFailure()
    {
        var scriptPath = CreateScript("codex-fail", "#!/bin/sh\necho 'error' >&2\nexit 1\n");
        try
        {
            using var host = new CodexUlwHost(MakeConfig(scriptPath));
            var receipt = await host.DispatchPromptAsync(new UlwPromptRequest("s3", "test"));
            Assert.False(receipt.Accepted);
        }
        finally { File.Delete(scriptPath); }
    }

    [Fact]
    public async Task ReadMessagesAsync_ReturnsEmptyForUnknownSession()
    {
        using var host = new CodexUlwHost(MakeConfig("/bin/true"));
        var messages = await host.ReadMessagesAsync("unknown");
        Assert.Empty(messages);
    }

    [Fact]
    public async Task ReadTodosAsync_ReturnsEmpty()
    {
        using var host = new CodexUlwHost(MakeConfig("/bin/true"));
        var todos = await host.ReadTodosAsync("any-session");
        Assert.Empty(todos);
    }

    [Fact]
    public async Task ReadStatusAsync_ReturnsUnknownForNewSession()
    {
        using var host = new CodexUlwHost(MakeConfig("/bin/true"));
        var status = await host.ReadStatusAsync("unknown");
        Assert.Equal("unknown", status);
    }

    [Fact]
    public async Task ReadStatusAsync_ReturnsCompletedAfterSuccessfulRun()
    {
        var scriptPath = CreateScript("codex-status", "#!/bin/sh\necho '{\"type\":\"completed\",\"session_id\":\"s2\"}'\n");
        try
        {
            using var host = new CodexUlwHost(MakeConfig(scriptPath));
            await host.DispatchPromptAsync(new UlwPromptRequest("s2", "test"));
            var status = await host.ReadStatusAsync("s2");
            Assert.Equal("completed", status);
        }
        finally { File.Delete(scriptPath); }
    }

    [Fact]
    public async Task AbortAsync_DoesNotThrow()
    {
        using var host = new CodexUlwHost(MakeConfig("/bin/true"));
        await host.AbortAsync("any-session");
    }

    [Fact]
    public async Task OnEvent_ReceivesIdleAndCompleted()
    {
        var scriptPath = CreateScript("codex-events", "#!/bin/sh\necho '{\"type\":\"message\",\"role\":\"assistant\",\"content\":\"hi\"}'\necho '{\"type\":\"idle\",\"session_id\":\"s1\"}'\necho '{\"type\":\"completed\",\"session_id\":\"s1\"}'\n");
        try
        {
            using var host = new CodexUlwHost(MakeConfig(scriptPath));
            var events = new List<UlwSessionEvent>();
            host.OnEvent(events.Add);

            await host.DispatchPromptAsync(new UlwPromptRequest("s1", "test"));

            Assert.Contains(events, e => e.Type == UlwSessionEventType.Idle && e.SessionId == "s1");
            Assert.Contains(events, e => e.Type == UlwSessionEventType.Completed && e.SessionId == "s1");
        }
        finally { File.Delete(scriptPath); }
    }

    [Fact]
    public void OnEvent_UnsubscribeStopsReceiving()
    {
        using var host = new CodexUlwHost(MakeConfig("/bin/true"));
        var count = 0;
        var unsub = host.OnEvent(_ => count++);
        unsub();
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task ReadMessagesAsync_ReturnsMessagesFromRun()
    {
        var scriptPath = CreateScript("codex-msgs", "#!/bin/sh\necho '{\"type\":\"message\",\"role\":\"assistant\",\"content\":\"hello world\"}'\necho '{\"type\":\"completed\",\"session_id\":\"s4\"}'\n");
        try
        {
            using var host = new CodexUlwHost(MakeConfig(scriptPath));
            await host.DispatchPromptAsync(new UlwPromptRequest("s4", "test"));
            var messages = await host.ReadMessagesAsync("s4");
            Assert.Single(messages);
            Assert.Equal("assistant", messages[0].Role);
            Assert.Equal("hello world", messages[0].Text);
        }
        finally { File.Delete(scriptPath); }
    }
}
