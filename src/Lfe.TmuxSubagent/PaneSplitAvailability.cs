namespace Lfe.TmuxSubagent;

public static class PaneSplitAvailability
{
    private static int GetMinSplitWidth(int? minPaneWidth = null) =>
        2 * Math.Max(1, minPaneWidth ?? PaneConstants.MinPaneWidth) + TmuxGridConstants.DividerSize;

    public static int GetColumnCount(int paneCount) =>
        paneCount <= 0 ? 1 : Math.Min(TmuxGridConstants.MaxCols, Math.Max(1, (int)Math.Ceiling((double)paneCount / TmuxGridConstants.MaxRows)));

    public static int GetColumnWidth(int agentAreaWidth, int paneCount)
    {
        var cols = GetColumnCount(paneCount);
        var dividersWidth = (cols - 1) * TmuxGridConstants.DividerSize;
        return (int)Math.Floor((double)(agentAreaWidth - dividersWidth) / cols);
    }

    public static bool IsSplittableAtCount(int agentAreaWidth, int paneCount, int? minPaneWidth = null) =>
        GetColumnWidth(agentAreaWidth, paneCount) >= GetMinSplitWidth(minPaneWidth);

    public static int? FindMinimalEvictions(int agentAreaWidth, int currentCount, int? minPaneWidth = null)
    {
        for (var k = 1; k <= currentCount; k++)
        {
            if (IsSplittableAtCount(agentAreaWidth, currentCount - k, minPaneWidth))
                return k;
        }
        return null;
    }

    public static bool CanSplitPane(TmuxPaneInfo pane, SplitDirection direction, int? minPaneWidth = null) =>
        direction == SplitDirection.Horizontal
            ? pane.Width >= GetMinSplitWidth(minPaneWidth)
            : pane.Height >= TmuxGridConstants.MinSplitHeight;

    public static bool CanSplitPaneAnyDirection(TmuxPaneInfo pane, int? minPaneWidth = null) =>
        pane.Width >= GetMinSplitWidth(minPaneWidth) || pane.Height >= TmuxGridConstants.MinSplitHeight;

    public static SplitDirection? GetBestSplitDirection(TmuxPaneInfo pane, int? minPaneWidth = null)
    {
        var canH = pane.Width >= GetMinSplitWidth(minPaneWidth);
        var canV = pane.Height >= TmuxGridConstants.MinSplitHeight;

        if (!canH && !canV) return null;
        if (canH && !canV) return SplitDirection.Horizontal;
        if (!canH && canV) return SplitDirection.Vertical;
        return pane.Width >= pane.Height ? SplitDirection.Horizontal : SplitDirection.Vertical;
    }
}
