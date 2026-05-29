namespace Lfe.CodexAdapter.Tests;

/// <summary>
/// Mock codex process that emits controlled JSONL output for testing.
/// Supports happy-path, timeout, crash, and no-binary simulations.
/// </summary>
public sealed class MockCodexProcess : IDisposable
{
    private readonly List<string> _jsonlLines = [];
    private int _exitCode;
    private bool _simulateTimeout;
    private bool _simulateCrash;
    private string _crashMessage = string.Empty;

    /// <summary>
    /// Configures the mock to emit the given JSONL lines as stdout.
    /// </summary>
    public MockCodexProcess WithJsonlLines(params string[] lines)
    {
        _jsonlLines.AddRange(lines);
        return this;
    }

    /// <summary>
    /// Configures the mock to exit with the given code.
    /// </summary>
    public MockCodexProcess WithExitCode(int exitCode)
    {
        _exitCode = exitCode;
        return this;
    }

    /// <summary>
    /// Configures the mock to simulate a timeout (never completes).
    /// </summary>
    public MockCodexProcess WithTimeout()
    {
        _simulateTimeout = true;
        return this;
    }

    /// <summary>
    /// Configures the mock to simulate a crash (writes to stderr, non-zero exit).
    /// </summary>
    public MockCodexProcess WithCrash(string errorMessage = "codex crashed unexpectedly")
    {
        _simulateCrash = true;
        _exitCode = 1;
        _crashMessage = errorMessage;
        return this;
    }

    /// <summary>
    /// Builds the full stdout output as a single string.
    /// </summary>
    public string BuildStdout()
        => string.Join("\n", _jsonlLines) + "\n";

    /// <summary>
    /// Builds the stderr output (empty for success, error for crash).
    /// </summary>
    public string BuildStderr()
        => _simulateCrash ? _crashMessage : string.Empty;

    /// <summary>
    /// Gets the configured exit code.
    /// </summary>
    public int ExitCode => _exitCode;

    /// <summary>
    /// Gets whether timeout simulation is enabled.
    /// </summary>
    public bool SimulatesTimeout => _simulateTimeout;

    /// <summary>
    /// Creates a standard happy-path mock that emits message + idle + completed events.
    /// </summary>
    public static MockCodexProcess CreateHappyPath(string sessionId = "test-session")
    {
        return new MockCodexProcess()
            .WithJsonlLines(
                $"{{\"type\":\"message\",\"session_id\":\"{sessionId}\",\"role\":\"assistant\",\"content\":\"Hello from codex\"}}",
                $"{{\"type\":\"idle\",\"session_id\":\"{sessionId}\"}}",
                $"{{\"type\":\"completed\",\"session_id\":\"{sessionId}\"}}");
    }

    /// <summary>
    /// Creates a crash-simulation mock.
    /// </summary>
    public static MockCodexProcess CreateCrash(string error = "codex crashed")
        => new MockCodexProcess().WithCrash(error);

    /// <summary>
    /// Creates a timeout-simulation mock.
    /// </summary>
    public static MockCodexProcess CreateTimeout()
        => new MockCodexProcess().WithTimeout();

    public void Dispose()
    {
    }
}
