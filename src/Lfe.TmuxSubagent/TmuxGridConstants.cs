namespace Lfe.TmuxSubagent;

public static class TmuxGridConstants
{
    public const double MainPaneRatio = 0.5;
    private const double DefaultMainPaneSize = MainPaneRatio * 100;
    public const int MaxCols = 2;
    public const int MaxRows = 3;
    public const int MaxGridSize = 4;
    public const int DividerSize = 1;
    public const int MinSplitWidth = 2 * PaneConstants.MinPaneWidth + DividerSize;
    public const int MinSplitHeight = 2 * PaneConstants.MinPaneHeight + DividerSize;

    public static double Clamp(double value, double min, double max) =>
        Math.Max(min, Math.Min(max, value));

    public static double GetMainPaneSizePercent(CapacityConfig? config = null) =>
        Clamp(config?.MainPaneSize ?? DefaultMainPaneSize, 20, 80);

    public static int ComputeMainPaneWidth(int windowWidth, CapacityConfig? config = null)
    {
        var safeWidth = Math.Max(0, windowWidth);
        if (config is null)
            return (int)Math.Floor(safeWidth * MainPaneRatio);

        var minMainPaneWidth = config.MainPaneMinWidth == 0
            ? (int)Math.Floor(safeWidth * MainPaneRatio)
            : config.MainPaneMinWidth;
        var minAgentPaneWidth = config.AgentPaneWidth == 0 ? PaneConstants.MinPaneWidth : config.AgentPaneWidth;
        var percentageWidth = (int)Math.Floor((safeWidth - DividerSize) * (GetMainPaneSizePercent(config) / 100));
        var maxMainPaneWidth = Math.Max(0, safeWidth - DividerSize - minAgentPaneWidth);

        return (int)Clamp(Math.Max(percentageWidth, minMainPaneWidth), 0, maxMainPaneWidth);
    }

    public static int ComputeAgentAreaWidth(int windowWidth, CapacityConfig? config = null)
    {
        var safeWidth = Math.Max(0, windowWidth);
        if (config is null)
            return (int)Math.Floor(safeWidth * (1 - MainPaneRatio));

        var mainPaneWidth = ComputeMainPaneWidth(safeWidth, config);
        return Math.Max(0, safeWidth - DividerSize - mainPaneWidth);
    }
}
