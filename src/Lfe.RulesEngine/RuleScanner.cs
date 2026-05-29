namespace Lfe.RulesEngine;

public static class RuleScanner
{
    public static string SafeRealPath(string filePath)
    {
        try { return System.IO.Path.GetFullPath(filePath); }
        catch { return filePath; }
    }

    public static void FindRuleFilesRecursive(
        string dir,
        List<DirectoryScanEntry> results,
        HashSet<string>? visited = null,
        string? boundaryRoot = null)
    {
        visited ??= new HashSet<string>(StringComparer.Ordinal);

        if (!Directory.Exists(dir)) return;
        var realDir = SafeRealPath(dir);
        var effectiveBoundary = boundaryRoot ?? realDir;

        if (!IsPathWithinRoot(realDir, effectiveBoundary)) return;
        if (visited.Contains(realDir)) return;
        visited.Add(realDir);

        FileSystemInfo[] entries;
        try { entries = new DirectoryInfo(dir).GetFileSystemInfos().OrderBy(e => e.Name).ToArray(); }
        catch { return; }

        foreach (var entry in entries)
        {
            var fullPath = System.IO.Path.Join(dir, entry.Name);
            if (entry is DirectoryInfo)
            {
                if (!RuleConstants.ExcludedDirs.Contains(entry.Name))
                    FindRuleFilesRecursive(fullPath, results, visited, effectiveBoundary);
                continue;
            }

            if (entry is FileInfo && IsRuleFile(entry.Name, dir))
            {
                var realPath = SafeRealPath(fullPath);
                if (!IsPathWithinRoot(realPath, effectiveBoundary)) continue;
                results.Add(new DirectoryScanEntry(fullPath, realPath, entry.Name));
            }
        }
    }

    private static bool IsRuleFile(string fileName, string dir)
    {
        if (IsGitHubInstructionsDir(dir))
            return RuleConstants.GitHubInstructionsPattern.IsMatch(fileName);
        return RuleConstants.RuleExtensions.Any(ext => fileName.EndsWith(ext, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsGitHubInstructionsDir(string dir)
        => dir.Contains(".github/instructions") || dir.EndsWith(".github/instructions");

    internal static bool IsPathWithinRoot(string candidate, string root)
    {
        var rel = System.IO.Path.GetRelativePath(root, candidate);
        return rel == "." || (!rel.StartsWith("..") && !System.IO.Path.IsPathRooted(rel));
    }
}
