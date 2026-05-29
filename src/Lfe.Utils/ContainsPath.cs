namespace Lfe.Utils;

public static class ContainsPath
{
    public static bool Check(string rootPath, string candidatePath)
    {
        var canonicalRootPath = ToCanonicalPath(rootPath);
        var canonicalCandidatePath = ToCanonicalPath(candidatePath);
        var relativePath = Path.GetRelativePath(canonicalRootPath, canonicalCandidatePath);

        return relativePath == "."
            || relativePath.Length == 0
            || (!relativePath.StartsWith("..", StringComparison.Ordinal) && !Path.IsPathRooted(relativePath));
    }

    public static bool IsWithinProject(string candidatePath, string projectRoot) => Check(projectRoot, candidatePath);

    private static string ToCanonicalPath(string pathToNormalize)
    {
        var resolvedPath = Path.GetFullPath(pathToNormalize);
        if (File.Exists(resolvedPath) || Directory.Exists(resolvedPath))
        {
            return NormalizePath(ResolveExistingPath(resolvedPath));
        }

        var nearestExistingAncestor = FindNearestExistingAncestor(resolvedPath);
        var canonicalAncestor = File.Exists(nearestExistingAncestor) || Directory.Exists(nearestExistingAncestor)
            ? ResolveExistingPath(nearestExistingAncestor)
            : nearestExistingAncestor;
        var relativePath = Path.GetRelativePath(nearestExistingAncestor, resolvedPath);

        return NormalizePath(Path.Combine(canonicalAncestor, relativePath == "." ? Path.GetFileName(resolvedPath) : relativePath));
    }

    private static string FindNearestExistingAncestor(string resolvedPath)
    {
        var candidatePath = resolvedPath;

        while (!File.Exists(candidatePath) && !Directory.Exists(candidatePath))
        {
            var parentPath = Path.GetDirectoryName(candidatePath);
            if (string.IsNullOrEmpty(parentPath) || string.Equals(parentPath, candidatePath, StringComparison.Ordinal))
            {
                break;
            }

            candidatePath = parentPath;
        }

        return candidatePath;
    }

    private static string ResolveExistingPath(string path)
    {
        var root = Path.GetPathRoot(path) ?? string.Empty;
        var current = root.Length == 0 ? Directory.GetCurrentDirectory() : root;
        var relativePath = root.Length == 0 ? path : path[root.Length..];
        var segments = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);

        foreach (var segment in segments)
        {
            var candidate = Path.Combine(current, segment);
            if (!File.Exists(candidate) && !Directory.Exists(candidate))
            {
                current = candidate;
                continue;
            }

            FileSystemInfo info = Directory.Exists(candidate) ? new DirectoryInfo(candidate) : new FileInfo(candidate);
            current = info.ResolveLinkTarget(true)?.FullName ?? info.FullName;
        }

        return current;
    }

    private static string NormalizePath(string path) => Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
}
