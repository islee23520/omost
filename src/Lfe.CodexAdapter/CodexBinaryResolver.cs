using System.Diagnostics;

namespace Lfe.CodexAdapter;

/// <summary>
/// Resolves the codex executable path from explicit option, environment variable, or PATH lookup.
/// Resolution order: explicit CodexBinaryPath &gt; CODEX_BINARY_PATH env var &gt; PATH lookup.
/// </summary>
public sealed class CodexBinaryResolver
{
    /// <summary>
    /// Resolves the codex binary path using the configured options.
    /// </summary>
    public string Resolve(CodexAdapterOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        // 1. Explicit path wins
        if (!string.IsNullOrWhiteSpace(options.CodexBinaryPath))
        {
            if (!File.Exists(options.CodexBinaryPath))
                throw new InvalidOperationException(
                    $"Codex binary not found at explicit path: {options.CodexBinaryPath}");

            return options.CodexBinaryPath;
        }

        // 2. Environment variable override
        var envPath = Environment.GetEnvironmentVariable(CodexTransportConstants.CodexBinaryEnvVar);
        if (!string.IsNullOrWhiteSpace(envPath))
        {
            if (!File.Exists(envPath))
                throw new InvalidOperationException(
                    $"Codex binary not found at {CodexTransportConstants.CodexBinaryEnvVar} path: {envPath}");

            return envPath;
        }

        // 3. PATH lookup
        var pathBinary = FindOnPath(CodexTransportConstants.CodexBinaryName);
        if (pathBinary is not null)
            return pathBinary;

        throw new InvalidOperationException(
            $"Codex binary '{CodexTransportConstants.CodexBinaryName}' not found. " +
            $"Set {CodexTransportConstants.CodexBinaryEnvVar} or provide an explicit path in options.");
    }

    /// <summary>
    /// Resolves the full runtime configuration from adapter options.
    /// </summary>
    public CodexResolvedConfig ResolveConfig(CodexAdapterOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var binaryPath = Resolve(options);
        var workingDirectory = string.IsNullOrWhiteSpace(options.WorkingDirectory)
            ? Directory.GetCurrentDirectory()
            : options.WorkingDirectory;

        if (!Directory.Exists(workingDirectory))
            throw new InvalidOperationException(
                $"Working directory does not exist: {workingDirectory}");

        return new CodexResolvedConfig(
            binaryPath,
            workingDirectory,
            options.TimeoutMs ?? CodexTransportConstants.DefaultTimeoutMs,
            options.EnvironmentOverrides ?? new Dictionary<string, string>(),
            options.SessionOptions);
    }

    private static string? FindOnPath(string binaryName)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "which",
                    Arguments = binaryName,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                },
            };
            process.Start();
            var output = process.StandardOutput.ReadLine();
            process.WaitForExit();
            if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output) && File.Exists(output))
                return output;
        }
        catch
        {
            // which not available, ignore
        }

        return null;
    }
}

/// <summary>
/// Fully resolved runtime configuration for codex process invocation.
/// </summary>
public sealed record CodexResolvedConfig(
    string BinaryPath,
    string WorkingDirectory,
    int TimeoutMs,
    IReadOnlyDictionary<string, string> EnvironmentOverrides,
    CodexSessionOptions SessionOptions);
