using System.Diagnostics;

namespace Omodot.CodexAdapter;

public sealed record CodexRunResult(
    int ExitCode,
    string Stderr,
    IReadOnlyList<CodexAdapterEvent> Events,
    bool TimedOut);

public sealed class CodexProcessRunner : IDisposable
{
    private readonly CodexResolvedConfig _config;
    private Process? _process;

    public CodexProcessRunner(CodexResolvedConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        _config = config;
    }

    public ProcessStartInfo BuildStartInfo(string prompt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);

        var startInfo = new ProcessStartInfo
        {
            FileName = _config.BinaryPath,
            WorkingDirectory = _config.WorkingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            CreateNoWindow = true,
        };

        startInfo.ArgumentList.Add(CodexTransportConstants.ExecArg);
        startInfo.ArgumentList.Add(CodexTransportConstants.ExperimentalJsonFlag);

        if (!string.IsNullOrWhiteSpace(_config.SessionOptions.ModelId))
        {
            startInfo.ArgumentList.Add("--model");
            startInfo.ArgumentList.Add(_config.SessionOptions.ModelId);
        }

        if (!string.IsNullOrWhiteSpace(_config.SessionOptions.AgentName))
        {
            startInfo.ArgumentList.Add("--agent");
            startInfo.ArgumentList.Add(_config.SessionOptions.AgentName);
        }

        startInfo.ArgumentList.Add("--");
        startInfo.ArgumentList.Add(prompt);

        foreach (var (key, value) in _config.EnvironmentOverrides)
            startInfo.Environment[key] = value;

        return startInfo;
    }

    public async Task<CodexRunResult> ExecuteAsync(string prompt, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);

        using var timeoutCts = new CancellationTokenSource(_config.TimeoutMs);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

        var startInfo = BuildStartInfo(prompt);
        _process = new Process { StartInfo = startInfo };

        if (!_process.Start())
            return new CodexRunResult(-1, "Failed to start codex process", [], false);

        _process.StandardInput.Close();

        var stderrTask = _process.StandardError.ReadToEndAsync(linkedCts.Token);

        var events = new List<CodexAdapterEvent>();
        var parser = new CodexJsonlParser();

        try
        {
            await foreach (var evt in parser.ParseStreamAsync(_process.StandardOutput, linkedCts.Token))
                events.Add(evt);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested)
        {
        }

        bool timedOut;
        string stderr;

        if (timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            timedOut = true;
            try { _process.Kill(true); } catch { }
            _process.WaitForExit(1000);
            stderr = "";
        }
        else
        {
            stderr = await stderrTask.ConfigureAwait(false);
            await _process.WaitForExitAsync(ct).ConfigureAwait(false);
            timedOut = false;
        }

        return new CodexRunResult(_process.ExitCode, stderr, events, timedOut);
    }

    public void Dispose()
    {
        if (_process is not null && !_process.HasExited)
        {
            try { _process.Kill(true); } catch { }
        }

        _process?.Dispose();
    }
}
