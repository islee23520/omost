namespace Lfe.Utils;

public sealed record FileSystemEntry(string Name, bool IsFile);

public static class FileUtils
{
    public static bool IsMarkdownFile(FileSystemEntry entry) => !entry.Name.StartsWith(".", StringComparison.Ordinal) && entry.Name.EndsWith(".md", StringComparison.Ordinal) && entry.IsFile;

    public static bool IsSymbolicLink(string filePath)
    {
        try
        {
            return File.GetAttributes(filePath).HasFlag(FileAttributes.ReparsePoint);
        }
        catch
        {
            return false;
        }
    }

    public static string ResolveSymlink(string filePath)
    {
        try
        {
            var fullPath = Path.GetFullPath(filePath);
            if (!File.Exists(fullPath) && !Directory.Exists(fullPath))
            {
                return filePath;
            }

            var info = Directory.Exists(fullPath) ? new DirectoryInfo(fullPath) as FileSystemInfo : new FileInfo(fullPath);
            var resolved = info.ResolveLinkTarget(true)?.FullName ?? info.FullName;
            return NormalizeDarwinRealpath(resolved);
        }
        catch
        {
            return filePath;
        }
    }

    public static Task<string> ResolveSymlinkAsync(string filePath) => Task.FromResult(ResolveSymlink(filePath));

    private static string NormalizeDarwinRealpath(string filePath) => filePath.StartsWith("/private/var/", StringComparison.Ordinal) ? filePath["/private".Length..] : filePath;
}
