namespace Lfe.Tmux;

public static class TmuxLogger
{
    private static readonly object SyncLock = new();
    private static Action<string, object?>? _sink;

    public static void Log(string message, object? data = null)
    {
        Action<string, object?>? sink;
        lock (SyncLock)
        {
            sink = _sink;
        }

        sink?.Invoke(message, data);
    }

    public static void SetSinkForTesting(Action<string, object?>? sink)
    {
        lock (SyncLock)
        {
            _sink = sink;
        }
    }

    public static void ResetForTesting() => SetSinkForTesting(null);
}
