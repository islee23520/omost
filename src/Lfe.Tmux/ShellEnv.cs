namespace Lfe.Tmux;

public static class ShellEnv
{
    private static readonly HashSet<char> EscapedCharacters = ['"', '$', '`', '\\', ';', '|', '&', '<', '>'];

    public static string ShellEscapeForDoubleQuotedCommand(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var buffer = new System.Text.StringBuilder(value.Length);
        foreach (var character in value)
        {
            if (EscapedCharacters.Contains(character))
            {
                buffer.Append('\\');
            }

            buffer.Append(character);
        }

        return buffer.ToString();
    }

    public static IReadOnlyDictionary<string, string?> ParseEnvironmentVariables(string? output, bool nullSeparated = true)
    {
        if (string.IsNullOrEmpty(output))
        {
            return new Dictionary<string, string?>(StringComparer.Ordinal);
        }

        var separator = nullSeparated ? '\0' : '\n';
        var result = new Dictionary<string, string?>(StringComparer.Ordinal);

        foreach (var entry in output.Split(separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var equalsIndex = entry.IndexOf('=');
            if (equalsIndex <= 0)
            {
                continue;
            }

            var key = entry[..equalsIndex];
            var value = entry[(equalsIndex + 1)..];
            result[key] = value;
        }

        return result;
    }

    public static async Task<IReadOnlyDictionary<string, string?>> GetShellEnvironmentVariablesAsync(
        string shellPath = "/bin/sh",
        string command = "env -0",
        IReadOnlyDictionary<string, string?>? baseEnvironment = null,
        CancellationToken cancellationToken = default)
    {
        var result = await SpawnProcess.RunAsync(shellPath, ["-c", command], baseEnvironment, null, cancellationToken);
        return result.ExitCode == 0
            ? ParseEnvironmentVariables(result.Stdout, nullSeparated: true)
            : new Dictionary<string, string?>(StringComparer.Ordinal);
    }
}
