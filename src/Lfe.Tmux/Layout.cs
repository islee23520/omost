namespace Lfe.Tmux;

public sealed record MainPaneWidthOptions(int? MainPaneSize = null, int? MainPaneMinWidth = null, int? AgentPaneMinWidth = null);

public sealed record LayoutDependencies(
    Func<IReadOnlyList<string>, CancellationToken, Task<int>>? SpawnCommandAsync = null,
    Action<string, object?>? Log = null,
    Func<IReadOnlyDictionary<string, string?>?, CancellationToken, Task<string?>>? GetTmuxPathAsync = null,
    Func<string, IReadOnlyList<string>, RunTmuxOptions?, CancellationToken, Task<TmuxCommandResult>>? RunTmuxCommandAsync = null);

public static class Layout
{
    public static int CalculateMainPaneWidth(int windowWidth, MainPaneWidthOptions? options = null)
    {
        const int dividerWidth = 1;
        var sizePercent = Clamp(options?.MainPaneSize ?? 50, 20, 80);
        var minMainPaneWidth = options?.MainPaneMinWidth ?? 0;
        var minAgentPaneWidth = options?.AgentPaneMinWidth ?? 0;
        var desiredMainPaneWidth = (int)Math.Floor((windowWidth - dividerWidth) * (sizePercent / 100d));
        var maxMainPaneWidth = Math.Max(0, windowWidth - dividerWidth - minAgentPaneWidth);
        return Clamp(Math.Max(desiredMainPaneWidth, minMainPaneWidth), 0, maxMainPaneWidth);
    }

    public static async Task ApplyLayoutAsync(
        string tmux,
        TmuxLayout layout,
        int mainPaneSize,
        LayoutDependencies? dependencies = null,
        CancellationToken cancellationToken = default)
    {
        var spawnCommandAsync = dependencies?.SpawnCommandAsync ?? (async (args, ct) =>
        {
            var result = await TmuxRunner.RunTmuxCommandAsync(args[0], args.Skip(1).ToArray(), null, null, ct);
            return result.ExitCode;
        });

        await spawnCommandAsync([tmux, "select-layout", layout.ToTmuxValue()], cancellationToken);
        if (layout is not TmuxLayout.MainHorizontal and not TmuxLayout.MainVertical)
        {
            return;
        }

        var dimension = layout == TmuxLayout.MainHorizontal ? "main-pane-height" : "main-pane-width";
        await spawnCommandAsync([tmux, "set-window-option", dimension, $"{mainPaneSize}%"], cancellationToken);
    }

    public static Task EnforceMainPaneWidthAsync(
        string mainPaneId,
        int windowWidth,
        int mainPaneSize,
        LayoutDependencies? dependencies = null,
        CancellationToken cancellationToken = default)
        => EnforceMainPaneWidthAsync(mainPaneId, windowWidth, new MainPaneWidthOptions(MainPaneSize: mainPaneSize), dependencies, cancellationToken);

    public static async Task EnforceMainPaneWidthAsync(
        string mainPaneId,
        int windowWidth,
        MainPaneWidthOptions? options,
        LayoutDependencies? dependencies = null,
        CancellationToken cancellationToken = default)
    {
        var log = dependencies?.Log ?? TmuxLogger.Log;
        var getTmuxPathAsync = dependencies?.GetTmuxPathAsync ?? TmuxPathResolver.GetTmuxPathAsync;
        var runTmuxCommandAsync = dependencies?.RunTmuxCommandAsync ?? TmuxRunner.RunTmuxCommandAsync;
        var tmux = await getTmuxPathAsync(null, cancellationToken);
        if (string.IsNullOrEmpty(tmux))
        {
            return;
        }

        var mainWidth = CalculateMainPaneWidth(windowWidth, options);
        await runTmuxCommandAsync(tmux, ["resize-pane", "-t", mainPaneId, "-x", mainWidth.ToString()], null, cancellationToken);
        log("[enforceMainPaneWidth] main pane resized", new { mainPaneId, mainWidth, windowWidth, options?.MainPaneSize, options?.MainPaneMinWidth, options?.AgentPaneMinWidth });
    }

    private static int Clamp(int value, int min, int max) => Math.Max(min, Math.Min(max, value));
}
