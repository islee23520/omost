namespace Omodot.Utils;

public static class FormatDuration
{
    public static string Human(long milliseconds)
    {
        var totalSeconds = Math.Max(0, milliseconds / 1000);
        var hours = totalSeconds / 3600;
        var minutes = (totalSeconds % 3600) / 60;
        var seconds = totalSeconds % 60;

        if (hours > 0)
        {
            return $"{hours}h {minutes}m {seconds}s";
        }

        if (minutes > 0)
        {
            return $"{minutes}m {seconds}s";
        }

        return $"{seconds}s";
    }
}
