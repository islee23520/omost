namespace Omodot.TmuxSubagent;

public sealed record ActionResult(bool Success, string? PaneId = null, string? Error = null);

public sealed record ActionExecutorConfig(int? MainPaneSize = null);

public sealed record ExecuteContext(
    ActionExecutorConfig Config,
    string Directory,
    string ServerUrl,
    WindowState WindowState);

public sealed record PaneOperationResult(bool Success, string? PaneId = null);

public interface IActionExecutorDeps
{
    Task<PaneOperationResult> SpawnTmuxPaneAsync(string sessionId, string description, ActionExecutorConfig config, string serverUrl, string directory, string targetPaneId, SplitDirection splitDirection);
    Task<bool> CloseTmuxPaneAsync(string paneId);
    Task<PaneOperationResult> ReplaceTmuxPaneAsync(string paneId, string newSessionId, string description, ActionExecutorConfig config, string serverUrl, string directory);
    Task EnforceMainPaneWidthAsync(string paneId, int windowWidth, int? mainPaneSize);
}

public static class ActionExecutorCore
{
    public static async Task<ActionResult> ExecuteActionAsync(PaneAction action, ExecuteContext ctx, IActionExecutorDeps deps)
    {
        if (action is PaneAction.Close close)
        {
            var success = await deps.CloseTmuxPaneAsync(close.PaneId).ConfigureAwait(false);
            if (success && ctx.WindowState.MainPane is not null)
            {
                await EnforceMainPaneAsync(ctx.WindowState, ctx.Config, deps).ConfigureAwait(false);
            }
            return new ActionResult(success);
        }

        if (action is PaneAction.Replace replace)
        {
            var result = await deps.ReplaceTmuxPaneAsync(
                replace.PaneId, replace.NewSessionId, replace.Description,
                ctx.Config, ctx.ServerUrl, ctx.Directory).ConfigureAwait(false);
            return new ActionResult(result.Success, result.PaneId);
        }

        if (action is PaneAction.Spawn spawn)
        {
            var result = await deps.SpawnTmuxPaneAsync(
                spawn.SessionId, spawn.Description, ctx.Config, ctx.ServerUrl, ctx.Directory,
                spawn.TargetPaneId, spawn.SplitDirection).ConfigureAwait(false);

            if (result.Success && ctx.WindowState.MainPane is not null)
            {
                await EnforceMainPaneAsync(ctx.WindowState, ctx.Config, deps).ConfigureAwait(false);
            }

            return new ActionResult(result.Success, result.PaneId);
        }

        return new ActionResult(false, Error: "Unknown action type");
    }

    private static async Task EnforceMainPaneAsync(WindowState windowState, ActionExecutorConfig config, IActionExecutorDeps deps)
    {
        if (windowState.MainPane is null) return;
        await deps.EnforceMainPaneWidthAsync(
            windowState.MainPane.PaneId,
            windowState.WindowWidth,
            config.MainPaneSize).ConfigureAwait(false);
    }
}
