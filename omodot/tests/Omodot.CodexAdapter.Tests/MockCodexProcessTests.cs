using Xunit;

namespace Omodot.CodexAdapter.Tests;

public sealed class MockCodexProcessTests
{
    [Fact]
    public void CreateHappyPath_BuildsExpectedOutput()
    {
        var mock = MockCodexProcess.CreateHappyPath("my-session");
        var stdout = mock.BuildStdout();
        Assert.Contains("my-session", stdout);
        Assert.Contains("Hello from codex", stdout);
        Assert.Equal(string.Empty, mock.BuildStderr());
        Assert.Equal(0, mock.ExitCode);
    }

    [Fact]
    public void CreateHappyPath_DefaultSessionId()
    {
        var mock = MockCodexProcess.CreateHappyPath();
        var stdout = mock.BuildStdout();
        Assert.Contains("test-session", stdout);
    }

    [Fact]
    public void CreateCrash_SetsErrorState()
    {
        var mock = MockCodexProcess.CreateCrash("fatal error");
        Assert.Equal(1, mock.ExitCode);
        Assert.Contains("fatal error", mock.BuildStderr());
    }

    [Fact]
    public void CreateTimeout_SetsTimeoutFlag()
    {
        var mock = MockCodexProcess.CreateTimeout();
        Assert.True(mock.SimulatesTimeout);
    }

    [Fact]
    public void WithExitCode_SetsCustomExitCode()
    {
        var mock = new MockCodexProcess().WithExitCode(42);
        Assert.Equal(42, mock.ExitCode);
    }

    [Fact]
    public void WithJsonlLines_AppendsLines()
    {
        var mock = new MockCodexProcess()
            .WithJsonlLines("line1", "line2")
            .WithJsonlLines("line3");
        var stdout = mock.BuildStdout();
        Assert.Contains("line1", stdout);
        Assert.Contains("line2", stdout);
        Assert.Contains("line3", stdout);
    }

    [Fact]
    public void HappyPath_ProducesThreeJsonlLines()
    {
        var mock = MockCodexProcess.CreateHappyPath("s1");
        var stdout = mock.BuildStdout();
        Assert.Contains("message", stdout);
        Assert.Contains("idle", stdout);
        Assert.Contains("completed", stdout);
    }

    [Fact]
    public void MockProcess_CanFeedIntoParser()
    {
        var mock = MockCodexProcess.CreateHappyPath("session-1");
        var parser = new CodexJsonlParser();
        var events = parser.ParseStream(mock.BuildStdout());
        Assert.Equal(3, events.Count);
        Assert.Equal(CodexAdapterEventType.Message, events[0].EventType);
        Assert.Equal(CodexAdapterEventType.Idle, events[1].EventType);
        Assert.Equal(CodexAdapterEventType.Completed, events[2].EventType);
    }
}
