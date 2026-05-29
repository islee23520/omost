namespace Lfe.RulesEngine;

public static class RuleFinder
{
    public delegate void DeprecationLogger(string message, string path);

    private static DeprecationLogger _logDeprecation = (_, _) => { };
    private static readonly HashSet<string> WarnedDirs = new(StringComparer.Ordinal);
    private static readonly HashSet<string> SisyphusSources = new([".sisyphus/rules", "~/.sisyphus/rules"]);
    private const string SisyphusDeprecationMessage = "[rules] .sisyphus/rules is deprecated and will be removed in v4.3.0; migrate to .omo/rules";

    public static void SetDeprecationLogger(DeprecationLogger logger) => _logDeprecation = logger;

    public static List<RuleFileCandidate> FindRuleFiles(
        string? projectRoot,
        string homeDir,
        string currentFile,
        FindRuleFilesOptions? options = null,
        IRuleScanCache? cache = null)
    {
        options ??= new FindRuleFilesOptions();
        var startDir = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(currentFile))!;
        var effectiveProjectRoot = ResolveEffectiveProjectRoot(projectRoot, options.WorkspaceDirectory, startDir);
        var cacheKey = $"{projectRoot ?? ""}\0{effectiveProjectRoot}\0{startDir}\0{(options.SkipClaudeUserRules ? "1" : "0")}";

        if (cache?.Get(cacheKey) is { } cached) return cached.ToList();

        var candidates = new List<RuleFileCandidate>();
        var seenRealPaths = new HashSet<string>(StringComparer.Ordinal);

        AddProjectRuleCandidates(effectiveProjectRoot, startDir, candidates, seenRealPaths, cache);
        AddProjectSingleFileCandidates(effectiveProjectRoot, candidates, seenRealPaths);
        AddUserRuleCandidates(homeDir, options.SkipClaudeUserRules, candidates, seenRealPaths, cache);

        var sorted = RuleOrdering.SortCandidates(candidates);
        cache?.Set(cacheKey, sorted);
        return sorted;
    }

    private static string ResolveEffectiveProjectRoot(string? projectRoot, string? workspaceDirectory, string startDir)
    {
        if (projectRoot is not null) return projectRoot;
        if (workspaceDirectory is null) return startDir;
        var workspaceRoot = System.IO.Path.GetFullPath(workspaceDirectory);
        return IsSameOrChildPath(startDir, workspaceRoot) ? workspaceRoot : startDir;
    }

    private static void AddProjectRuleCandidates(
        string projectRoot, string startDir, List<RuleFileCandidate> candidates,
        HashSet<string> seenRealPaths, IRuleScanCache? cache)
    {
        var projectRootRealPath = RuleScanner.SafeRealPath(projectRoot);
        var currentDir = startDir;
        var distance = 0;

        while (true)
        {
            foreach (var (parent, subdir) in RuleConstants.ProjectRuleSubdirs)
            {
                var source = $"{parent}/{subdir}";
                var ruleDir = System.IO.Path.Join(currentDir, parent, subdir);
                foreach (var entry in ScanDirectoryWithCache(ruleDir, cache, projectRootRealPath))
                {
                    if (seenRealPaths.Contains(entry.RealPath)) continue;
                    seenRealPaths.Add(entry.RealPath);
                    WarnSisyphusDeprecation(source, entry.Path);
                    candidates.Add(new RuleFileCandidate(
                        Path: entry.Path,
                        RealPath: entry.RealPath,
                        IsGlobal: false,
                        Distance: distance,
                        RelativePath: NormalizePath(System.IO.Path.GetRelativePath(projectRoot, entry.Path)),
                        Source: source));
                }
            }
            if (currentDir == projectRoot) break;
            var parentDir = System.IO.Path.GetDirectoryName(currentDir);
            if (parentDir is null || parentDir == currentDir || !IsSameOrChildPath(parentDir, projectRoot)) break;
            currentDir = parentDir;
            distance++;
        }
    }

    private static void AddProjectSingleFileCandidates(
        string projectRoot, List<RuleFileCandidate> candidates, HashSet<string> seenRealPaths)
    {
        foreach (var ruleFile in RuleConstants.ProjectRuleFiles)
        {
            var filePath = System.IO.Path.Join(projectRoot, ruleFile);
            if (!File.Exists(filePath)) continue;
            var realPath = RuleScanner.SafeRealPath(filePath);
            if (seenRealPaths.Contains(realPath)) continue;
            seenRealPaths.Add(realPath);
            candidates.Add(new RuleFileCandidate(
                Path: filePath,
                RealPath: realPath,
                IsGlobal: false,
                Distance: 0,
                RelativePath: NormalizePath(ruleFile),
                Source: ruleFile,
                IsSingleFile: true));
        }
    }

    private static void AddUserRuleCandidates(
        string homeDir, bool skipClaudeUserRules, List<RuleFileCandidate> candidates,
        HashSet<string> seenRealPaths, IRuleScanCache? cache)
    {
        var userRuleDirs = RuleConstants.OpencodeUserRuleDirs
            .Select(dir => (System.IO.Path.Join(homeDir, dir), $"~/{dir}" as string))
            .ToList();

        if (!skipClaudeUserRules)
            userRuleDirs.Add((System.IO.Path.Join(homeDir, RuleConstants.UserRuleDir), "~/.claude/rules"));

        foreach (var (userRuleDir, source) in userRuleDirs)
        {
            foreach (var entry in ScanDirectoryWithCache(userRuleDir, cache))
            {
                if (seenRealPaths.Contains(entry.RealPath)) continue;
                seenRealPaths.Add(entry.RealPath);
                WarnSisyphusDeprecation(source, entry.Path);
                candidates.Add(new RuleFileCandidate(
                    Path: entry.Path,
                    RealPath: entry.RealPath,
                    IsGlobal: true,
                    Distance: RuleConstants.GlobalDistance,
                    RelativePath: NormalizePath(System.IO.Path.GetRelativePath(homeDir, entry.Path)),
                    Source: source));
            }
        }
    }

    private static IReadOnlyList<DirectoryScanEntry> ScanDirectoryWithCache(
        string dir, IRuleScanCache? cache, string? boundaryRealPath = null)
    {
        if (cache?.GetDirScan(dir) is { } cached) return cached;
        var entries = new List<DirectoryScanEntry>();
        RuleScanner.FindRuleFilesRecursive(dir, entries, null, boundaryRealPath);
        cache?.SetDirScan(dir, entries);
        return entries;
    }

    private static void WarnSisyphusDeprecation(string source, string path)
    {
        if (!SisyphusSources.Contains(source)) return;
        var warningKey = System.IO.Path.GetDirectoryName(path);
        if (warningKey is null || WarnedDirs.Contains(warningKey)) return;
        WarnedDirs.Add(warningKey);
        _logDeprecation(SisyphusDeprecationMessage, path);
    }

    internal static void ResetWarningState() { WarnedDirs.Clear(); _logDeprecation = (_, _) => { }; }

    private static bool IsSameOrChildPath(string childPath, string parentPath)
    {
        var rel = System.IO.Path.GetRelativePath(parentPath, childPath);
        return rel == "." || (!rel.StartsWith("..") && !System.IO.Path.IsPathRooted(rel));
    }

    private static string NormalizePath(string path) => path.Replace('\\', '/');
}
