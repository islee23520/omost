namespace Lfe.RulesEngine;

public static class AgentsMdFinder
{
    public static async Task<string[]> FindAgentsMdUp(FindAgentsMdUpInput input)
    {
        var startDir = System.IO.Path.GetFullPath(input.StartDir);
        var rootDir = System.IO.Path.GetFullPath(input.RootDir);
        var skipRoot = input.SkipRoot;
        var cacheKey = $"{startDir}\0{rootDir}\0{(skipRoot ? "1" : "0")}";

        if (input.Cache?.Get(cacheKey) is { } cached) return cached.ToArray();

        var found = new List<string>();
        var current = startDir;

        while (true)
        {
            var isRootDir = current == rootDir;
            if (!(skipRoot && isRootDir))
            {
                var agentsPath = System.IO.Path.Join(current, RuleConstants.AgentsFilename);
                if (File.Exists(agentsPath)) found.Add(agentsPath);
            }
            if (isRootDir) break;
            var parent = System.IO.Path.GetDirectoryName(current);
            if (parent is null || parent == current || !IsSameOrChildPath(parent, rootDir)) break;
            current = parent;
        }

        found.Reverse();
        var result = found.ToArray();
        input.Cache?.Set(cacheKey, result);
        return result;
    }

    private static bool IsSameOrChildPath(string childPath, string parentPath)
    {
        var rel = System.IO.Path.GetRelativePath(parentPath, childPath);
        return rel == "." || (!rel.StartsWith("..") && !System.IO.Path.IsPathRooted(rel));
    }
}
