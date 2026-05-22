namespace Omodot.SearchTools;

public static class ProcessStreamReader
{
    public static async Task<string> ReadProcessStreamAsync(ProcessReadableStream? stream)
    {
        if (stream?.Reader is null)
        {
            return string.Empty;
        }

        return await stream.Reader.ReadToEndAsync().ConfigureAwait(false);
    }
}
