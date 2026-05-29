namespace Lfe.CommandExecutor;

public static class CommandExecution
{
    public static async Task<string> ExecuteCommandAsync(string command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        try
        {
            var result = await ProcessExecution.RunAsync(CreateRequest(command), cancellationToken).ConfigureAwait(false);
            return FormatOutput(result.Stdout, result.Stderr);
        }
        catch (Exception ex)
        {
            return $"[stderr: {ex.Message}]";
        }
    }

    private static ProcessExecutionRequest CreateRequest(string command)
    {
        return OperatingSystem.IsWindows()
            ? new ProcessExecutionRequest("cmd.exe", ["/d", "/s", "/c", command])
            : new ProcessExecutionRequest("/bin/sh", ["-c", command]);
    }

    internal static string FormatOutput(string? stdout, string? stderr)
    {
        if (!string.IsNullOrEmpty(stderr))
        {
            return !string.IsNullOrEmpty(stdout)
                ? $"{stdout}{Environment.NewLine}[stderr: {stderr}]"
                : $"[stderr: {stderr}]";
        }

        return stdout ?? string.Empty;
    }
}
