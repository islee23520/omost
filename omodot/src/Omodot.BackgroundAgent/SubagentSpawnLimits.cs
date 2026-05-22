namespace Omodot.BackgroundAgent;

public sealed record SubagentSpawnContext
{
    public string RootSessionId { get; init; } = string.Empty;
    public int ParentDepth { get; init; }
    public int ChildDepth { get; init; }
}

public interface ISessionLineageReader
{
    Task<string?> GetParentSessionIdAsync(string sessionId);
}

public static class SubagentSpawnLimits
{
    public static int GetMaxSubagentDepth(BackgroundTaskCoreConfig? config = null)
    {
        return config?.MaxDepth ?? BackgroundAgentConstants.DefaultMaxSubagentDepth;
    }

    public static async Task<SubagentSpawnContext> ResolveSubagentSpawnContextAsync(ISessionLineageReader reader, string parentSessionId)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var rootSessionId = parentSessionId;
        var currentSessionId = parentSessionId;
        var parentDepth = 0;

        while (true)
        {
            if (!visited.Add(currentSessionId))
            {
                throw new InvalidOperationException($"Detected a session parent cycle while resolving {parentSessionId}");
            }

            string? nextParentSessionId;
            try
            {
                nextParentSessionId = await reader.GetParentSessionIdAsync(currentSessionId).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    $"Subagent spawn blocked: failed to resolve session lineage for {parentSessionId}, so background_task.maxDepth cannot be enforced safely. {exception.Message}",
                    exception);
            }

            if (string.IsNullOrEmpty(nextParentSessionId))
            {
                rootSessionId = currentSessionId;
                break;
            }

            currentSessionId = nextParentSessionId;
            parentDepth += 1;
        }

        return new SubagentSpawnContext
        {
            RootSessionId = rootSessionId,
            ParentDepth = parentDepth,
            ChildDepth = parentDepth + 1,
        };
    }

    public static Exception CreateSubagentDepthLimitError(int childDepth, int maxDepth, string parentSessionId, string rootSessionId)
    {
        return new InvalidOperationException(
            $"Subagent spawn blocked: child depth {childDepth} exceeds background_task.maxDepth={maxDepth}. Parent session: {parentSessionId}. Root session: {rootSessionId}. Continue in an existing subagent session instead of spawning another.");
    }
}
