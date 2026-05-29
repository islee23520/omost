namespace Lfe.AstGrepMcp;

public static class WorkspacePaths
{
    public static string NormalizeWorkspaceDirectory(string workspaceDirectory) =>
        Path.GetFullPath(workspaceDirectory);

    public static string[] ResolveWorkspacePaths(string[]? rawPaths, string workspaceDirectory)
    {
        var workspace = NormalizeWorkspaceDirectory(workspaceDirectory);
        var requested = rawPaths is { Length: > 0 } ? rawPaths : ["."];
        return requested.Select(p => ResolveWorkspacePath(p, workspace)).ToArray();
    }

    private static string ResolveWorkspacePath(string rawPath, string workspaceDirectory)
    {
        if (string.IsNullOrEmpty(rawPath)) throw new ArgumentException("paths entries must be non-empty strings");
        if (rawPath.StartsWith("-")) throw new ArgumentException($"paths entries must not start with '-': {rawPath}");
        if (rawPath.Contains('\0')) throw new ArgumentException("paths entries must not contain null bytes");

        if (Path.IsPathRooted(rawPath))
        {
            if (!File.Exists(rawPath) && !Directory.Exists(rawPath))
                throw new ArgumentException($"absolute path entry does not exist: {rawPath}");
            var relative = Path.GetRelativePath(workspaceDirectory, Path.GetFullPath(rawPath));
            return relative == "" ? "." : relative;
        }

        var absolutePath = Path.GetFullPath(Path.Join(workspaceDirectory, rawPath));
        AssertInsideWorkspace(absolutePath, workspaceDirectory, rawPath);
        var normalizedPath = Path.GetRelativePath(workspaceDirectory, absolutePath);
        return normalizedPath == "" ? "." : normalizedPath;
    }

    private static void AssertInsideWorkspace(string candidatePath, string workspaceDirectory, string rawPath)
    {
        var relative = Path.GetRelativePath(workspaceDirectory, candidatePath);
        if (relative == "" || (!relative.StartsWith("..") && !Path.IsPathRooted(relative))) return;
        throw new ArgumentException($"paths entries must stay inside the workspace: {rawPath}");
    }
}
