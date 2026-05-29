namespace Lfe.BackgroundAgent;

public static class DurationFormatter
{
    public static string FormatDuration(DateTime start, DateTime? end = null)
    {
        var duration = (end ?? DateTime.UtcNow) - start;
        var seconds = (int)Math.Floor(duration.TotalSeconds);
        var minutes = seconds / 60;
        var hours = minutes / 60;

        if (hours > 0)
        {
            return $"{hours}h {minutes % 60}m {seconds % 60}s";
        }

        if (minutes > 0)
        {
            return $"{minutes}m {seconds % 60}s";
        }

        return $"{seconds}s";
    }
}
