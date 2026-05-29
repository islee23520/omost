namespace Lfe.TmuxSubagent;

public sealed record GridCapacity(int Cols, int Rows, int Total);
public sealed record GridSlot(int Row, int Col);
public sealed record GridPlan(int Cols, int Rows, int SlotWidth, int SlotHeight);

public static class GridPlanning
{
    private static int ResolveMinPaneWidth(CapacityConfig? config) =>
        config is not null && config.AgentPaneWidth > 0
            ? Math.Max(1, config.AgentPaneWidth)
            : PaneConstants.MinPaneWidth;

    private static int ResolveAgentAreaWidth(int windowWidth, CapacityConfig? config) =>
        TmuxGridConstants.ComputeAgentAreaWidth(windowWidth, config);

    public static GridCapacity CalculateCapacity(int windowWidth, int windowHeight, CapacityConfig? config = null, int? mainPaneWidth = null)
    {
        var availableWidth = mainPaneWidth.HasValue
            ? Math.Max(0, windowWidth - mainPaneWidth.Value - TmuxGridConstants.DividerSize)
            : ResolveAgentAreaWidth(windowWidth, config);
        var minPaneWidth = ResolveMinPaneWidth(config);

        var cols = Math.Min(TmuxGridConstants.MaxGridSize,
            Math.Max(0, (int)Math.Floor((double)(availableWidth + TmuxGridConstants.DividerSize) / (minPaneWidth + TmuxGridConstants.DividerSize))));
        var rows = Math.Min(TmuxGridConstants.MaxGridSize,
            Math.Max(0, (int)Math.Floor((double)(windowHeight + TmuxGridConstants.DividerSize) / (PaneConstants.MinPaneHeight + TmuxGridConstants.DividerSize))));

        return new GridCapacity(cols, rows, cols * rows);
    }

    public static GridPlan ComputeGridPlan(int windowWidth, int windowHeight, int paneCount, CapacityConfig? config = null, int? mainPaneWidth = null)
    {
        var capacity = CalculateCapacity(windowWidth, windowHeight, config, mainPaneWidth);
        var (maxCols, maxRows, _) = capacity;

        if (maxCols == 0 || maxRows == 0 || paneCount == 0)
            return new GridPlan(1, 1, 0, 0);

        var bestCols = 1;
        var bestRows = 1;
        var bestArea = int.MaxValue;

        for (var rows = 1; rows <= maxRows; rows++)
        {
            for (var cols = 1; cols <= maxCols; cols++)
            {
                if (cols * rows < paneCount) continue;
                var area = cols * rows;
                if (area < bestArea || (area == bestArea && rows < bestRows))
                {
                    bestCols = cols;
                    bestRows = rows;
                    bestArea = area;
                }
            }
        }

        var availableWidth = mainPaneWidth.HasValue
            ? Math.Max(0, windowWidth - mainPaneWidth.Value - TmuxGridConstants.DividerSize)
            : ResolveAgentAreaWidth(windowWidth, config);
        var slotWidth = (int)Math.Floor((double)availableWidth / bestCols);
        var slotHeight = (int)Math.Floor((double)windowHeight / bestRows);

        return new GridPlan(bestCols, bestRows, slotWidth, slotHeight);
    }

    public static GridSlot MapPaneToSlot(TmuxPaneInfo pane, GridPlan plan, int mainPaneWidth)
    {
        var relativeX = Math.Max(0, pane.Left - mainPaneWidth);
        var col = plan.SlotWidth > 0 ? Math.Min(plan.Cols - 1, (int)Math.Floor((double)relativeX / plan.SlotWidth)) : 0;
        var row = plan.SlotHeight > 0 ? Math.Min(plan.Rows - 1, (int)Math.Floor((double)pane.Top / plan.SlotHeight)) : 0;
        return new GridSlot(row, col);
    }
}
