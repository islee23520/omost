namespace Omodot.TmuxSubagent;

public sealed record TrackedSession(
    string SessionId,
    string PaneId,
    string Description,
    bool AttachActivated,
    DateTimeOffset? AttachActivatedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastSeenAt,
    bool ClosePending,
    int CloseRetryCount,
    int? LastMessageCount = null,
    int? StableIdlePolls = null,
    int? ActivityVersion = null,
    int? ObservedIdleActivityVersion = null);

public static class PaneConstants
{
    public const int MinPaneWidth = 52;
    public const int MinPaneHeight = 11;
}

public sealed record TmuxPaneInfo(
    string PaneId,
    int Width,
    int Height,
    int Left,
    int Top,
    string Title,
    bool IsActive);

public sealed record WindowState(
    int WindowWidth,
    int WindowHeight,
    bool? WindowActive = null,
    bool? SessionAttached = null,
    TmuxPaneInfo? MainPane = null,
    IReadOnlyList<TmuxPaneInfo>? AgentPanes = null)
{
    public IReadOnlyList<TmuxPaneInfo> AgentPanesValue => AgentPanes ?? [];
}

public enum SplitDirection
{
    Horizontal,
    Vertical,
}

public abstract record PaneAction
{
    public sealed record Close(string PaneId, string SessionId) : PaneAction;
    public sealed record Spawn(string SessionId, string Description, string TargetPaneId, SplitDirection SplitDirection) : PaneAction;
    public sealed record Replace(string PaneId, string OldSessionId, string NewSessionId, string Description) : PaneAction;
}

public sealed record SpawnDecision(
    bool CanSpawn,
    IReadOnlyList<PaneAction> Actions,
    string? Reason = null);

public sealed record CapacityConfig(
    string? Layout = null,
    int? MainPaneSize = null,
    int MainPaneMinWidth = PaneConstants.MinPaneWidth,
    int AgentPaneWidth = PaneConstants.MinPaneWidth);
