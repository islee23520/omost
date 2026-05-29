namespace Lfe.RulesEngine;

public sealed record RuleMetadata(
    string? Description = null,
    string[]? Globs = null,
    string[]? Paths = null,
    string[]? ApplyTo = null,
    bool AlwaysApply = false);

public sealed record RuleFrontmatterResult(RuleMetadata Metadata, string Body);

public sealed record RuleFileCandidate(
    string Path,
    string RealPath,
    bool IsGlobal,
    int Distance,
    string RelativePath,
    string Source,
    bool IsSingleFile = false);

public sealed record MatchResult(bool Applies, string? Reason = null);

public sealed record DirectoryScanEntry(string Path, string RealPath, string RelativePath);

public sealed record RuleScanCacheStats(int CandidateEntries, int DirectoryEntries);

public interface IRuleScanCache
{
    IReadOnlyList<RuleFileCandidate>? Get(string key);
    void Set(string key, IReadOnlyList<RuleFileCandidate> value);
    IReadOnlyList<DirectoryScanEntry>? GetDirScan(string dir);
    void SetDirScan(string dir, IReadOnlyList<DirectoryScanEntry> entries);
    RuleScanCacheStats Stats();
    void Clear();
}

public interface IAgentsMdCache
{
    IReadOnlyList<string>? Get(string key);
    void Set(string key, IReadOnlyList<string> value);
    void Clear();
}

public sealed record FindRuleFilesOptions(bool SkipClaudeUserRules = false, string? WorkspaceDirectory = null);

public sealed record FindAgentsMdUpInput(string StartDir, string RootDir, bool SkipRoot = true, IAgentsMdCache? Cache = null);
