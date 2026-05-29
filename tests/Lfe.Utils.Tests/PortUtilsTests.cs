using System.Net;
using System.Net.Sockets;

namespace Lfe.Utils.Tests;

public sealed class PortUtilsTests
{
    [Fact]
    public async Task IsPortAvailableAsync_returns_true_for_released_port()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();

        Assert.True(await PortUtils.IsPortAvailableAsync(port));
    }

    [Fact]
    public async Task FindAvailablePortAsync_finds_next_port()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        var found = await PortUtils.FindAvailablePortAsync(port);
        listener.Stop();

        Assert.True(found >= port + 1);
    }

    [Fact]
    public async Task GetAvailableServerPortAsync_reports_auto_selection()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        var result = await PortUtils.GetAvailableServerPortAsync(port);
        listener.Stop();

        Assert.True(result.WasAutoSelected);
        Assert.True(result.Port >= port + 1);
    }
}
