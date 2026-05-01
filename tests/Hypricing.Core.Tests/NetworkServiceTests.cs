using Hypricing.Core.Infrastructure;
using Hypricing.Core.Services;

namespace Hypricing.Core.Tests;

public class NetworkServiceTests
{
    [Fact]
    public async Task GetDevicesAsync_ParsesConnectedDeviceWithIp()
    {
        var cli = new FakeCliRunner(new()
        {
            [("nmcli", "-t -f DEVICE,TYPE,STATE,CONNECTION device status")] =
                "wlan0:wifi:connected:HomeNetwork\n",
            [("nmcli", "-t -f IP4.ADDRESS device show wlan0")] =
                "IP4.ADDRESS[1]:192.168.1.100/24\n",
        });
        var service = new NetworkService(cli);

        var devices = await service.GetDevicesAsync();

        Assert.Single(devices);
        Assert.Equal("wlan0", devices[0].Name);
        Assert.Equal("wifi", devices[0].Type);
        Assert.Equal("connected", devices[0].State);
        Assert.Equal("HomeNetwork", devices[0].Connection);
        Assert.Equal("192.168.1.100/24", devices[0].IpAddress);
    }

    [Fact]
    public async Task GetDevicesAsync_FiltersLoopbackBridgeTun()
    {
        var cli = new FakeCliRunner(new()
        {
            [("nmcli", "-t -f DEVICE,TYPE,STATE,CONNECTION device status")] =
                "lo:loopback:unmanaged:--\nbr0:bridge:unmanaged:--\ntun0:tun:unmanaged:--\neth0:ethernet:disconnected:--\n",
        });
        var service = new NetworkService(cli);

        var devices = await service.GetDevicesAsync();

        Assert.Single(devices);
        Assert.Equal("eth0", devices[0].Name);
    }

    [Fact]
    public async Task GetDevicesAsync_NullConnectionWhenDash()
    {
        var cli = new FakeCliRunner(new()
        {
            [("nmcli", "-t -f DEVICE,TYPE,STATE,CONNECTION device status")] =
                "eth0:ethernet:disconnected:--\n",
        });
        var service = new NetworkService(cli);

        var devices = await service.GetDevicesAsync();

        Assert.Single(devices);
        Assert.Null(devices[0].Connection);
        Assert.Null(devices[0].IpAddress);
    }

    [Fact]
    public async Task GetDevicesAsync_ReturnsEmptyOnCliFailure()
    {
        var cli = new ThrowingCliRunner();
        var service = new NetworkService(cli);

        var devices = await service.GetDevicesAsync();

        Assert.Empty(devices);
    }

    [Fact]
    public async Task GetWifiNetworksAsync_ParsesNetworks()
    {
        var cli = new FakeCliRunner(new()
        {
            [("nmcli", "--escape no -t -f SSID,SIGNAL,SECURITY,IN-USE device wifi list")] =
                "HomeNetwork:80:WPA2:\nOfficeWifi:60:WPA2:\n",
        });
        var service = new NetworkService(cli);

        var networks = await service.GetWifiNetworksAsync();

        Assert.Equal(2, networks.Length);
        Assert.Equal("HomeNetwork", networks[0].Ssid);
        Assert.Equal(80, networks[0].Signal);
        Assert.Equal("WPA2", networks[0].Security);
        Assert.False(networks[0].Active);
    }

    [Fact]
    public async Task GetWifiNetworksAsync_ActiveNetworkSortedFirst()
    {
        var cli = new FakeCliRunner(new()
        {
            [("nmcli", "--escape no -t -f SSID,SIGNAL,SECURITY,IN-USE device wifi list")] =
                "WeakNetwork:30:WPA2:\nActiveNetwork:50:WPA2:*\nStrongNetwork:90:WPA2:\n",
        });
        var service = new NetworkService(cli);

        var networks = await service.GetWifiNetworksAsync();

        Assert.Equal(3, networks.Length);
        Assert.Equal("ActiveNetwork", networks[0].Ssid);
        Assert.True(networks[0].Active);
        Assert.Equal("StrongNetwork", networks[1].Ssid);
    }

    [Fact]
    public async Task GetWifiNetworksAsync_ParsesSsidWithColons()
    {
        var cli = new FakeCliRunner(new()
        {
            [("nmcli", "--escape no -t -f SSID,SIGNAL,SECURITY,IN-USE device wifi list")] =
                "My:Cool:Wifi:75:WPA2:\n",
        });
        var service = new NetworkService(cli);

        var networks = await service.GetWifiNetworksAsync();

        Assert.Single(networks);
        Assert.Equal("My:Cool:Wifi", networks[0].Ssid);
        Assert.Equal(75, networks[0].Signal);
    }

    [Fact]
    public async Task GetWifiEnabledAsync_ReturnsTrue()
    {
        var cli = new FakeCliRunner(new()
        {
            [("nmcli", "radio wifi")] = "enabled\n",
        });
        var service = new NetworkService(cli);

        Assert.True(await service.GetWifiEnabledAsync());
    }

    [Fact]
    public async Task GetWifiEnabledAsync_ReturnsFalse()
    {
        var cli = new FakeCliRunner(new()
        {
            [("nmcli", "radio wifi")] = "disabled\n",
        });
        var service = new NetworkService(cli);

        Assert.False(await service.GetWifiEnabledAsync());
    }

    [Fact]
    public async Task ConnectAsync_SendsCorrectCommand()
    {
        var cli = new RecordingCliRunner();
        var service = new NetworkService(cli);

        await service.ConnectAsync("HomeNetwork");

        Assert.Single(cli.Invocations);
        Assert.Equal(("nmcli", "device wifi connect \"HomeNetwork\""), cli.Invocations[0]);
    }

    [Fact]
    public async Task DisconnectAsync_SendsCorrectCommand()
    {
        var cli = new RecordingCliRunner();
        var service = new NetworkService(cli);

        await service.DisconnectAsync("wlan0");

        Assert.Single(cli.Invocations);
        Assert.Equal(("nmcli", "device disconnect wlan0"), cli.Invocations[0]);
    }

    private sealed class FakeCliRunner(Dictionary<(string, string), string> map) : CliRunner
    {
        public override Task<string> RunAsync(string command, string arguments, CancellationToken ct = default)
            => Task.FromResult(map.TryGetValue((command, arguments), out var r) ? r : string.Empty);
    }

    private sealed class RecordingCliRunner : CliRunner
    {
        public List<(string Command, string Arguments)> Invocations { get; } = [];

        public override Task<string> RunAsync(string command, string arguments, CancellationToken ct = default)
        {
            Invocations.Add((command, arguments));
            return Task.FromResult(string.Empty);
        }
    }

    private sealed class ThrowingCliRunner : CliRunner
    {
        public override Task<string> RunAsync(string command, string arguments, CancellationToken ct = default)
            => throw new InvalidOperationException("simulated failure");
    }
}
