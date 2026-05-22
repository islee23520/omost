using System.Diagnostics;

namespace Omodot.SearchTools;

public enum SearchBackend
{
    Rg,
    Grep,
    Find,
    PowerShell,
}

public enum SpawnStreamMode
{
    Pipe,
    Inherit,
    Ignore,
}

public enum GrepOutputMode
{
    Content,
    FilesWithMatches,
    Count,
}

public sealed record ResolvedSearchCli(string Path, SearchBackend Backend);

public sealed record SpawnOptions
{
    public string? Cwd { get; init; }

    public SpawnStreamMode Stdout { get; init; } = SpawnStreamMode.Pipe;

    public SpawnStreamMode Stderr { get; init; } = SpawnStreamMode.Pipe;
}

public sealed class ProcessReadableStream
{
    public ProcessReadableStream(StreamReader? reader)
    {
        Reader = reader;
    }

    public StreamReader? Reader { get; }
}

public sealed class SpawnedProcess : IDisposable
{
    private readonly Action _kill;
    private readonly IDisposable? _disposable;

    public SpawnedProcess(
        Task<int> exited,
        ProcessReadableStream? stdout,
        ProcessReadableStream? stderr,
        Action? kill = null,
        IDisposable? disposable = null)
    {
        Exited = exited ?? throw new ArgumentNullException(nameof(exited));
        Stdout = stdout;
        Stderr = stderr;
        _kill = kill ?? (() => { });
        _disposable = disposable;
    }

    public Task<int> Exited { get; }

    public ProcessReadableStream? Stdout { get; }

    public ProcessReadableStream? Stderr { get; }

    public void Kill()
    {
        _kill();
    }

    public void Dispose()
    {
        _disposable?.Dispose();
    }
}

public sealed record FileMatch(string Path, long Mtime);

public sealed record GlobResult
{
    public IReadOnlyList<FileMatch> Files { get; init; } = Array.Empty<FileMatch>();

    public int TotalFiles { get; init; }

    public bool Truncated { get; init; }

    public string? Error { get; init; }
}

public sealed record GlobOptions
{
    public string Pattern { get; init; } = string.Empty;

    public IReadOnlyList<string>? Paths { get; init; }

    public bool Hidden { get; init; } = true;

    public bool Follow { get; init; } = true;

    public bool NoIgnore { get; init; }

    public int? MaxDepth { get; init; }

    public int? Timeout { get; init; }

    public int? Limit { get; init; }

    public int? Threads { get; init; }
}

public sealed record GlobRunnerDeps(
    Func<ResolvedSearchCli>? ResolveCli,
    Func<IReadOnlyList<string>, SpawnOptions?, SpawnedProcess>? SpawnProcess);

public sealed record GrepMatch(string File, int Line, string Text);

public sealed record GrepResult
{
    public IReadOnlyList<GrepMatch> Matches { get; init; } = Array.Empty<GrepMatch>();

    public int TotalMatches { get; init; }

    public int FilesSearched { get; init; }

    public bool Truncated { get; init; }

    public string? Error { get; init; }
}

public sealed record CountResult(string File, int Count);

public sealed record GrepOptions
{
    public string Pattern { get; init; } = string.Empty;

    public IReadOnlyList<string>? Paths { get; init; }

    public IReadOnlyList<string>? Globs { get; init; }

    public IReadOnlyList<string>? ExcludeGlobs { get; init; }

    public int? Context { get; init; }

    public int? MaxDepth { get; init; }

    public string? MaxFilesize { get; init; }

    public int? MaxCount { get; init; }

    public int? MaxColumns { get; init; }

    public bool CaseSensitive { get; init; }

    public bool WholeWord { get; init; }

    public bool FixedStrings { get; init; }

    public bool Multiline { get; init; }

    public bool Hidden { get; init; }

    public bool NoIgnore { get; init; }

    public IReadOnlyList<string>? FileType { get; init; }

    public int? Timeout { get; init; }

    public int? Threads { get; init; }

    public GrepOutputMode OutputMode { get; init; } = GrepOutputMode.Content;

    public int? HeadLimit { get; init; }
}

public sealed record GrepRunnerDeps(
    Func<ResolvedSearchCli>? ResolveCli,
    Func<IReadOnlyList<string>, SpawnOptions?, SpawnedProcess>? SpawnProcess);

public sealed record SearchProcessOutput(string Stdout, string Stderr, int ExitCode);

internal static class SearchProcessSpawner
{
    public static SpawnedProcess Spawn(IReadOnlyList<string> command, SpawnOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.Count == 0)
        {
            throw new ArgumentException("Command must contain at least one element.", nameof(command));
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = command[0],
            UseShellExecute = false,
            RedirectStandardOutput = options?.Stdout == SpawnStreamMode.Pipe,
            RedirectStandardError = options?.Stderr == SpawnStreamMode.Pipe,
        };

        if (!string.IsNullOrWhiteSpace(options?.Cwd))
        {
            startInfo.WorkingDirectory = options.Cwd;
        }

        for (var i = 1; i < command.Count; i++)
        {
            startInfo.ArgumentList.Add(command[i]);
        }

        var process = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true,
        };

        try
        {
            process.Start();
        }
        catch
        {
            process.Dispose();
            throw;
        }

        var exited = WaitForExitAsync(process);
        var stdout = startInfo.RedirectStandardOutput ? new ProcessReadableStream(process.StandardOutput) : null;
        var stderr = startInfo.RedirectStandardError ? new ProcessReadableStream(process.StandardError) : null;
        return new SpawnedProcess(exited, stdout, stderr, () => Kill(process), process);
    }

    private static async Task<int> WaitForExitAsync(Process process)
    {
        await process.WaitForExitAsync().ConfigureAwait(false);
        return process.ExitCode;
    }

    private static void Kill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
        }
    }
}
