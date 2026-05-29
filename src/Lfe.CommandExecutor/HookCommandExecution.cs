using System.Text.RegularExpressions;

namespace Lfe.CommandExecutor;

public static partial class HookCommandExecution
{
    private const string ClaudeProjectDirectoryVariable = "CLAUDE_PROJECT_DIR";

    public static async Task<CommandResult> ExecuteHookCommandAsync(
        string command,
        string stdin,
        string cwd,
        ExecuteHookOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentException.ThrowIfNullOrWhiteSpace(cwd);

        options ??= new ExecuteHookOptions();

        var home = HomeDirectory.GetHomeDirectory();
        var expandedCommand = ExpandCommand(command, home, cwd);
        var request = options.ForceZsh
            ? CreateForcedShellRequest(expandedCommand, stdin, cwd, home, options)
            : CreateDefaultShellRequest(expandedCommand, stdin, cwd, home, options);

        return await ProcessExecution.RunAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private static ProcessExecutionRequest CreateForcedShellRequest(
        string command,
        string stdin,
        string cwd,
        string home,
        ExecuteHookOptions options)
    {
        var shellPath = ShellPathFinder.FindZshPath(options.ZshPath) ?? ShellPathFinder.FindBashPath();
        if (string.IsNullOrWhiteSpace(shellPath))
        {
            return CreateDefaultShellRequest(command, stdin, cwd, home, options);
        }

        return new ProcessExecutionRequest(
            shellPath,
            ["-lc", command],
            cwd,
            CreateEnvironmentVariables(home, cwd, options.AllowedEnvVars),
            stdin,
            options.TimeoutMs);
    }

    private static ProcessExecutionRequest CreateDefaultShellRequest(
        string command,
        string stdin,
        string cwd,
        string home,
        ExecuteHookOptions options)
    {
        return OperatingSystem.IsWindows()
            ? new ProcessExecutionRequest(
                "cmd.exe",
                ["/d", "/s", "/c", command],
                cwd,
                CreateEnvironmentVariables(home, cwd, options.AllowedEnvVars),
                stdin,
                options.TimeoutMs)
            : new ProcessExecutionRequest(
                "/bin/sh",
                ["-c", command],
                cwd,
                CreateEnvironmentVariables(home, cwd, options.AllowedEnvVars),
                stdin,
                options.TimeoutMs);
    }

    private static IReadOnlyDictionary<string, string?> CreateEnvironmentVariables(
        string home,
        string cwd,
        IReadOnlyList<string>? allowedEnvVars)
    {
        var variables = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["HOME"] = home,
            [ClaudeProjectDirectoryVariable] = cwd,
        };

        if (allowedEnvVars is null)
        {
            foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
            {
                variables[(string)entry.Key] = entry.Value?.ToString();
            }

            variables["HOME"] = home;
            variables[ClaudeProjectDirectoryVariable] = cwd;
            return variables;
        }

        variables["PATH"] = Environment.GetEnvironmentVariable("PATH");

        var allowed = new HashSet<string>(allowedEnvVars, StringComparer.Ordinal);
        foreach (var key in allowed)
        {
            if (string.Equals(key, "HOME", StringComparison.Ordinal) ||
                string.Equals(key, ClaudeProjectDirectoryVariable, StringComparison.Ordinal))
            {
                continue;
            }

            variables[key] = Environment.GetEnvironmentVariable(key);
        }

        return variables;
    }

    private static string ExpandCommand(string command, string home, string cwd)
    {
        var expanded = LeadingTildePattern().Replace(command, home);
        expanded = InlineTildePattern().Replace(expanded, $" {home}");
        expanded = expanded.Replace($"${ClaudeProjectDirectoryVariable}", cwd, StringComparison.Ordinal);
        expanded = expanded.Replace($"${{{ClaudeProjectDirectoryVariable}}}", cwd, StringComparison.Ordinal);
        return expanded;
    }

    [GeneratedRegex(@"^~(?=/|$)", RegexOptions.CultureInvariant)]
    private static partial Regex LeadingTildePattern();

    [GeneratedRegex(@"\s~(?=/)", RegexOptions.CultureInvariant)]
    private static partial Regex InlineTildePattern();
}
