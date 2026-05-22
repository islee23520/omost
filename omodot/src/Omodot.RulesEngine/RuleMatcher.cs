using System.Security.Cryptography;
using System.Text;
using DotNet.Globbing;

namespace Omodot.RulesEngine;

public static class RuleMatcher
{
    private static readonly Dictionary<string, Glob> MatcherCache = new();
    private const int MaxMatcherCacheEntries = 256;

    public static void ResetMatcherCache() => MatcherCache.Clear();

    public static int GetMatcherCacheEntries() => MatcherCache.Count;

    public static MatchResult ShouldApplyRule(RuleMetadata metadata, string currentFilePath, string? projectRoot)
    {
        if (metadata.AlwaysApply) return new MatchResult(true, "alwaysApply");

        var patterns = NormalizeGlobs(metadata);
        if (patterns.Length == 0) return new MatchResult(false);

        var pathBases = new[]
        {
            ToPosix(projectRoot != null ? System.IO.Path.GetRelativePath(projectRoot, currentFilePath) : currentFilePath),
            ToPosix(System.IO.Path.GetFileName(currentFilePath)),
        };

        var negativeMatchers = patterns
            .Where(p => p.StartsWith('!'))
            .Select(p => MatcherFor(p[1..]))
            .ToList();

        foreach (var pattern in patterns)
        {
            if (pattern.StartsWith('!')) continue;
            var isMatch = MatcherFor(pattern);
            if (!pathBases.Any(pathBase => isMatch.IsMatch(pathBase))) continue;
            if (pathBases.Any(pathBase => negativeMatchers.Any(excl => excl.IsMatch(pathBase))))
                return new MatchResult(false);
            return new MatchResult(true, $"glob: {pattern}");
        }

        return new MatchResult(false);
    }

    public static string CreateContentHash(string content)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes).ToLowerInvariant()[..16];
    }

    public static bool IsDuplicateByRealPath(string realPath, IReadOnlySet<string> cache) => cache.Contains(realPath);
    public static bool IsDuplicateByContentHash(string hash, IReadOnlySet<string> cache) => cache.Contains(hash);

    private static string[] NormalizeGlobs(RuleMetadata metadata)
    {
        var patterns = new List<string>();
        patterns.AddRange(NormalizePatternList(metadata.Globs));
        patterns.AddRange(NormalizePatternList(metadata.Paths));
        patterns.AddRange(NormalizePatternList(metadata.ApplyTo));
        return patterns.Select(ToPosix).Distinct().ToArray();
    }

    private static string[] NormalizePatternList(string[]? patterns) => patterns is null ? [] : patterns;

    private static Glob MatcherFor(string pattern)
    {
        if (MatcherCache.TryGetValue(pattern, out var cached))
        {
            // LRU: remove and re-add to move to end
            MatcherCache.Remove(pattern);
            MatcherCache[pattern] = cached;
            return cached;
        }

        var glob = Glob.Parse(ToPosix(pattern));
        if (MatcherCache.Count >= MaxMatcherCacheEntries)
        {
            var oldest = MatcherCache.Keys.First();
            MatcherCache.Remove(oldest);
        }
        MatcherCache[pattern] = glob;
        return glob;
    }

    private static string ToPosix(string path) => path.Replace('\\', '/');
}
