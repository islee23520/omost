using System.IO;

namespace Lfe.CommandExecutor.Tests;

public sealed class HookCommandExecutionTests
{
    [Fact]
    public async Task ExecuteHookCommandAsync_passes_stdin_and_returns_trimmed_output()
    {
        var tempDirectory = Directory.CreateTempSubdirectory("lfe-command-executor-");

        try
        {
            var result = await HookCommandExecution.ExecuteHookCommandAsync(
                "read input; printf \"stdout:$input\\n\"; printf \"stderr:$input\\n\" >&2",
                "value\n",
                tempDirectory.FullName);

            Assert.Equal(new CommandResult(0, "stdout:value", "stderr:value"), result);
        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task ExecuteHookCommandAsync_expands_home_and_project_directory_placeholders()
    {
        var tempDirectory = Directory.CreateTempSubdirectory("lfe-command-executor-");
        var homeDirectory = Path.Combine(tempDirectory.FullName, "home");

        try
        {
            var result = await EnvironmentScope.RunAsync(async () =>
            {
                Environment.SetEnvironmentVariable("HOME", homeDirectory);
                return await HookCommandExecution.ExecuteHookCommandAsync(
                    "printf \"%s|%s|%s\" ~ $CLAUDE_PROJECT_DIR ${CLAUDE_PROJECT_DIR}",
                    string.Empty,
                    tempDirectory.FullName);
            });

            Assert.Equal(0, result.ExitCode);
            Assert.Equal($"{homeDirectory}|{tempDirectory.FullName}|{tempDirectory.FullName}", result.Stdout);
        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task ExecuteHookCommandAsync_limits_environment_variables_when_allowed_env_vars_is_provided()
    {
        var tempDirectory = Directory.CreateTempSubdirectory("lfe-command-executor-");

        try
        {
            var result = await EnvironmentScope.RunAsync(async () =>
            {
                Environment.SetEnvironmentVariable("__LFEDOT_ALLOWED", "visible");
                Environment.SetEnvironmentVariable("__LFEDOT_SECRET", "hidden");

                return await HookCommandExecution.ExecuteHookCommandAsync(
                    "printf \"%s|%s|%s\" \"$__LFEDOT_ALLOWED\" \"$__LFEDOT_SECRET\" \"$CLAUDE_PROJECT_DIR\"",
                    string.Empty,
                    tempDirectory.FullName,
                    new ExecuteHookOptions(AllowedEnvVars: ["__LFEDOT_ALLOWED"]));
            });

            Assert.Equal(0, result.ExitCode);
            Assert.Equal($"visible||{tempDirectory.FullName}", result.Stdout);
        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task ExecuteHookCommandAsync_falls_back_through_force_zsh_when_zsh_path_is_missing()
    {
        var tempDirectory = Directory.CreateTempSubdirectory("lfe-command-executor-");

        try
        {
            var result = await HookCommandExecution.ExecuteHookCommandAsync(
                "printf 'forced shell'",
                string.Empty,
                tempDirectory.FullName,
                new ExecuteHookOptions(ForceZsh: true, ZshPath: Path.Combine(tempDirectory.FullName, "missing-zsh")));

            Assert.Equal(0, result.ExitCode);
            Assert.Equal("forced shell", result.Stdout);
        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task ExecuteHookCommandAsync_reports_timeout_failures_in_stderr()
    {
        var tempDirectory = Directory.CreateTempSubdirectory("lfe-command-executor-");

        try
        {
            var result = await HookCommandExecution.ExecuteHookCommandAsync(
                "sleep 5",
                string.Empty,
                tempDirectory.FullName,
                new ExecuteHookOptions(TimeoutMs: 50));

            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains("Hook command timed out after 50ms", result.Stderr);
        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
    }
}
