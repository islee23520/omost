namespace Lfe.TeamModeCore;

public enum TeamSessionRole
{
    Lead,
    Member,
}

public static class TeamSessionRegistry
{
    public static IReadOnlyDictionary<string, TeamSessionEntry> CreateTeamSessionRegistryState(IEnumerable<KeyValuePair<string, TeamSessionEntry>>? entries = null)
    {
        return entries is null ? new Dictionary<string, TeamSessionEntry>() : new Dictionary<string, TeamSessionEntry>(entries.ToDictionary(entry => entry.Key, entry => entry.Value));
    }

    public static IReadOnlyDictionary<string, TeamSessionEntry> RegisterTeamSessionInState(IReadOnlyDictionary<string, TeamSessionEntry> registry, string sessionId, TeamSessionEntry entry)
    {
        var nextRegistry = new Dictionary<string, TeamSessionEntry>(registry, StringComparer.Ordinal) { [sessionId] = entry };
        return nextRegistry;
    }

    public static TeamSessionEntry? LookupTeamSessionInState(IReadOnlyDictionary<string, TeamSessionEntry> registry, string sessionId)
    {
        return registry.TryGetValue(sessionId, out var entry) ? entry : null;
    }

    public static IReadOnlyDictionary<string, TeamSessionEntry> UnregisterTeamSessionInState(IReadOnlyDictionary<string, TeamSessionEntry> registry, string sessionId)
    {
        var nextRegistry = new Dictionary<string, TeamSessionEntry>(registry, StringComparer.Ordinal);
        nextRegistry.Remove(sessionId);
        return nextRegistry;
    }

    public static IReadOnlyDictionary<string, TeamSessionEntry> UnregisterTeamSessionsByTeamInState(IReadOnlyDictionary<string, TeamSessionEntry> registry, string teamRunId)
    {
        var nextRegistry = new Dictionary<string, TeamSessionEntry>(registry, StringComparer.Ordinal);
        foreach (var (sessionId, entry) in registry)
        {
            if (entry.TeamRunId == teamRunId)
            {
                nextRegistry.Remove(sessionId);
            }
        }

        return nextRegistry;
    }

    public static IReadOnlyDictionary<string, TeamSessionEntry> ClearTeamSessionRegistryState() => new Dictionary<string, TeamSessionEntry>();
}
