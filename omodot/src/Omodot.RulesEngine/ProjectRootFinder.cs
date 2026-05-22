namespace Omodot.RulesEngine;

public static class ProjectRootFinder
{
    private static readonly Dictionary<string, string?> Cache = new(StringComparer.Ordinal);

    public static void ClearCache() => Cache.Clear();

    public static string? FindProjectRoot(string startPath)
    {
        if (Cache.TryGetValue(startPath, out var cached)) return cached;

        var startDir = ResolveStartDir(startPath);
        if (Cache.TryGetValue(startDir, out var cachedDir))
        {
            Cache[startPath] = cachedDir;
            return cachedDir;
        }

        var visited = new List<string>();
        var current = startDir;
        string? resolved = null;

        while (true)
        {
            if (Cache.TryGetValue(current, out var cachedAncestor))
            {
                resolved = cachedAncestor;
                break;
            }
            visited.Add(current);
            if (HasProjectMarker(current)) { resolved = current; break; }
            var parent = System.IO.Path.GetDirectoryName(current);
            if (parent is null || parent == current) break;
            current = parent;
        }

        foreach (var dir in visited) Cache[dir] = resolved;
        Cache[startPath] = resolved;
        return resolved;
    }

    private static string ResolveStartDir(string startPath)
    {
        try { return Directory.Exists(startPath) ? startPath : System.IO.Path.GetDirectoryName(startPath)!; }
        catch { return System.IO.Path.GetDirectoryName(startPath)!; }
    }

    private static bool HasProjectMarker(string directory)
        => RuleConstants.ProjectMarkers.Any(marker => File.Exists(System.IO.Path.Join(directory, marker)) ||
                                                       Directory.Exists(System.IO.Path.Join(directory, marker)));
}
