namespace Omodot.CommandExecutor;

public static class ShellPathFinder
{
    private static readonly string[] DefaultZshPaths = ["/bin/zsh", "/usr/bin/zsh", "/usr/local/bin/zsh"];
    private static readonly string[] DefaultBashPaths = ["/bin/bash", "/usr/bin/bash", "/usr/local/bin/bash"];

    public static string? FindZshPath(string? customZshPath = null)
    {
        return FindShellPath(DefaultZshPaths, customZshPath);
    }

    public static string? FindBashPath()
    {
        return FindShellPath(DefaultBashPaths);
    }

    private static string? FindShellPath(IEnumerable<string> defaultPaths, string? customPath = null)
    {
        if (!string.IsNullOrWhiteSpace(customPath) && File.Exists(customPath))
        {
            return customPath;
        }

        foreach (var path in defaultPaths)
        {
            if (File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }
}
