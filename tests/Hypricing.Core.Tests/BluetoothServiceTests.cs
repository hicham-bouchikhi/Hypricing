using Hypricing.Core.Infrastructure;
using Hypricing.Core.Services;

namespace Hypricing.Core.Tests;

public class BluetoothServiceTests
{
    [Fact]
    public async Task GetDevicesAsync_ParsesPairedConnectedDevice()
    {
        var cli = new FakeCliRunner(new()
        {
            [("bluetoothctl", "devices")] = "Device AA:BB:CC:DD:EE:FF My Headphones\n",
            [("bluetoothctl", "info AA:BB:CC:DD:EE:FF")] =
                "Name: My Headphones\n\tIcon: audio-headphones\n\tPaired: yes\n\tTrusted: yes\n\tConnected: yes\n\tBattery Percentage: 0x4b (75)\n",
        });
        var service = new BluetoothService(cli);

        var devices = await service.GetDevicesAsync();

        Assert.Single(devices);
        Assert.Equal("AA:BB:CC:DD:EE:FF", devices[0].Address);
        Assert.Equal("My Headphones", devices[0].Name);
        Assert.Equal("audio-headphones", devices[0].Icon);
        Assert.True(devices[0].Paired);
        Assert.True(devices[0].Connected);
        Assert.True(devices[0].Trusted);
        Assert.Equal(75, devices[0].BatteryPercent);
    }

    [Fact]
    public async Task GetDevicesAsync_ParsesUnpairedDevice()
    {
        var cli = new FakeCliRunner(new()
        {
            [("bluetoothctl", "devices")] = "Device 11:22:33:44:55:66 Speaker\n",
            [("bluetoothctl", "info 11:22:33:44:55:66")] =
                "Name: Speaker\n\tIcon: audio-speakers\n\tPaired: no\n\tTrusted: no\n\tConnected: no\n",
        });
        var service = new BluetoothService(cli);

        var devices = await service.GetDevicesAsync();

        Assert.Single(devices);
        Assert.False(devices[0].Paired);
        Assert.False(devices[0].Connected);
        Assert.Null(devices[0].BatteryPercent);
    }

    [Fact]
    public async Task GetDevicesAsync_ParsesMultipleDevices()
    {
        var cli = new FakeCliRunner(new()
        {
            [("bluetoothctl", "devices")] =
                "Device AA:BB:CC:DD:EE:FF Headphones\nDevice 11:22:33:44:55:66 Keyboard\n",
            [("bluetoothctl", "info AA:BB:CC:DD:EE:FF")] = "Name: Headphones\n\tPaired: yes\n\tConnected: yes\n\tTrusted: no\n",
            [("bluetoothctl", "info 11:22:33:44:55:66")] = "Name: Keyboard\n\tPaired: yes\n\tConnected: no\n\tTrusted: yes\n",
        });
        var service = new BluetoothService(cli);

        var devices = await service.GetDevicesAsync();

        Assert.Equal(2, devices.Length);
        Assert.Equal("Headphones", devices[0].Name);
        Assert.Equal("Keyboard", devices[1].Name);
    }

    [Fact]
    public async Task GetDevicesAsync_ReturnsEmptyOnCliFailure()
    {
        var cli = new ThrowingCliRunner();
        var service = new BluetoothService(cli);

        var devices = await service.GetDevicesAsync();

        Assert.Empty(devices);
    }

    [Fact]
    public async Task GetDevicesAsync_ParsesBatteryPercentage()
    {
        var cli = new FakeCliRunner(new()
        {
            [("bluetoothctl", "devices")] = "Device CC:DD:EE:FF:AA:BB Speaker\n",
            [("bluetoothctl", "info CC:DD:EE:FF:AA:BB")] =
                "Name: Speaker\n\tPaired: yes\n\tConnected: no\n\tTrusted: no\n\tBattery Percentage: 0x32 (50)\n",
        });
        var service = new BluetoothService(cli);

        var devices = await service.GetDevicesAsync();

        Assert.Equal(50, devices[0].BatteryPercent);
    }

    [Fact]
    public async Task ConnectAsync_SendsCorrectCommand()
    {
        var cli = new RecordingCliRunner();
        var service = new BluetoothService(cli);

        await service.ConnectAsync("AA:BB:CC:DD:EE:FF");

        Assert.Single(cli.Invocations);
        Assert.Equal(("bluetoothctl", "connect AA:BB:CC:DD:EE:FF"), cli.Invocations[0]);
    }

    [Fact]
    public async Task DisconnectAsync_SendsCorrectCommand()
    {
        var cli = new RecordingCliRunner();
        var service = new BluetoothService(cli);

        await service.DisconnectAsync("AA:BB:CC:DD:EE:FF");

        Assert.Single(cli.Invocations);
        Assert.Equal(("bluetoothctl", "disconnect AA:BB:CC:DD:EE:FF"), cli.Invocations[0]);
    }

    [Fact]
    public async Task RemoveAsync_SendsCorrectCommand()
    {
        var cli = new RecordingCliRunner();
        var service = new BluetoothService(cli);

        await service.RemoveAsync("AA:BB:CC:DD:EE:FF");

        Assert.Single(cli.Invocations);
        Assert.Equal(("bluetoothctl", "remove AA:BB:CC:DD:EE:FF"), cli.Invocations[0]);
    }

    [Fact]
    public async Task ScanAsync_DoesNotThrowOnCliFailure()
    {
        var cli = new ThrowingCliRunner();
        var service = new BluetoothService(cli);

        await service.ScanAsync(); // must not throw
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
