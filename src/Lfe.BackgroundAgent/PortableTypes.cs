namespace Lfe.BackgroundAgent;

public static class ProcessCleanupEvents
{
    public const string BeforeExit = "beforeExit";
    public const string Exit = "exit";

    public static IReadOnlyList<string> LifecycleEvents { get; } =
    [
        BeforeExit,
        Exit,
    ];
}

public static class BackgroundTaskNotificationStatuses
{
    public const string Completed = "COMPLETED";
    public const string Cancelled = "CANCELLED";
    public const string Interrupted = "INTERRUPTED";
    public const string Error = "ERROR";

    public static IReadOnlyList<string> All { get; } =
    [
        Completed,
        Cancelled,
        Interrupted,
        Error,
    ];
}
