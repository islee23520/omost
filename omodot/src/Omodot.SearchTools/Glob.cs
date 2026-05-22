namespace Omodot.SearchTools;

public static class Glob
{
    public const int DEFAULT_TIMEOUT_MS = 60_000;
    public const int DEFAULT_LIMIT = 100;
    public const int DEFAULT_MAX_DEPTH = 20;
    public const int DEFAULT_MAX_OUTPUT_BYTES = 10 * 1024 * 1024;

    public static IReadOnlyList<string> RG_FILES_FLAGS { get; } = new[]
    {
        "--files",
        "--color=never",
        "--glob=!.git/*",
        "--no-messages",
    };

    public static IReadOnlyList<string> BuildGlobRgArgs(GlobOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var args = new List<string>(RG_FILES_FLAGS)
        {
            $"--threads={Math.Min(options.Threads ?? ResolveCli.DEFAULT_RG_THREADS, ResolveCli.DEFAULT_RG_THREADS)}",
            $"--max-depth={Math.Min(options.MaxDepth ?? DEFAULT_MAX_DEPTH, DEFAULT_MAX_DEPTH)}",
        };

        if (options.Hidden)
        {
            args.Add("--hidden");
        }

        if (options.Follow)
        {
            args.Add("--follow");
        }

        if (options.NoIgnore)
        {
            args.Add("--no-ignore");
        }

        args.Add($"--glob={options.Pattern}");
        return args;
    }

    public static IReadOnlyList<string> BuildFindArgs(GlobOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var args = new List<string>();
        if (options.Follow)
        {
            args.Add("-L");
        }

        args.Add(".");
        args.Add("-maxdepth");
        args.Add(Math.Min(options.MaxDepth ?? DEFAULT_MAX_DEPTH, DEFAULT_MAX_DEPTH).ToString());
        args.Add("-type");
        args.Add("f");
        args.Add("-name");
        args.Add(options.Pattern);

        if (!options.Hidden)
        {
            args.Add("-not");
            args.Add("-path");
            args.Add("*/.*");
        }

        return args;
    }

    public static IReadOnlyList<string> BuildPowerShellCommand(GlobOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var maxDepth = Math.Min(options.MaxDepth ?? DEFAULT_MAX_DEPTH, DEFAULT_MAX_DEPTH);
        var searchPath = options.Paths?.FirstOrDefault() ?? ".";
        var escapedPath = searchPath.Replace("'", "''", StringComparison.Ordinal);
        var escapedPattern = options.Pattern.Replace("'", "''", StringComparison.Ordinal);
        var psCommand = $"Get-ChildItem -LiteralPath '{escapedPath}' -File -Recurse -Depth {maxDepth - 1} -Filter '{escapedPattern}'";
        if (options.Hidden)
        {
            psCommand += " -Force";
        }

        psCommand += " -ErrorAction SilentlyContinue | Select-Object -ExpandProperty FullName";
        return new[] { "powershell.exe", "-NoProfile", "-Command", psCommand };
    }

    public static async Task<GlobResult> RunGlobAsync(GlobOptions options, GlobRunnerDeps? deps = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        await SearchSemaphore.Instance.AcquireAsync().ConfigureAwait(false);
        try
        {
            var cli = deps?.ResolveCli?.Invoke() ?? ResolveCli.ResolveGlobCli();
            var spawn = deps?.SpawnProcess ?? SearchProcessSpawner.Spawn;
            var timeout = Math.Min(options.Timeout ?? DEFAULT_TIMEOUT_MS, DEFAULT_TIMEOUT_MS);
            var limit = Math.Min(options.Limit ?? DEFAULT_LIMIT, DEFAULT_LIMIT);
            var isRg = cli.Backend == SearchBackend.Rg;
            var isWindows = cli.Backend == SearchBackend.PowerShell;

            string? cwd = null;
            IReadOnlyList<string> command;
            if (isRg)
            {
                cwd = options.Paths?.FirstOrDefault() ?? ".";
                command = new List<string> { cli.Path }.Concat(BuildGlobRgArgs(options)).Append(".").ToArray();
            }
            else if (isWindows)
            {
                command = BuildPowerShellCommand(options);
            }
            else
            {
                cwd = options.Paths?.FirstOrDefault() ?? ".";
                command = new List<string> { cli.Path }.Concat(BuildFindArgs(options)).ToArray();
            }

            using var process = spawn(command, new SpawnOptions { Cwd = cwd, Stdout = SpawnStreamMode.Pipe, Stderr = SpawnStreamMode.Pipe });
            var output = await SearchProcessOutputCollector
                .CollectSearchProcessOutputAsync(process, timeout, $"Glob search timeout after {timeout}ms")
                .ConfigureAwait(false);

            if (output.ExitCode > 1 && !string.IsNullOrWhiteSpace(output.Stderr))
            {
                return new GlobResult { Error = output.Stderr.Trim(), TotalFiles = 0, Truncated = false };
            }

            var truncatedOutput = output.Stdout.Length >= DEFAULT_MAX_OUTPUT_BYTES;
            var outputToProcess = truncatedOutput ? output.Stdout[..DEFAULT_MAX_OUTPUT_BYTES] : output.Stdout;
            var files = new List<FileMatch>();
            var truncated = false;

            foreach (var line in SplitLines(outputToProcess))
            {
                if (files.Count >= limit)
                {
                    truncated = true;
                    break;
                }

                var filePath = isWindows ? line.Trim() : Path.GetFullPath(line, cwd ?? ".");
                files.Add(new FileMatch(filePath, await GetFileMtimeAsync(filePath).ConfigureAwait(false)));
            }

            files.Sort((left, right) => right.Mtime.CompareTo(left.Mtime));
            return new GlobResult
            {
                Files = files,
                TotalFiles = files.Count,
                Truncated = truncated || truncatedOutput,
            };
        }
        catch (Exception ex)
        {
            return new GlobResult { Error = ex.Message, TotalFiles = 0, Truncated = false };
        }
        finally
        {
            SearchSemaphore.Instance.Release();
        }
    }

    public static string FormatGlobResult(GlobResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (!string.IsNullOrWhiteSpace(result.Error))
        {
            return $"Error: {result.Error}";
        }

        if (result.Files.Count == 0)
        {
            return "No files found";
        }

        var lines = new List<string> { $"Found {result.TotalFiles} file(s)", string.Empty };
        lines.AddRange(result.Files.Select(file => file.Path));
        if (result.Truncated)
        {
            lines.Add(string.Empty);
            lines.Add("(Results are truncated. Consider using a more specific path or pattern.)");
        }

        return string.Join('\n', lines);
    }

    private static IEnumerable<string> SplitLines(string input)
    {
        foreach (var line in input.Trim().Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var normalized = line.TrimEnd('\r');
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                yield return normalized;
            }
        }
    }

    private static Task<long> GetFileMtimeAsync(string filePath)
    {
        try
        {
            var mtime = File.GetLastWriteTimeUtc(filePath);
            return Task.FromResult(new DateTimeOffset(mtime).ToUnixTimeMilliseconds());
        }
        catch
        {
            return Task.FromResult(0L);
        }
    }
}
