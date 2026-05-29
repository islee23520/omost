namespace Lfe.TmuxSubagent;

public static class PollingConstants
{
    public const int SessionTimeoutMs = 10 * 60 * 1000;
    public const int MinStabilityTimeMs = 10 * 1000;
    public const int StablePollsRequired = 3;
}
