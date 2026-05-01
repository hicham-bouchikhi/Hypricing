using System.Globalization;
using Hypricing.Core.Infrastructure;

namespace Hypricing.Core.Services;

public sealed record NetworkDevice(
    string Name,
    string Type,
    string State,
    string? Connection,
    string? IpAddress);

public sealed record WifiNetwork(
    string Ssid,
    int Signal,
    string Security,
    bool Active);

public sealed class NetworkService(CliRunner cli)
{
    public async Task<NetworkDevice[]> GetDevicesAsync(CancellationToken ct = default)
    {
        string output;
        try
        {
            output = await cli.RunAsync("nmcli", "-t -f DEVICE,TYPE,STATE,CONNECTION device status", ct);
        }
        catch
        {
            return [];
        }

        var devices = new List<NetworkDevice>();
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = line.Split(':', 4);
            if (parts.Length < 3) continue;

            var type = parts[1];
            if (type is "loopback" or "bridge" or "tun") continue;

            var name = parts[0];
            var state = parts[2];
            var connection = parts.Length > 3 && parts[3] != "--" ? parts[3] : null;

            string? ip = null;
            if (state == "connected")
                ip = await GetIpAsync(name, ct);

            devices.Add(new NetworkDevice(name, type, state, connection, ip));
        }

        return [.. devices];
    }

    public async Task<WifiNetwork[]> GetWifiNetworksAsync(CancellationToken ct = default)
    {
        string output;
        try
        {
            output = await cli.RunAsync("nmcli", "--escape no -t -f SSID,SIGNAL,SECURITY,IN-USE device wifi list", ct);
        }
        catch
        {
            return [];
        }

        var networks = new List<WifiNetwork>();
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = line.Split(':');
            if (parts.Length < 4) continue;

            var inUse = parts[^1];
            var security = parts[^2];
            var signalStr = parts[^3];
            var ssid = string.Join(":", parts[..^3]);

            if (string.IsNullOrEmpty(ssid)) continue;
            if (!int.TryParse(signalStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var signal)) continue;

            networks.Add(new WifiNetwork(ssid, signal, security, inUse == "*"));
        }

        // Active network first, then descending signal
        networks.Sort((a, b) =>
        {
            if (a.Active != b.Active) return a.Active ? -1 : 1;
            return b.Signal.CompareTo(a.Signal);
        });

        return [.. networks];
    }

    public async Task<bool> GetWifiEnabledAsync(CancellationToken ct = default)
    {
        try
        {
            var output = await cli.RunAsync("nmcli", "radio wifi", ct);
            return output.Trim().StartsWith("enabled", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public async Task SetWifiEnabledAsync(bool enable, CancellationToken ct = default) =>
        await cli.RunAsync("nmcli", $"radio wifi {(enable ? "on" : "off")}", ct);

    public async Task ConnectAsync(string ssid, CancellationToken ct = default) =>
        await cli.RunAsync("nmcli", $"device wifi connect \"{ssid}\"", ct);

    public async Task DisconnectAsync(string device, CancellationToken ct = default) =>
        await cli.RunAsync("nmcli", $"device disconnect {device}", ct);

    public async Task ScanAsync(CancellationToken ct = default)
    {
        try
        {
            await cli.RunAsync("nmcli", "device wifi rescan", ct);
        }
        catch
        {
            // rescan exits non-zero when already scanning — ignore
        }
    }

    private async Task<string?> GetIpAsync(string device, CancellationToken ct)
    {
        try
        {
            var output = await cli.RunAsync("nmcli", $"-t -f IP4.ADDRESS device show {device}", ct);
            foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var sep = line.IndexOf(':', StringComparison.Ordinal);
                if (sep < 0) continue;
                var value = line[(sep + 1)..].Trim();
                if (!string.IsNullOrEmpty(value))
                    return value;
            }
        }
        catch { }
        return null;
    }
}
