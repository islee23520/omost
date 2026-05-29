namespace Lfe.Tmux;

public static class CmuxDetect
{
    public static bool IsCmuxCompatEnvironment(IReadOnlyDictionary<string, string?>? environment = null)
    {
        var source = environment ?? TmuxEnvironment.CaptureCurrentEnvironment();
        source.TryGetValue("TMUX", out var tmuxEnvironment);
        source.TryGetValue("CMUX_SOCKET_PATH", out var cmuxSocketPath);

        return tmuxEnvironment?.Contains("cmuxterm", StringComparison.Ordinal) == true
            || (!string.IsNullOrEmpty(cmuxSocketPath) && string.IsNullOrEmpty(tmuxEnvironment));
    }
}
