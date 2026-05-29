using System.Net.Http;

namespace Lfe.Tmux;

public sealed class ServerHealthState
{
    public bool? ServerAvailable { get; set; }
    public string? ServerCheckUrl { get; set; }
    public bool ServerRunningInProcess { get; set; }
}

public sealed record ServerHealthCheckOptions(ServerHealthState? State = null, int TimeoutMs = 3000, int MaxAttempts = 2);

public sealed record ServerHealthDependencies(
    Func<Uri, CancellationToken, Task<bool>>? FetchAsync = null,
    Func<TimeSpan, CancellationToken, Task>? DelayAsync = null);

public static class ServerHealth
{
    private static readonly HttpClient HttpClient = new();
    private static readonly object SyncLock = new();
    private static bool? _serverAvailable;
    private static string? _serverCheckUrl;
    private static bool _serverRunningInProcess;

    public static void MarkServerRunningInProcess()
    {
        lock (SyncLock)
        {
            _serverRunningInProcess = true;
        }
    }

    public static ServerHealthState CreateServerHealthStateForTesting()
        => new()
        {
            ServerAvailable = null,
            ServerCheckUrl = null,
            ServerRunningInProcess = false,
        };

    public static async Task<bool> IsServerRunningAsync(
        string serverUrl,
        ServerHealthCheckOptions? options = null,
        ServerHealthDependencies? dependencies = null,
        CancellationToken cancellationToken = default)
    {
        var state = options?.State;
        if ((state?.ServerRunningInProcess ?? ReadMarkedRunningInProcess()) == true)
        {
            return true;
        }

        var cachedUrl = state?.ServerCheckUrl ?? ReadServerCheckUrl();
        var cachedAvailable = state?.ServerAvailable ?? ReadServerAvailable();
        if (string.Equals(cachedUrl, serverUrl, StringComparison.Ordinal) && cachedAvailable == true)
        {
            return true;
        }

        var healthUri = new Uri(new Uri(serverUrl, UriKind.Absolute), "/global/health");
        var fetchAsync = dependencies?.FetchAsync ?? DefaultFetchAsync;
        var delayAsync = dependencies?.DelayAsync ?? ((TimeSpan delay, CancellationToken ct) => Task.Delay(delay, ct));
        var timeoutMs = options?.TimeoutMs ?? 3000;
        var maxAttempts = options?.MaxAttempts ?? 2;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(timeoutMs);

            try
            {
                if (await fetchAsync(healthUri, timeoutCts.Token))
                {
                    if (state is not null)
                    {
                        state.ServerCheckUrl = serverUrl;
                        state.ServerAvailable = true;
                    }
                    else
                    {
                        WriteCachedServerCheck(serverUrl, true);
                    }

                    return true;
                }
            }
            catch
            {
            }

            if (attempt < maxAttempts)
            {
                await delayAsync(TimeSpan.FromMilliseconds(250), cancellationToken);
            }
        }

        return false;
    }

    public static void ResetServerCheck()
    {
        lock (SyncLock)
        {
            _serverAvailable = null;
            _serverCheckUrl = null;
            _serverRunningInProcess = false;
        }
    }

    private static async Task<bool> DefaultFetchAsync(Uri healthUri, CancellationToken cancellationToken)
    {
        var response = await HttpClient.GetAsync(healthUri, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    private static bool? ReadServerAvailable()
    {
        lock (SyncLock)
        {
            return _serverAvailable;
        }
    }

    private static string? ReadServerCheckUrl()
    {
        lock (SyncLock)
        {
            return _serverCheckUrl;
        }
    }

    private static bool ReadMarkedRunningInProcess()
    {
        lock (SyncLock)
        {
            return _serverRunningInProcess;
        }
    }

    private static void WriteCachedServerCheck(string serverUrl, bool available)
    {
        lock (SyncLock)
        {
            _serverCheckUrl = serverUrl;
            _serverAvailable = available;
        }
    }
}
