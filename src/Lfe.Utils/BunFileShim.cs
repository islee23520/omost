namespace Lfe.Utils;

public interface IUtilityFile
{
    Task<string> TextAsync();
    Task<byte[]> ReadBytesAsync();
    Task<bool> ExistsAsync();
    Task DeleteAsync();
}

public sealed record UtilityFile(string Path) : IUtilityFile
{
    public Task<string> TextAsync() => File.ReadAllTextAsync(Path);

    public Task<byte[]> ReadBytesAsync() => File.ReadAllBytesAsync(Path);

    public Task<bool> ExistsAsync() => Task.FromResult(File.Exists(Path));

    public Task DeleteAsync()
    {
        File.Delete(Path);
        return Task.CompletedTask;
    }
}

public static class BunFileShim
{
    public static IUtilityFile BunFile(string path) => new UtilityFile(path);

    public static async Task<long> BunWriteAsync(string path, string data)
    {
        await File.WriteAllTextAsync(path, data).ConfigureAwait(false);
        return System.Text.Encoding.UTF8.GetByteCount(data);
    }

    public static async Task<long> BunWriteAsync(string path, byte[] data)
    {
        await File.WriteAllBytesAsync(path, data).ConfigureAwait(false);
        return data.LongLength;
    }
}
