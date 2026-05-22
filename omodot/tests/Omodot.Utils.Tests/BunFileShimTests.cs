namespace Omodot.Utils.Tests;

public sealed class BunFileShimTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), $"omodot-utils-bunfile-{Guid.NewGuid():N}");

    public BunFileShimTests()
    {
        Directory.CreateDirectory(_tempDirectory);
    }

    [Fact]
    public async Task BunFile_reads_and_deletes_file()
    {
        var filePath = Path.Combine(_tempDirectory, "file.txt");
        await File.WriteAllTextAsync(filePath, "hello");
        var file = BunFileShim.BunFile(filePath);
        Assert.True(await file.ExistsAsync());
        Assert.Equal("hello", await file.TextAsync());
        await file.DeleteAsync();
        Assert.False(await file.ExistsAsync());
    }

    [Fact]
    public async Task BunWriteAsync_writes_string_and_bytes()
    {
        var textPath = Path.Combine(_tempDirectory, "text.txt");
        var bytesPath = Path.Combine(_tempDirectory, "bytes.bin");
        Assert.Equal(5, await BunFileShim.BunWriteAsync(textPath, "hello"));
        Assert.Equal("hello", await File.ReadAllTextAsync(textPath));
        Assert.Equal(3, await BunFileShim.BunWriteAsync(bytesPath, new byte[] { 1, 2, 3 }));
        Assert.Equal([1, 2, 3], await File.ReadAllBytesAsync(bytesPath));
    }

    public void Dispose()
    {
        Directory.Delete(_tempDirectory, true);
    }
}
