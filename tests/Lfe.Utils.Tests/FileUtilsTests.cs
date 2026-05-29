namespace Lfe.Utils.Tests;

public sealed class FileUtilsTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), $"lfe-utils-file-{Guid.NewGuid():N}");

    public FileUtilsTests()
    {
        Directory.CreateDirectory(_tempDirectory);
    }

    [Fact]
    public void IsMarkdownFile_accepts_visible_markdown_files()
    {
        Assert.True(FileUtils.IsMarkdownFile(new FileSystemEntry("README.md", true)));
    }

    [Fact]
    public void IsSymbolicLink_detects_symlink()
    {
        var target = Path.Combine(_tempDirectory, "target.txt");
        var link = Path.Combine(_tempDirectory, "link.txt");
        File.WriteAllText(target, "hello");
        File.CreateSymbolicLink(link, target);

        Assert.True(FileUtils.IsSymbolicLink(link));
    }

    [Fact]
    public void ResolveSymlink_resolves_link_target()
    {
        var target = Path.Combine(_tempDirectory, "target.txt");
        var link = Path.Combine(_tempDirectory, "link.txt");
        File.WriteAllText(target, "hello");
        File.CreateSymbolicLink(link, target);

        Assert.Equal(Path.GetFullPath(target), FileUtils.ResolveSymlink(link));
    }

    [Fact]
    public async Task ResolveSymlinkAsync_resolves_link_target()
    {
        var target = Path.Combine(_tempDirectory, "target-async.txt");
        var link = Path.Combine(_tempDirectory, "link-async.txt");
        File.WriteAllText(target, "hello");
        File.CreateSymbolicLink(link, target);

        Assert.Equal(Path.GetFullPath(target), await FileUtils.ResolveSymlinkAsync(link));
    }

    public void Dispose()
    {
        Directory.Delete(_tempDirectory, true);
    }
}
