namespace Omodot.Tmux;

public static class TmuxConstants
{
    public const int PollIntervalBackgroundMs = 2000;
    public const int SessionTimeoutMs = 60 * 60 * 1000;
    public const int SessionMissingGraceMs = 30 * 1000;
    public const int SessionReadyPollIntervalMs = 500;
    public const int SessionReadyTimeoutMs = 10_000;
}
