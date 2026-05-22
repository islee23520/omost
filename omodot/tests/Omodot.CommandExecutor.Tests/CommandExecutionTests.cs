namespace Omodot.CommandExecutor.Tests;

public sealed class CommandExecutionTests
{
    [Fact]
    public async Task ExecuteCommandAsync_returns_trimmed_stdout()
    {
        var result = await CommandExecution.ExecuteCommandAsync("printf 'hello\\n'");

        Assert.Equal("hello", result);
    }

    [Fact]
    public async Task ExecuteCommandAsync_returns_stderr_only_when_successful_command_only_writes_stderr()
    {
        var result = await CommandExecution.ExecuteCommandAsync("printf 'err-only' >&2");

        Assert.Equal("[stderr: err-only]", result);
    }

    [Fact]
    public async Task ExecuteCommandAsync_returns_stdout_followed_by_stderr_when_command_fails()
    {
        var result = await CommandExecution.ExecuteCommandAsync("printf 'out'; printf 'err' >&2; exit 7");

        Assert.Equal("out\n[stderr: err]", result);
    }
}
