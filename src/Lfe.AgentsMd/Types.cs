namespace Lfe.AgentsMd;

public sealed record TruncationResult(string Result, bool Truncated);

public interface IAgentsMdTruncator
{
    Task<TruncationResult> Truncate(string sessionId, string content);
}

public sealed record AgentsMdContextOutput(string Title, string Output, object? Metadata = null)
{
    public string Output { get; set; } = Output;
}

public interface IAgentsMdInjectedPathsStorage
{
    HashSet<string> LoadInjectedPaths(string sessionId);
    void SaveInjectedPaths(string sessionId, HashSet<string> paths);
}
