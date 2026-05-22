namespace Omodot.Utils;

public sealed record AbortErrorInfo(string? Name, string? Message);

public static class IsAbortError
{
    public static bool Check(Exception? error)
    {
        if (error is null)
        {
            return false;
        }

        return Check(new AbortErrorInfo(error.GetType().Name, error.Message));
    }

    public static bool Check(string? error)
    {
        if (string.IsNullOrEmpty(error))
        {
            return false;
        }

        var lower = error.ToLowerInvariant();
        return lower.Contains("abort", StringComparison.Ordinal) || lower.Contains("cancel", StringComparison.Ordinal) || lower.Contains("interrupt", StringComparison.Ordinal);
    }

    public static bool Check(AbortErrorInfo? error)
    {
        if (error is null)
        {
            return false;
        }

        var name = error.Name;
        var message = error.Message?.ToLowerInvariant() ?? string.Empty;

        if (name is "MessageAbortedError" or "AbortError")
        {
            return true;
        }

        if (name == "DOMException" && message.Contains("abort", StringComparison.Ordinal))
        {
            return true;
        }

        return message.Contains("aborted", StringComparison.Ordinal)
            || message.Contains("cancelled", StringComparison.Ordinal)
            || message.Contains("canceled", StringComparison.Ordinal)
            || message.Contains("interrupted", StringComparison.Ordinal);
    }
}
