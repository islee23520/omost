namespace Lfe.GitWorktree;

public static class ParseStatusPorcelainLine
{
    public static ParsedGitStatusPorcelainLine? Parse(string line)
    {
        if (string.IsNullOrEmpty(line)) return null;
        var statusToken = line.Length >= 2 ? line[..2].Trim() : line.Trim();
        var filePath = line.Length > 3 ? line[3..] : "";
        if (string.IsNullOrEmpty(filePath)) return null;
        return new ParsedGitStatusPorcelainLine(filePath, ToGitFileStatus(statusToken));
    }

    private static GitFileStatus ToGitFileStatus(string token) => token switch
    {
        "A" or "??" => GitFileStatus.Added,
        "D" => GitFileStatus.Deleted,
        _ => GitFileStatus.Modified
    };
}
