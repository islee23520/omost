namespace Lfe.TmuxSubagent;

public sealed record SpawnTarget(string TargetPaneId, SplitDirection SplitDirection);

public static class SpawnActionDecider
{
    private static SplitDirection GetInitialSplitDirection(string? layout) =>
        layout == "main-horizontal" ? SplitDirection.Vertical : SplitDirection.Horizontal;

    private static bool IsStrictMainVertical(CapacityConfig config) =>
        config.Layout == "main-vertical";

    private static bool IsStrictMainHorizontal(CapacityConfig config) =>
        config.Layout == "main-horizontal";

    private static bool IsStrictMainLayout(string? layout) =>
        layout == "main-vertical" || layout == "main-horizontal";

    private static SplitDirection GetStrictFollowupSplitDirection(CapacityConfig config) =>
        IsStrictMainHorizontal(config) ? SplitDirection.Horizontal : SplitDirection.Vertical;

    private static List<TmuxPaneInfo> SortPanesForStrictLayout(IReadOnlyList<TmuxPaneInfo> panes, CapacityConfig config) =>
        IsStrictMainHorizontal(config)
            ? panes.OrderBy(p => p.Left).ThenBy(p => p.Top).ToList()
            : panes.OrderBy(p => p.Top).ThenBy(p => p.Left).ToList();

    private static Dictionary<string, TmuxPaneInfo> BuildOccupancy(IReadOnlyList<TmuxPaneInfo> agentPanes, GridPlan plan, int mainPaneWidth)
    {
        var occupancy = new Dictionary<string, TmuxPaneInfo>();
        foreach (var pane in agentPanes)
        {
            var slot = GridPlanning.MapPaneToSlot(pane, plan, mainPaneWidth);
            occupancy[$"{slot.Row}:{slot.Col}"] = pane;
        }
        return occupancy;
    }

    private static (int Row, int Col) FindFirstEmptySlot(Dictionary<string, TmuxPaneInfo> occupancy, GridPlan plan)
    {
        for (var row = 0; row < plan.Rows; row++)
        {
            for (var col = 0; col < plan.Cols; col++)
            {
                if (!occupancy.ContainsKey($"{row}:{col}"))
                    return (row, col);
            }
        }
        return (plan.Rows - 1, plan.Cols - 1);
    }

    public static SpawnTarget? FindSpawnTarget(WindowState state, CapacityConfig config)
    {
        if (state.MainPane is null) return null;
        var existingCount = state.AgentPanesValue.Count;
        var minAgentPaneWidth = config.AgentPaneWidth > 0 ? (int?)config.AgentPaneWidth : null;
        var initialDirection = GetInitialSplitDirection(config.Layout);

        if (existingCount == 0)
        {
            var virtualMainPane = state.MainPane with { Width = state.WindowWidth };
            return PaneSplitAvailability.CanSplitPane(virtualMainPane, initialDirection, minAgentPaneWidth)
                ? new SpawnTarget(state.MainPane.PaneId, initialDirection)
                : null;
        }

        if (IsStrictMainLayout(config.Layout))
        {
            var followupDirection = GetStrictFollowupSplitDirection(config);
            var panesByPriority = SortPanesForStrictLayout(state.AgentPanesValue, config);
            foreach (var pane in panesByPriority)
            {
                if (PaneSplitAvailability.CanSplitPane(pane, followupDirection, minAgentPaneWidth))
                    return new SpawnTarget(pane.PaneId, followupDirection);
            }
            return null;
        }

        var plan = GridPlanning.ComputeGridPlan(state.WindowWidth, state.WindowHeight, existingCount + 1, config);
        var mainPaneWidth = TmuxGridConstants.ComputeMainPaneWidth(state.WindowWidth, config);
        var occupancy = BuildOccupancy(state.AgentPanesValue, plan, mainPaneWidth);
        var (targetRow, targetCol) = FindFirstEmptySlot(occupancy, plan);

        if (!IsStrictMainVertical(config) && occupancy.TryGetValue($"{targetRow}:{targetCol - 1}", out var leftPane)
            && PaneSplitAvailability.CanSplitPane(leftPane, SplitDirection.Horizontal, minAgentPaneWidth))
        {
            return new SpawnTarget(leftPane.PaneId, SplitDirection.Horizontal);
        }

        if (occupancy.TryGetValue($"{targetRow - 1}:{targetCol}", out var abovePane)
            && PaneSplitAvailability.CanSplitPane(abovePane, SplitDirection.Vertical, minAgentPaneWidth))
        {
            return new SpawnTarget(abovePane.PaneId, SplitDirection.Vertical);
        }

        var panesByPosition = state.AgentPanesValue.OrderBy(p => p.Left).ThenBy(p => p.Top).ToList();

        foreach (var pane in panesByPosition)
        {
            if (PaneSplitAvailability.CanSplitPane(pane, SplitDirection.Vertical, minAgentPaneWidth))
                return new SpawnTarget(pane.PaneId, SplitDirection.Vertical);
        }

        if (IsStrictMainVertical(config)) return null;

        foreach (var pane in panesByPosition)
        {
            if (PaneSplitAvailability.CanSplitPane(pane, SplitDirection.Horizontal, minAgentPaneWidth))
                return new SpawnTarget(pane.PaneId, SplitDirection.Horizontal);
        }

        return null;
    }

    public static SpawnDecision DecideSpawnActions(WindowState state, string sessionId, string description, CapacityConfig config, IReadOnlyList<SessionMapping> sessionMappings)
    {
        if (state.MainPane is null)
            return new SpawnDecision(false, [], "no main pane found");

        var agentAreaWidth = TmuxGridConstants.ComputeAgentAreaWidth(state.WindowWidth, config);
        var minAgentPaneWidth = config.AgentPaneWidth > 0 ? (int?)config.AgentPaneWidth : null;
        var currentCount = state.AgentPanesValue.Count;
        var strictLayout = IsStrictMainLayout(config.Layout);
        var initialSplitDirection = GetInitialSplitDirection(config.Layout);

        if (agentAreaWidth < (minAgentPaneWidth ?? PaneConstants.MinPaneWidth) && currentCount > 0)
            return new SpawnDecision(false, [], $"window too small for agent panes: {state.WindowWidth}x{state.WindowHeight}");

        var oldestPane = OldestAgentPane.FindOldestAgentPane(state.AgentPanesValue, sessionMappings);
        var oldestMapping = oldestPane is not null
            ? sessionMappings.FirstOrDefault(m => m.PaneId == oldestPane.PaneId)
            : null;

        if (currentCount == 0)
        {
            var virtualMainPane = state.MainPane with { Width = state.WindowWidth };
            if (PaneSplitAvailability.CanSplitPane(virtualMainPane, initialSplitDirection, minAgentPaneWidth))
            {
                return new SpawnDecision(true, [
                    new PaneAction.Spawn(sessionId, description, state.MainPane.PaneId, initialSplitDirection)
                ]);
            }
            return new SpawnDecision(false, [], "mainPane too small to split");
        }

        var canEvaluateSpawnTarget = strictLayout || PaneSplitAvailability.IsSplittableAtCount(agentAreaWidth, currentCount, minAgentPaneWidth);

        if (canEvaluateSpawnTarget)
        {
            var spawnTarget = FindSpawnTarget(state, config);
            if (spawnTarget is not null)
            {
                return new SpawnDecision(true, [
                    new PaneAction.Spawn(sessionId, description, spawnTarget.TargetPaneId, spawnTarget.SplitDirection)
                ]);
            }
        }

        if (!strictLayout)
        {
            var minEvictions = PaneSplitAvailability.FindMinimalEvictions(agentAreaWidth, currentCount, minAgentPaneWidth);
            if (minEvictions == 1 && oldestPane is not null)
            {
                return new SpawnDecision(true, [
                    new PaneAction.Close(oldestPane.PaneId, oldestMapping?.SessionId ?? ""),
                    new PaneAction.Spawn(sessionId, description, state.MainPane.PaneId, initialSplitDirection)
                ], "closed 1 pane to make room for split");
            }
        }

        return new SpawnDecision(false, [], "no split target available (defer attach)");
    }

    public static PaneAction.Close? DecideCloseAction(WindowState state, string sessionId, IReadOnlyList<SessionMapping> sessionMappings)
    {
        var mapping = sessionMappings.FirstOrDefault(m => m.SessionId == sessionId);
        if (mapping is null) return null;

        var paneExists = state.AgentPanesValue.Any(p => p.PaneId == mapping.PaneId);
        if (!paneExists) return null;

        return new PaneAction.Close(mapping.PaneId, sessionId);
    }
}
