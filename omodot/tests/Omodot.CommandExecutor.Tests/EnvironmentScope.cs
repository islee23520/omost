using System.Collections;
using System.Collections.Concurrent;

namespace Omodot.CommandExecutor.Tests;

internal static class EnvironmentScope
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public static async Task<T> RunAsync<T>(Func<Task<T>> action)
    {
        await Gate.WaitAsync().ConfigureAwait(false);

        var snapshot = Capture();
        try
        {
            return await action().ConfigureAwait(false);
        }
        finally
        {
            Restore(snapshot);
            Gate.Release();
        }
    }

    private static IReadOnlyDictionary<string, string?> Capture()
    {
        var snapshot = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            snapshot[(string)entry.Key] = entry.Value?.ToString();
        }

        return snapshot;
    }

    private static void Restore(IReadOnlyDictionary<string, string?> snapshot)
    {
        foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            Environment.SetEnvironmentVariable((string)entry.Key, null);
        }

        foreach (var entry in snapshot)
        {
            Environment.SetEnvironmentVariable(entry.Key, entry.Value);
        }
    }
}
