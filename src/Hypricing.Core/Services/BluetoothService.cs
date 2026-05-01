using System.Globalization;
using Hypricing.Core.Infrastructure;

namespace Hypricing.Core.Services;

public sealed record BluetoothDevice(
    string Address,
    string Name,
    string Icon,
    bool Paired,
    bool Connected,
    bool Trusted,
    int? BatteryPercent);

public sealed class BluetoothService(CliRunner cli)
{
    public async Task<BluetoothDevice[]> GetDevicesAsync(CancellationToken ct = default)
    {
        string output;
        try
        {
            output = await cli.RunAsync("bluetoothctl", "devices", ct);
        }
        catch
        {
            return [];
        }

        var addresses = new List<(string Address, string Name)>();
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2 && parts[0] == "Device")
                addresses.Add((parts[1], parts.Length > 2 ? string.Join(" ", parts[2..]) : parts[1]));
        }

        var devices = new List<BluetoothDevice>(addresses.Count);
        foreach (var (address, fallbackName) in addresses)
        {
            var device = await GetDeviceInfoAsync(address, fallbackName, ct);
            if (device is not null)
                devices.Add(device);
        }

        return [.. devices];
    }

    public async Task ConnectAsync(string address, CancellationToken ct = default) =>
        await cli.RunAsync("bluetoothctl", $"connect {address}", ct);

    public async Task DisconnectAsync(string address, CancellationToken ct = default) =>
        await cli.RunAsync("bluetoothctl", $"disconnect {address}", ct);

    public async Task RemoveAsync(string address, CancellationToken ct = default) =>
        await cli.RunAsync("bluetoothctl", $"remove {address}", ct);

    public async Task ScanAsync(CancellationToken ct = default)
    {
        try
        {
            await cli.RunAsync("bluetoothctl", "--timeout 5 scan on", ct);
        }
        catch
        {
            // scan exits non-zero on some versions — ignore
        }
    }

    private async Task<BluetoothDevice?> GetDeviceInfoAsync(string address, string fallbackName, CancellationToken ct)
    {
        string info;
        try
        {
            info = await cli.RunAsync("bluetoothctl", $"info {address}", ct);
        }
        catch
        {
            return new BluetoothDevice(address, fallbackName, string.Empty, false, false, false, null);
        }

        return ParseDeviceInfo(address, fallbackName, info);
    }

    private static BluetoothDevice ParseDeviceInfo(string address, string fallbackName, string info)
    {
        string name = fallbackName;
        string icon = string.Empty;
        bool paired = false, connected = false, trusted = false;
        int? battery = null;

        foreach (var raw in info.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var line = raw.Trim();
            var sep = line.IndexOf(": ", StringComparison.Ordinal);
            if (sep < 0) continue;

            var key = line[..sep];
            var value = line[(sep + 2)..];

            switch (key)
            {
                case "Name":
                case "Alias":
                    if (key == "Name") name = value;
                    break;
                case "Icon":
                    icon = value;
                    break;
                case "Paired":
                    paired = value == "yes";
                    break;
                case "Connected":
                    connected = value == "yes";
                    break;
                case "Trusted":
                    trusted = value == "yes";
                    break;
                case "Battery Percentage":
                    battery = ParseBattery(value);
                    break;
            }
        }

        return new BluetoothDevice(address, name, icon, paired, connected, trusted, battery);
    }

    private static int? ParseBattery(string value)
    {
        // Format: "0x4b (75)"
        var open = value.IndexOf('(');
        var close = value.IndexOf(')');
        if (open < 0 || close <= open) return null;
        var digits = value[(open + 1)..close];
        return int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : null;
    }
}
