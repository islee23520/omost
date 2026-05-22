namespace Omodot.Tmux;

public static class TmuxPathResolver
{
    public static Task<string?> GetTmuxPathAsync(
        IReadOnlyDictionary<string, string?>? environment = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var source = environment ?? TmuxEnvironment.CaptureCurrentEnvironment();

        if (source.TryGetValue("TMUX_BIN", out var tmuxBin) && !string.IsNullOrWhiteSpace(tmuxBin))
        {
            return Task.FromResult<string?>(tmuxBin);
        }

        if (!source.TryGetValue("PATH", out var pathValue) || string.IsNullOrWhiteSpace(pathValue))
        {
            return Task.FromResult<string?>(null);
        }

        foreach (var directory in pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var candidate = Path.Combine(directory, "tmux");
                if (File.Exists(candidate))
                {
                    return Task.FromResult<string?>(candidate);
                }
            }
            catch
            {
            }
        }

        return Task.FromResult<string?>(null);
    }
}
