namespace Omodot.BackgroundAgent;

public static class BackgroundAgentConstants
{
    public const int TaskTtlMs = 30 * 60 * 1000;
    public const int TerminalTaskTtlMs = 30 * 60 * 1000;
    public const int MinStabilityTimeMs = 10 * 1000;
    public const int DefaultStaleTimeoutMs = 2_700_000;
    public const int DefaultMessageStalenessTimeoutMs = 3_600_000;
    public const int DefaultMaxToolCalls = 4000;
    public const int DefaultCircuitBreakerConsecutiveThreshold = 20;
    public const bool DefaultCircuitBreakerEnabled = true;
    public const int MinRuntimeBeforeStaleMs = 30_000;
    public const int DefaultSessionGoneTimeoutMs = 60_000;
    public const int MinIdleTimeMs = 5000;
    public const int PollingIntervalMs = 3000;
    public const int TaskCleanupDelayMs = 10 * 60 * 1000;
    public const int TmuxCallbackDelayMs = 200;
    public const int DefaultWaitForSessionTimeoutMs = 30_000;
    public const int DefaultWaitForSessionIntervalMs = 100;
    public const int DefaultMaxSubagentDepth = 3;
    public const string SessionNextEventPrefix = "session.next.";
}

public static class BackgroundTaskStatuses
{
    public const string Pending = "pending";
    public const string Running = "running";
    public const string Completed = "completed";
    public const string Error = "error";
    public const string Cancelled = "cancelled";
    public const string Interrupt = "interrupt";

    public static IReadOnlyList<string> Terminal { get; } =
        [Completed, Error, Cancelled, Interrupt];
}

public static class SessionStatusClassifier
{
    public static IReadOnlyList<string> ActiveSessionStatuses { get; } = ["busy", "retry", "running"];
    public static IReadOnlyList<string> KnownTerminalStatuses { get; } = ["idle", "interrupted"];

    public static bool IsActiveSessionStatus(string type, Action<string>? onUnknownStatus = null)
    {
        if (ActiveSessionStatuses.Contains(type, StringComparer.Ordinal))
        {
            return true;
        }

        if (!KnownTerminalStatuses.Contains(type, StringComparer.Ordinal))
        {
            onUnknownStatus?.Invoke(type);
        }

        return false;
    }

    public static bool IsTerminalSessionStatus(string type)
    {
        return KnownTerminalStatuses.Contains(type, StringComparer.Ordinal) &&
               !string.Equals(type, "idle", StringComparison.Ordinal);
    }
}

public static class BackgroundTaskHistoryConstants
{
    public const int MaxTaskHistoryEntriesPerParent = 100;
}

public static class BackgroundTaskRegistryConstants
{
    public const int MaxCompletedTaskRegistrySize = 100;
}
