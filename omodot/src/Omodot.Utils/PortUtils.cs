using System.Net;
using System.Net.Sockets;

namespace Omodot.Utils;

public sealed record AutoPortResult(int Port, bool WasAutoSelected);

public static class PortUtils
{
    public const int DefaultServerPort = 4096;
    private const int MaxPortAttempts = 20;

    public static async Task<bool> IsPortAvailableAsync(int port, string hostname = "127.0.0.1")
    {
        try
        {
            var address = await ResolveAddressAsync(hostname).ConfigureAwait(false);
            var listener = new TcpListener(address, port);
            listener.Start();
            listener.Stop();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static async Task<int> FindAvailablePortAsync(int startPort = DefaultServerPort, string hostname = "127.0.0.1")
    {
        for (var attempt = 0; attempt < MaxPortAttempts; attempt++)
        {
            var port = startPort + attempt;
            if (await IsPortAvailableAsync(port, hostname).ConfigureAwait(false))
            {
                return port;
            }
        }

        throw new InvalidOperationException($"No available port found in range {startPort}-{startPort + MaxPortAttempts - 1}");
    }

    public static async Task<AutoPortResult> GetAvailableServerPortAsync(int preferredPort = DefaultServerPort, string hostname = "127.0.0.1")
    {
        if (await IsPortAvailableAsync(preferredPort, hostname).ConfigureAwait(false))
        {
            return new AutoPortResult(preferredPort, false);
        }

        var port = await FindAvailablePortAsync(preferredPort + 1, hostname).ConfigureAwait(false);
        return new AutoPortResult(port, true);
    }

    private static async Task<IPAddress> ResolveAddressAsync(string hostname)
    {
        if (IPAddress.TryParse(hostname, out var parsed))
        {
            return parsed;
        }

        var addresses = await Dns.GetHostAddressesAsync(hostname).ConfigureAwait(false);
        return addresses.First(address => address.AddressFamily == AddressFamily.InterNetwork || address.AddressFamily == AddressFamily.InterNetworkV6);
    }
}
