using System.Globalization;
using System.Text.RegularExpressions;

namespace Omodot.SearchTools;

public static partial class Grep
{
    public const int DEFAULT_GREP_MAX_DEPTH = 20;
    public const string DEFAULT_MAX_FILESIZE = "10M";
    public const int DEFAULT_MAX_COUNT = 500;
    public const int DEFAULT_MAX_COLUMNS = 1000;
    public const int DEFAULT_GREP_TIMEOUT_MS = 60_000;
    public const int DEFAULT_GREP_MAX_OUTPUT_BYTES = 256 * 1024;

    public static IReadOnlyList<string> RG_GREP_FLAGS { get; } = new[]
    {
        "--no-follow",
        "--color=never",
        "--no-heading",
        "--line-number",
        "--with-filename",
        "--no-messages",
    };

    public static IReadOnlyList<string> CLASSIC_GREP_FLAGS { get; } = new[]
    {
        "-n",
        "-H",
        "--color=never",
    };

    public static IReadOnlyList<string> BuildRipgrepArgs(GrepOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var args = new List<string>(RG_GREP_FLAGS)
        {
            $"--threads={Math.Min(options.Threads ?? ResolveCli.DEFAULT_RG_THREADS, ResolveCli.DEFAULT_RG_THREADS)}",
            $"--max-depth={Math.Min(options.MaxDepth ?? DEFAULT_GREP_MAX_DEPTH, DEFAULT_GREP_MAX_DEPTH)}",
            $"--max-filesize={options.MaxFilesize ?? DEFAULT_MAX_FILESIZE}",
            $"--max-count={Math.Min(options.MaxCount ?? DEFAULT_MAX_COUNT, DEFAULT_MAX_COUNT)}",
            $"--max-columns={Math.Min(options.MaxColumns ?? DEFAULT_MAX_COLUMNS, DEFAULT_MAX_COLUMNS)}",
        };

        if (options.Context is > 0)
        {
            args.Add($"-C{Math.Min(options.Context.Value, 10)}");
        }

        if (options.CaseSensitive)
        {
            args.Add("--case-sensitive");
        }

        if (options.WholeWord)
        {
            args.Add("-w");
        }

        if (options.FixedStrings)
        {
            args.Add("-F");
        }

        if (options.Multiline)
        {
            args.Add("-U");
        }

        if (options.Hidden)
        {
            args.Add("--hidden");
        }

        if (options.NoIgnore)
        {
            args.Add("--no-ignore");
        }

        foreach (var type in options.FileType ?? Array.Empty<string>())
        {
            args.Add($"--type={type}");
        }

        foreach (var glob in options.Globs ?? Array.Empty<string>())
        {
            args.Add($"--glob={glob}");
        }

        foreach (var glob in options.ExcludeGlobs ?? Array.Empty<string>())
        {
            args.Add($"--glob=!{glob}");
        }

        if (options.OutputMode == GrepOutputMode.FilesWithMatches)
        {
            args.Add("--files-with-matches");
        }

        if (options.OutputMode == GrepOutputMode.Count)
        {
            args.Add("--count");
        }

        return args;
    }

    public static IReadOnlyList<string> BuildClassicGrepArgs(GrepOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var args = new List<string>(CLASSIC_GREP_FLAGS) { "-r" };
        if (options.Context is > 0)
        {
            args.Add($"-C{Math.Min(options.Context.Value, 10)}");
        }

        if (!options.CaseSensitive)
        {
            args.Add("-i");
        }

        if (options.WholeWord)
        {
            args.Add("-w");
        }

        if (options.FixedStrings)
        {
            args.Add("-F");
        }

        foreach (var glob in options.Globs ?? Array.Empty<string>())
        {
            args.Add($"--include={glob}");
        }

        foreach (var glob in options.ExcludeGlobs ?? Array.Empty<string>())
        {
            args.Add($"--exclude={glob}");
        }

        args.Add("--exclude-dir=.git");
        args.Add("--exclude-dir=node_modules");
        return args;
    }

    public static IReadOnlyList<GrepMatch> ParseOutput(string output, bool filesOnly = false)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return Array.Empty<GrepMatch>();
        }

        var matches = new List<GrepMatch>();
        foreach (var rawLine in output.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (filesOnly)
            {
                matches.Add(new GrepMatch(line.Trim(), 0, string.Empty));
                continue;
            }

            var match = GrepLineRegex().Match(line);
            if (match.Success)
            {
                matches.Add(new GrepMatch(
                    match.Groups[1].Value,
                    int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture),
                    match.Groups[3].Value));
            }
        }

        return matches;
    }

    public static IReadOnlyList<CountResult> ParseCountOutput(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return Array.Empty<CountResult>();
        }

        var results = new List<CountResult>();
        foreach (var rawLine in output.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var match = CountLineRegex().Match(line);
            if (match.Success)
            {
                results.Add(new CountResult(
                    match.Groups[1].Value,
                    int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture)));
            }
        }

        return results;
    }

    public static async Task<GrepResult> RunGrepAsync(GrepOptions options, GrepRunnerDeps? deps = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        await SearchSemaphore.Instance.AcquireAsync().ConfigureAwait(false);
        try
        {
            var cli = deps?.ResolveCli?.Invoke() ?? ResolveCli.ResolveGrepCli();
            var spawn = deps?.SpawnProcess ?? SearchProcessSpawner.Spawn;
            var args = (cli.Backend == SearchBackend.Rg ? BuildRipgrepArgs(options) : BuildClassicGrepArgs(options)).ToList();
            var timeout = Math.Min(options.Timeout ?? DEFAULT_GREP_TIMEOUT_MS, DEFAULT_GREP_TIMEOUT_MS);

            if (cli.Backend == SearchBackend.Rg)
            {
                args.Add("--");
                args.Add(options.Pattern);
            }
            else
            {
                args.Add("-e");
                args.Add(options.Pattern);
            }

            args.AddRange(options.Paths?.Count > 0 ? options.Paths : ["."]);

            using var process = spawn([cli.Path, .. args], new SpawnOptions { Stdout = SpawnStreamMode.Pipe, Stderr = SpawnStreamMode.Pipe });
            var output = await SearchProcessOutputCollector
                .CollectSearchProcessOutputAsync(process, timeout, $"Search timeout after {timeout}ms")
                .ConfigureAwait(false);

            var truncatedByBytes = output.Stdout.Length >= DEFAULT_GREP_MAX_OUTPUT_BYTES;
            var outputToProcess = truncatedByBytes ? output.Stdout[..DEFAULT_GREP_MAX_OUTPUT_BYTES] : output.Stdout;

            if (output.ExitCode > 1 && !string.IsNullOrWhiteSpace(output.Stderr))
            {
                return new GrepResult { Error = output.Stderr.Trim(), FilesSearched = 0, TotalMatches = 0, Truncated = false };
            }

            var matches = ParseOutput(outputToProcess, options.OutputMode == GrepOutputMode.FilesWithMatches);
            var limited = options.HeadLimit is > 0 ? matches.Take(options.HeadLimit.Value).ToArray() : matches.ToArray();

            return new GrepResult
            {
                Matches = limited,
                TotalMatches = limited.Length,
                FilesSearched = limited.Select(match => match.File).Distinct(StringComparer.Ordinal).Count(),
                Truncated = truncatedByBytes || (options.HeadLimit is > 0 && matches.Count > options.HeadLimit.Value),
            };
        }
        catch (Exception ex)
        {
            return new GrepResult { Error = ex.Message, FilesSearched = 0, TotalMatches = 0, Truncated = false };
        }
        finally
        {
            SearchSemaphore.Instance.Release();
        }
    }

    public static async Task<IReadOnlyList<CountResult>> RunGrepCountAsync(GrepOptions options, GrepRunnerDeps? deps = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        await SearchSemaphore.Instance.AcquireAsync().ConfigureAwait(false);
        try
        {
            var cli = deps?.ResolveCli?.Invoke() ?? ResolveCli.ResolveGrepCli();
            var spawn = deps?.SpawnProcess ?? SearchProcessSpawner.Spawn;
            var countOptions = options with { Context = 0 };
            var args = (cli.Backend == SearchBackend.Rg ? BuildRipgrepArgs(countOptions) : BuildClassicGrepArgs(countOptions)).ToList();
            if (cli.Backend == SearchBackend.Rg)
            {
                args.Add("--count");
                args.Add("--");
                args.Add(options.Pattern);
            }
            else
            {
                args.Add("-c");
                args.Add("-e");
                args.Add(options.Pattern);
            }

            args.AddRange(options.Paths?.Count > 0 ? options.Paths : ["."]);
            var timeout = Math.Min(options.Timeout ?? DEFAULT_GREP_TIMEOUT_MS, DEFAULT_GREP_TIMEOUT_MS);

            using var process = spawn([cli.Path, .. args], new SpawnOptions { Stdout = SpawnStreamMode.Pipe, Stderr = SpawnStreamMode.Pipe });
            var output = await SearchProcessOutputCollector
                .CollectSearchProcessOutputAsync(process, timeout, $"Search timeout after {timeout}ms")
                .ConfigureAwait(false);

            if (output.ExitCode > 1 && !string.IsNullOrWhiteSpace(output.Stderr))
            {
                throw new InvalidOperationException(output.Stderr.Trim());
            }

            return ParseCountOutput(output.Stdout);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Count search failed: {ex.Message}", ex);
        }
        finally
        {
            SearchSemaphore.Instance.Release();
        }
    }

    public static string FormatGrepResult(GrepResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (!string.IsNullOrWhiteSpace(result.Error))
        {
            return $"Error: {result.Error}";
        }

        if (result.Matches.Count == 0)
        {
            return "No matches found";
        }

        var lines = new List<string>();
        var isFilesOnlyMode = result.Matches.All(match => match.Line == 0 && string.IsNullOrWhiteSpace(match.Text));
        lines.Add($"Found {result.TotalMatches} match(es) in {result.FilesSearched} file(s)");
        if (result.Truncated)
        {
            lines.Add("[Output truncated due to size limit]");
        }

        lines.Add(string.Empty);

        var byFile = new Dictionary<string, List<GrepMatch>>(StringComparer.Ordinal);
        foreach (var match in result.Matches)
        {
            if (!byFile.TryGetValue(match.File, out var matches))
            {
                matches = new List<GrepMatch>();
                byFile[match.File] = matches;
            }

            matches.Add(match);
        }

        foreach (var entry in byFile)
        {
            lines.Add(entry.Key);
            if (!isFilesOnlyMode)
            {
                foreach (var match in entry.Value)
                {
                    var trimmedText = match.Text.Trim();
                    if (match.Line == 0 && trimmedText.Length == 0)
                    {
                        continue;
                    }

                    lines.Add($"  {match.Line}: {trimmedText}");
                }
            }

            lines.Add(string.Empty);
        }

        return string.Join('\n', lines);
    }

    public static string FormatCountResult(IReadOnlyList<CountResult> results)
    {
        ArgumentNullException.ThrowIfNull(results);

        if (results.Count == 0)
        {
            return "No matches found";
        }

        var total = results.Sum(result => result.Count);
        var lines = new List<string> { $"Found {total} match(es) in {results.Count} file(s):", string.Empty };
        foreach (var result in results.OrderByDescending(item => item.Count))
        {
            lines.Add($"  {result.Count.ToString(CultureInfo.InvariantCulture).PadLeft(6)}: {result.File}");
        }

        return string.Join('\n', lines);
    }

    [GeneratedRegex("^([A-Za-z]:[\\/].*?|.+?):(\\d+):(.*)$")]
    private static partial Regex GrepLineRegex();

    [GeneratedRegex("^([A-Za-z]:[\\/].*?|.+?):(\\d+)$")]
    private static partial Regex CountLineRegex();
}
