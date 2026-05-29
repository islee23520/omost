using Lfe.RulesEngine;

namespace Lfe.AgentsMd;

public static class AgentsMdFormatter
{
    public static string FormatAgentsMdContextBlock(string agentsPath, string content, bool truncated)
    {
        var truncationNotice = truncated
            ? $"{AgentsMdConstants.TruncationNoticePrefix}{agentsPath}{AgentsMdConstants.TruncationNoticeSuffix}"
            : "";
        return $"\n\n[Directory Context: {agentsPath}]\n{content}{truncationNotice}";
    }

    public static string? ResolveFilePath(string rootDirectory, string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        if (Path.IsPathRooted(path)) return path;
        return Path.GetFullPath(Path.Join(rootDirectory, path));
    }
}
