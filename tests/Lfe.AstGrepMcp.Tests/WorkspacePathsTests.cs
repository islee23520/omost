namespace Lfe.AstGrepMcp.Tests;

public sealed class WorkspacePathsTests
{
    [Fact]
    public void NormalizeWorkspaceDirectory_ResolvesFullPath()
    {
        var tmp = Path.GetTempPath();
        var path = Path.Join(tmp, ".");
        var result = WorkspacePaths.NormalizeWorkspaceDirectory(path);
        // Path.GetFullPath removes trailing separator on macOS; compare normalized
        Assert.Equal(Path.GetFullPath(tmp).TrimEnd(Path.DirectorySeparatorChar), result.TrimEnd(Path.DirectorySeparatorChar));
    }

    [Fact]
    public void ResolveWorkspacePaths_DefaultsToDot_WhenNull()
    {
        var tmp = Path.GetTempPath();
        var result = WorkspacePaths.ResolveWorkspacePaths(null, tmp);
        Assert.Equal(["."], result);
    }

    [Fact]
    public void ResolveWorkspacePaths_DefaultsToDot_WhenEmpty()
    {
        var tmp = Path.GetTempPath();
        var result = WorkspacePaths.ResolveWorkspacePaths([], tmp);
        Assert.Equal(["."], result);
    }

    [Fact]
    public void ResolveWorkspacePaths_RejectsEmptyString()
    {
        var tmp = Path.GetTempPath();
        var ex = Assert.Throws<ArgumentException>(() => WorkspacePaths.ResolveWorkspacePaths([""], tmp));
        Assert.Contains("non-empty", ex.Message);
    }

    [Fact]
    public void ResolveWorkspacePaths_RejectsFlagPrefix()
    {
        var tmp = Path.GetTempPath();
        var ex = Assert.Throws<ArgumentException>(() => WorkspacePaths.ResolveWorkspacePaths(["-flag"], tmp));
        Assert.Contains("must not start with '-'", ex.Message);
    }

    [Fact]
    public void ResolveWorkspacePaths_RejectsNullBytes()
    {
        var tmp = Path.GetTempPath();
        var ex = Assert.Throws<ArgumentException>(() => WorkspacePaths.ResolveWorkspacePaths(["bad\0path"], tmp));
        Assert.Contains("null bytes", ex.Message);
    }

    [Fact]
    public void ResolveWorkspacePaths_RejectsTraversal()
    {
        var tmp = Path.GetTempPath();
        var ex = Assert.Throws<ArgumentException>(() => WorkspacePaths.ResolveWorkspacePaths(["../outside"], tmp));
        Assert.Contains("stay inside the workspace", ex.Message);
    }

    [Fact]
    public void ResolveWorkspacePaths_RejectsAbsolutePathOutsideWorkspace()
    {
        var tmp = Path.GetTempPath();
        var ex = Assert.Throws<ArgumentException>(() => WorkspacePaths.ResolveWorkspacePaths(["/definitely/not/here"], tmp));
        Assert.Contains("does not exist", ex.Message);
    }

    [Fact]
    public void ResolveWorkspacePaths_ResolvesRelativePath()
    {
        var tmp = Path.GetTempPath();
        var result = WorkspacePaths.ResolveWorkspacePaths(["src"], tmp);
        Assert.Equal(["src"], result);
    }
}
