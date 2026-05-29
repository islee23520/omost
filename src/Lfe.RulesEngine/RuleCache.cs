namespace Lfe.RulesEngine;

public static class RuleCache
{
    public static IRuleScanCache CreateRuleScanCache()
    {
        var candidateCache = new Dictionary<string, IReadOnlyList<RuleFileCandidate>>();
        var directoryCache = new Dictionary<string, IReadOnlyList<DirectoryScanEntry>>();

        return new RuleScanCacheImpl(candidateCache, directoryCache);
    }

    public static IAgentsMdCache CreateAgentsMdCache()
    {
        return new AgentsMdCacheImpl();
    }

    private sealed class RuleScanCacheImpl(
        Dictionary<string, IReadOnlyList<RuleFileCandidate>> candidateCache,
        Dictionary<string, IReadOnlyList<DirectoryScanEntry>> directoryCache) : IRuleScanCache
    {
        public IReadOnlyList<RuleFileCandidate>? Get(string key) => candidateCache.GetValueOrDefault(key);
        public void Set(string key, IReadOnlyList<RuleFileCandidate> value) => candidateCache[key] = value;
        public IReadOnlyList<DirectoryScanEntry>? GetDirScan(string dir) => directoryCache.GetValueOrDefault(dir);
        public void SetDirScan(string dir, IReadOnlyList<DirectoryScanEntry> entries) => directoryCache[dir] = entries;
        public RuleScanCacheStats Stats() => new(candidateCache.Count, directoryCache.Count);
        public void Clear() { candidateCache.Clear(); directoryCache.Clear(); }
    }

    private sealed class AgentsMdCacheImpl : IAgentsMdCache
    {
        private readonly Dictionary<string, IReadOnlyList<string>> _cache = new();
        public IReadOnlyList<string>? Get(string key) => _cache.GetValueOrDefault(key);
        public void Set(string key, IReadOnlyList<string> value) => _cache[key] = value;
        public void Clear() => _cache.Clear();
    }
}
