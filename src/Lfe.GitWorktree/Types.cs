namespace Lfe.GitWorktree;

public enum GitFileStatus { Modified, Added, Deleted }

public sealed record GitFileStat(string Path, int Added, int Removed, GitFileStatus Status);

public sealed record ParsedGitStatusPorcelainLine(string FilePath, GitFileStatus Status);
