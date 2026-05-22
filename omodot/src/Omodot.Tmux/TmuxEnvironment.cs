using System.Collections;

namespace Omodot.Tmux;

public enum SplitDirection
{
    Horizontal,
    Vertical,
}

public static class SplitDirectionExtensions
{
    public static string ToTmuxArgument(this SplitDirection direction) => direction switch
    {
        SplitDirection.Horizontal => "-h",
        SplitDirection.Vertical => "-v",
        _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null),
    };
}

public static class TmuxEnvironment
{
    public static bool IsInsideTmuxEnvironment(IReadOnlyDictionary<string, string?> environment)
        => environment.TryGetValue("TMUX", out var tmux) && !string.IsNullOrEmpty(tmux);

    public static bool IsInsideTmux()
        => IsInsideTmuxEnvironment(CaptureCurrentEnvironment());

    public static string? GetCurrentPaneId(IReadOnlyDictionary<string, string?>? environment = null)
    {
        var source = environment ?? CaptureCurrentEnvironment();
        return source.TryGetValue("TMUX_PANE", out var paneId) ? paneId : null;
    }

    public static IReadOnlyDictionary<string, string?> CaptureCurrentEnvironment()
    {
        var values = Environment.GetEnvironmentVariables();
        var result = new Dictionary<string, string?>(StringComparer.Ordinal);

        foreach (DictionaryEntry entry in values)
        {
            if (entry.Key is string key)
            {
                result[key] = entry.Value?.ToString();
            }
        }

        return result;
    }
}
