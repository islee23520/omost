namespace Lfe.GitWorktree;

public static class ParseDiffNumstat
{
    public static List<GitFileStat> Parse(string output, Dictionary<string, GitFileStatus> statusMap)
    {
        if (string.IsNullOrEmpty(output)) return [];
        var stats = new List<GitFileStat>();
        foreach (var line in output.Split('\n'))
        {
            var parts = line.Split('\t');
            if (parts.Length < 3) continue;
            var added = parts[0] == "-" ? 0 : int.TryParse(parts[0], out var a) ? a : 0;
            var removed = parts[1] == "-" ? 0 : int.TryParse(parts[1], out var r) ? r : 0;
            var path = parts[2];
            var status = statusMap.GetValueOrDefault(path, GitFileStatus.Modified);
            stats.Add(new GitFileStat(path, added, removed, status));
        }
        return stats;
    }
}
