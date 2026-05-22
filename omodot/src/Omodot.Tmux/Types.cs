namespace Omodot.Tmux;

public sealed record SpawnPaneResult(bool Success, string? PaneId = null);

public enum TmuxLayout
{
    EvenHorizontal,
    EvenVertical,
    MainHorizontal,
    MainVertical,
    Tiled,
}

public enum TmuxIsolationMode
{
    Inline,
    Window,
    Session,
}

public sealed record TmuxConfig(
    bool Enabled,
    TmuxLayout? Layout = null,
    int? MainPaneSize = null,
    int? MainPaneMinWidth = null,
    int? AgentPaneMinWidth = null,
    TmuxIsolationMode? Isolation = null);

public static class TmuxLayoutExtensions
{
    public static string ToTmuxValue(this TmuxLayout layout) => layout switch
    {
        TmuxLayout.EvenHorizontal => "even-horizontal",
        TmuxLayout.EvenVertical => "even-vertical",
        TmuxLayout.MainHorizontal => "main-horizontal",
        TmuxLayout.MainVertical => "main-vertical",
        TmuxLayout.Tiled => "tiled",
        _ => throw new ArgumentOutOfRangeException(nameof(layout), layout, null),
    };
}
