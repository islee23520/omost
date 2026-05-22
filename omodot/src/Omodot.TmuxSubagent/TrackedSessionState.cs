namespace Omodot.TmuxSubagent;

public static class TrackedSessionState
{
    public static TrackedSession CreateTrackedSession(string sessionId, string paneId, string description, DateTimeOffset? now = null)
    {
        var timestamp = now ?? DateTimeOffset.UtcNow;
        return new TrackedSession(
            SessionId: sessionId,
            PaneId: paneId,
            Description: description,
            AttachActivated: false,
            AttachActivatedAt: null,
            CreatedAt: timestamp,
            LastSeenAt: timestamp,
            ClosePending: false,
            CloseRetryCount: 0,
            ActivityVersion: 0);
    }

    public static TrackedSession MarkClosePending(TrackedSession tracked) => tracked with
    {
        ClosePending = true,
        CloseRetryCount = tracked.ClosePending ? tracked.CloseRetryCount + 1 : tracked.CloseRetryCount
    };
}
