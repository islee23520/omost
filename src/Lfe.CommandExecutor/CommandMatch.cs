namespace Lfe.CommandExecutor;

public sealed record CommandMatch(string FullMatch, string Command, int Start, int End);
