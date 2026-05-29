using Lfe.RulesEngine;

namespace Lfe.AgentsMd;

public static class AgentsMdInjector
{
    public static async Task ProcessFilePathForAgentsInjection(
        string rootDirectory,
        IAgentsMdTruncator truncator,
        Dictionary<string, HashSet<string>> sessionCaches,
        IAgentsMdInjectedPathsStorage storage,
        IAgentsMdCache? agentsMdCache,
        string filePath,
        string sessionId,
        AgentsMdContextOutput output)
    {
        if (output.Output is not string) return;

        var resolved = AgentsMdFormatter.ResolveFilePath(rootDirectory, filePath);
        if (resolved is null) return;

        var dir = Path.GetDirectoryName(resolved)!;
        var cache = GetSessionCache(sessionCaches, sessionId, storage);

        var agentsPaths = await AgentsMdFinder.FindAgentsMdUp(new FindAgentsMdUpInput(dir, rootDirectory));

        var dirty = false;
        foreach (var agentsPath in agentsPaths)
        {
            var agentsDir = Path.GetDirectoryName(agentsPath)!;
            if (cache.Contains(agentsDir)) continue;

            string? content = null;
            try { content = await File.ReadAllTextAsync(agentsPath); } catch { }

            if (content is null) continue;
            cache.Add(agentsDir);

            var (result, truncated) = await truncator.Truncate(sessionId, content);
            output.Output += AgentsMdFormatter.FormatAgentsMdContextBlock(agentsPath, result, truncated);
            dirty = true;
        }

        if (dirty) storage.SaveInjectedPaths(sessionId, cache);
    }

    private static HashSet<string> GetSessionCache(
        Dictionary<string, HashSet<string>> sessionCaches,
        string sessionId,
        IAgentsMdInjectedPathsStorage storage)
    {
        if (sessionCaches.TryGetValue(sessionId, out var existing)) return existing;
        var loaded = storage.LoadInjectedPaths(sessionId);
        sessionCaches[sessionId] = loaded;
        return loaded;
    }
}
