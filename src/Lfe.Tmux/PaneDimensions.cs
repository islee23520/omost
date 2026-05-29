namespace Lfe.Tmux;

public sealed record PaneDimensions(int PaneWidth, int WindowWidth);

public sealed record PaneDimensionsDependencies(
    Func<IReadOnlyDictionary<string, string?>?, CancellationToken, Task<string?>>? GetTmuxPathAsync = null,
    Func<string, IReadOnlyList<string>, RunTmuxOptions?, CancellationToken, Task<TmuxCommandResult>>? RunTmuxCommandAsync = null);

public static class PaneDimensionsReader
{
    public static async Task<PaneDimensions?> GetPaneDimensionsAsync(
        string paneId,
        PaneDimensionsDependencies? dependencies = null,
        CancellationToken cancellationToken = default)
    {
        var getTmuxPathAsync = dependencies?.GetTmuxPathAsync ?? TmuxPathResolver.GetTmuxPathAsync;
        var tmux = await getTmuxPathAsync(null, cancellationToken);
        if (string.IsNullOrEmpty(tmux))
        {
            return null;
        }

        var runTmuxCommandAsync = dependencies?.RunTmuxCommandAsync ?? TmuxRunner.RunTmuxCommandAsync;
        var result = await runTmuxCommandAsync(tmux, ["display", "-p", "-t", paneId, "#{pane_width},#{window_width}"], null, cancellationToken);
        if (result.ExitCode != 0)
        {
            return null;
        }

        var values = result.Output.Split(',', StringSplitOptions.TrimEntries);
        if (values.Length != 2 || !int.TryParse(values[0], out var paneWidth) || !int.TryParse(values[1], out var windowWidth))
        {
            return null;
        }

        return new PaneDimensions(paneWidth, windowWidth);
    }
}
