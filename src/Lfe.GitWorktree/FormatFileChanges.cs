namespace Lfe.GitWorktree;

public static class FormatFileChanges
{
    public static string Format(List<GitFileStat> stats, string? notepadPath = null)
    {
        if (stats.Count == 0) return "[FILE CHANGES SUMMARY]\nNo file changes detected.\n";
        var lines = new List<string> { "[FILE CHANGES SUMMARY]" };
        var modified = stats.Where(s => s.Status == GitFileStatus.Modified).ToList();
        var added = stats.Where(s => s.Status == GitFileStatus.Added).ToList();
        var deleted = stats.Where(s => s.Status == GitFileStatus.Deleted).ToList();

        if (modified.Count > 0) { lines.Add("Modified files:"); lines.AddRange(modified.Select(f => $"  {f.Path}  (+{f.Added}, -{f.Removed})")); lines.Add(""); }
        if (added.Count > 0) { lines.Add("Created files:"); lines.AddRange(added.Select(f => $"  {f.Path}  (+{f.Added})")); lines.Add(""); }
        if (deleted.Count > 0) { lines.Add("Deleted files:"); lines.AddRange(deleted.Select(f => $"  {f.Path}  (-{f.Removed})")); lines.Add(""); }

        if (notepadPath is not null)
        {
            var normalizedNotepad = notepadPath.Replace('\\', '/');
            var match = stats.FirstOrDefault(s => s.Path.Replace('\\', '/') == normalizedNotepad);
            if (match is not null) { lines.Add("[NOTEPAD UPDATED]"); lines.Add($"  {match.Path}  (+{match.Added})"); lines.Add(""); }
        }
        return string.Join("\n", lines);
    }
}
