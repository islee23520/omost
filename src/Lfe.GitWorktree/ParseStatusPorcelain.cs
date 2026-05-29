namespace Lfe.GitWorktree;

public static class ParseStatusPorcelain
{
    public static Dictionary<string, GitFileStatus> Parse(string output)
    {
        var map = new Dictionary<string, GitFileStatus>();
        if (string.IsNullOrEmpty(output)) return map;
        foreach (var line in output.Split('\n'))
        {
            var parsed = ParseStatusPorcelainLine.Parse(line);
            if (parsed is not null) map[parsed.FilePath] = parsed.Status;
        }
        return map;
    }
}
