namespace Omodot.TmuxSubagent;

public sealed record SessionMapping(string SessionId, string PaneId, DateTimeOffset CreatedAt);

public static class OldestAgentPane
{
    public static TmuxPaneInfo? FindOldestAgentPane(IReadOnlyList<TmuxPaneInfo> agentPanes, IReadOnlyList<SessionMapping> sessionMappings)
    {
        if (agentPanes.Count == 0) return null;

        var paneIdToAge = new Dictionary<string, DateTimeOffset>();
        foreach (var mapping in sessionMappings)
            paneIdToAge[mapping.PaneId] = mapping.CreatedAt;

        TmuxPaneInfo? bestPane = null;
        DateTimeOffset? bestAge = null;

        foreach (var pane in agentPanes)
        {
            if (paneIdToAge.TryGetValue(pane.PaneId, out var age))
            {
                if (bestAge is null || age < bestAge)
                {
                    bestPane = pane;
                    bestAge = age;
                }
            }
        }

        if (bestPane is not null) return bestPane;

        bestPane = agentPanes[0];
        foreach (var pane in agentPanes)
        {
            if (pane.Top < bestPane.Top || (pane.Top == bestPane.Top && pane.Left < bestPane.Left))
                bestPane = pane;
        }
        return bestPane;
    }
}
