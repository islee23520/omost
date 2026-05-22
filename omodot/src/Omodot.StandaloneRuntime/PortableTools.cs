namespace Omodot.StandaloneRuntime;

/// <summary>
/// A portable tool definition that can execute with arbitrary parameters.
/// </summary>
public sealed class PortableToolDefinition
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required Func<Dictionary<string, object?>, Task<string>> ExecuteAsync { get; init; }
}

/// <summary>
/// Runtime options for portable tools, including search configuration.
/// </summary>
public sealed class PortableToolRuntimeOptions
{
    public string? Cwd { get; init; }
    public SearchToolOptions? Search { get; init; }
}

public sealed class SearchToolOptions
{
    public Func<SearchTools.ResolvedSearchCli>? ResolveGlobCli { get; init; }
    public Func<SearchTools.ResolvedSearchCli>? ResolveGrepCli { get; init; }
    public Func<IReadOnlyList<string>, SearchTools.SpawnOptions?, SearchTools.SpawnedProcess>? SpawnProcess { get; init; }
}

public static class PortableTools
{
    private static string RequireString(Dictionary<string, object?> params_, string key)
    {
        if (params_.TryGetValue(key, out var value) && value is string s && s.Length > 0)
            return s;
        throw new ArgumentException($"Missing required string parameter '{key}'");
    }

    private static string? OptionalString(Dictionary<string, object?> params_, string key)
    {
        if (params_.TryGetValue(key, out var value) && value is string s)
            return s;
        return null;
    }

    private static int? OptionalNumber(Dictionary<string, object?> params_, string key)
    {
        if (params_.TryGetValue(key, out var value) && value is int i)
            return i;
        if (params_.TryGetValue(key, out var v2) && v2 is double d && double.IsFinite(d))
            return (int)d;
        return null;
    }

    private static string ResolveSearchPath(string baseDir, string? inputPath)
        => inputPath is not null ? Path.GetFullPath(inputPath, baseDir) : baseDir;

    public static List<PortableToolDefinition> CreatePortableTools(PortableToolRuntimeOptions? options = null)
    {
        options ??= new PortableToolRuntimeOptions();
        var cwd = options.Cwd ?? Directory.GetCurrentDirectory();
        var spawnProcess = options.Search?.SpawnProcess;

        return
        [
            new PortableToolDefinition
            {
                Name = "glob",
                Description = "Find files by glob pattern using the portable standalone runtime tool surface.",
                ExecuteAsync = async (params_) =>
                {
                    var pattern = RequireString(params_, "pattern");
                    var searchPath = ResolveSearchPath(cwd, OptionalString(params_, "path"));
                    var result = await SearchTools.Glob.RunGlobAsync(
                        new SearchTools.GlobOptions { Pattern = pattern, Paths = [searchPath] },
                        new SearchTools.GlobRunnerDeps(
                            ResolveCli: options.Search?.ResolveGlobCli,
                            SpawnProcess: spawnProcess));
                    return SearchTools.Glob.FormatGlobResult(result);
                },
            },
            new PortableToolDefinition
            {
                Name = "grep",
                Description = "Search file content using the portable standalone runtime tool surface.",
                ExecuteAsync = async (params_) =>
                {
                    var pattern = RequireString(params_, "pattern");
                    var include = OptionalString(params_, "include");
                    var outputModeStr = OptionalString(params_, "output_mode") ?? "files_with_matches";
                    var headLimit = OptionalNumber(params_, "head_limit") ?? 0;
                    var searchPath = ResolveSearchPath(cwd, OptionalString(params_, "path"));

                    var outputMode = outputModeStr is "content" or "files_with_matches"
                        ? (outputModeStr == "content"
                            ? SearchTools.GrepOutputMode.Content
                            : SearchTools.GrepOutputMode.FilesWithMatches)
                        : SearchTools.GrepOutputMode.FilesWithMatches;

                    if (outputModeStr == "count")
                    {
                        var results = await SearchTools.Grep.RunGrepCountAsync(
                            new SearchTools.GrepOptions
                            {
                                Pattern = pattern,
                                Paths = [searchPath],
                                Globs = include is not null ? [include] : null,
                            },
                            new SearchTools.GrepRunnerDeps(
                                ResolveCli: options.Search?.ResolveGrepCli,
                                SpawnProcess: spawnProcess));
                        var limited = headLimit > 0 ? results.Take(headLimit).ToList() : results;
                        return SearchTools.Grep.FormatCountResult(limited);
                    }

                    var result = await SearchTools.Grep.RunGrepAsync(
                        new SearchTools.GrepOptions
                        {
                            Pattern = pattern,
                            Paths = [searchPath],
                            Globs = include is not null ? [include] : null,
                            Context = 0,
                            OutputMode = outputMode,
                            HeadLimit = headLimit > 0 ? headLimit : null,
                        },
                        new SearchTools.GrepRunnerDeps(
                            ResolveCli: options.Search?.ResolveGrepCli,
                            SpawnProcess: spawnProcess));
                    return SearchTools.Grep.FormatGrepResult(result);
                },
            },
        ];
    }

    public static async Task<string> ExecutePortableToolAsync(
        IReadOnlyList<PortableToolDefinition> tools,
        string name,
        Dictionary<string, object?> parameters)
    {
        var tool = tools.FirstOrDefault(t => t.Name == name)
            ?? throw new ArgumentException($"Unknown portable tool: {name}");
        return await tool.ExecuteAsync(parameters);
    }
}
