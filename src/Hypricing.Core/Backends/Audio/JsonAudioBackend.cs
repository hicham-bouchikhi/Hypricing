using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Hypricing.Core.Contracts;
using Hypricing.Core.Infrastructure;

namespace Hypricing.Core.Backends.Audio;

/// <summary>
/// Generic audio backend driven entirely by a JSON preset.
/// Runs shell commands, parses JSON output using field mappings.
/// </summary>
public sealed class JsonAudioBackend(CliRunner cli, AudioPreset preset) : IAudioBackend
{
    public string PresetName => preset.Name;

    public async Task<IReadOnlyList<AudioDevice>> ListSinksAsync(CancellationToken ct = default)
    {
        var cmd = preset.Commands.ListSinks;
        var json = await RunJsonCommandAsync(cmd.Run, ct);
        var defaultName = await GetDefaultNameAsync(preset.Commands.GetDefaultSink, ct);
        var devices = ParseDevices(json, cmd.Fields, defaultName);
        return await ResolveBluetoothNamesAsync(devices, ct);
    }

    public async Task<IReadOnlyList<AudioDevice>> ListSourcesAsync(CancellationToken ct = default)
    {
        var cmd = preset.Commands.ListSources;
        var json = await RunJsonCommandAsync(cmd.Run, ct);
        var defaultName = await GetDefaultNameAsync(preset.Commands.GetDefaultSource, ct);
        var devices = ParseDevices(json, cmd.Fields, defaultName);
        devices = devices.Where(d => !d.Name.Contains(".monitor")).ToList();
        return await ResolveBluetoothNamesAsync(devices, ct);
    }

    public async Task<IReadOnlyList<AudioStream>> ListStreamsAsync(CancellationToken ct = default)
    {
        var cmd = preset.Commands.ListStreams;
        var json = await RunJsonCommandAsync(cmd.Run, ct);
        return ParseStreams(json, cmd.Fields);
    }

    public Task SetVolumeAsync(int deviceId, double volume, CancellationToken ct = default)
    {
        var pct = (int)Math.Round(volume * 100);
        var cmd = preset.Commands.SetVolume
            .Replace("{id}", deviceId.ToString())
            .Replace("{volumePct}", pct + "%")
            .Replace("{volume}", volume.ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
        return RunFireAndForgetAsync(cmd, ct);
    }

    public Task ToggleMuteAsync(int deviceId, CancellationToken ct = default)
    {
        var cmd = preset.Commands.ToggleMute
            .Replace("{id}", deviceId.ToString());
        return RunFireAndForgetAsync(cmd, ct);
    }

    public Task SetDefaultSinkAsync(int deviceId, string deviceName, CancellationToken ct = default)
    {
        var cmd = preset.Commands.SetDefaultSink
            .Replace("{id}", deviceId.ToString())
            .Replace("{name}", deviceName);
        return RunFireAndForgetAsync(cmd, ct);
    }

    public Task SetDefaultSourceAsync(int deviceId, string deviceName, CancellationToken ct = default)
    {
        var cmd = preset.Commands.SetDefaultSource
            .Replace("{id}", deviceId.ToString())
            .Replace("{name}", deviceName);
        return RunFireAndForgetAsync(cmd, ct);
    }

    public Task MoveStreamAsync(int streamId, int sinkId, CancellationToken ct = default)
    {
        var cmd = preset.Commands.MoveStream
            .Replace("{streamId}", streamId.ToString())
            .Replace("{sinkId}", sinkId.ToString());
        return RunFireAndForgetAsync(cmd, ct);
    }

    public Task SetStreamVolumeAsync(int streamId, double volume, CancellationToken ct = default)
    {
        var pct = (int)Math.Round(volume * 100);
        var cmd = preset.Commands.SetStreamVolume
            .Replace("{streamId}", streamId.ToString())
            .Replace("{volumePct}", pct + "%")
            .Replace("{volume}", volume.ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
        return RunFireAndForgetAsync(cmd, ct);
    }

    private async Task<IReadOnlyList<AudioDevice>> ResolveBluetoothNamesAsync(
        IReadOnlyList<AudioDevice> devices, CancellationToken ct)
    {
        var needsResolution = devices.Any(d =>
            d.Description is "(null)" or "" && d.Name.StartsWith("bluez_", StringComparison.Ordinal));

        if (!needsResolution) return devices;

        var result = new List<AudioDevice>(devices.Count);
        foreach (var device in devices)
        {
            if (device.Description is "(null)" or "" &&
                device.Name.StartsWith("bluez_", StringComparison.Ordinal))
            {
                var alias = await GetBluetoothAliasAsync(device.Name, ct);
                if (!string.IsNullOrEmpty(alias))
                {
                    result.Add(new AudioDevice
                    {
                        Id = device.Id,
                        Name = device.Name,
                        Description = alias,
                        Volume = device.Volume,
                        Muted = device.Muted,
                        IsDefault = device.IsDefault,
                    });
                    continue;
                }
            }
            result.Add(device);
        }
        return result;
    }

    private async Task<string> GetBluetoothAliasAsync(string deviceName, CancellationToken ct)
    {
        // Extract MAC from "bluez_output.34_0E_22_E3_71_FE.1" → "34:0E:22:E3:71:FE"
        var parts = deviceName.Split('.');
        if (parts.Length < 2) return string.Empty;
        var mac = parts[1].Replace('_', ':');

        try
        {
            var output = await cli.RunAsync("bluetoothctl", $"info {mac}", ct);
            foreach (var line in output.Split('\n'))
            {
                var trimmed = line.AsSpan().Trim();
                if (trimmed.StartsWith("Alias:"))
                    return trimmed["Alias:".Length..].Trim().ToString();
                if (trimmed.StartsWith("Name:"))
                    return trimmed["Name:".Length..].Trim().ToString();
            }
        }
        catch
        {
            // bluetoothctl not available
        }
        return string.Empty;
    }

    private async Task<JsonArray> RunJsonCommandAsync(string command, CancellationToken ct = default)
    {
        var parts = SplitCommand(command);
        var output = await cli.RunAsync(parts.exe, parts.args, ct);

        if (string.IsNullOrWhiteSpace(output))
            return [];

        var node = JsonNode.Parse(output);
        return node is JsonArray arr ? arr : [];
    }

    private Task RunFireAndForgetAsync(string command, CancellationToken ct = default)
    {
        var parts = SplitCommand(command);
        return cli.RunAsync(parts.exe, parts.args, ct);
    }

    private async Task<string> GetDefaultNameAsync(string command, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(command)) return string.Empty;
        var parts = SplitCommand(command);
        var output = await cli.RunAsync(parts.exe, parts.args, ct);
        return output.Trim();
    }

    private static IReadOnlyList<AudioDevice> ParseDevices(JsonArray items, AudioFieldMap fields, string defaultName = "")
    {
        var result = new List<AudioDevice>(items.Count);
        foreach (var item in items)
        {
            if (item is not JsonObject obj) continue;
            var name = GetString(obj, fields.Name);
            result.Add(new AudioDevice
            {
                Id = GetInt(obj, fields.Id),
                Name = name,
                Description = GetString(obj, fields.Description),
                Volume = ParseVolumePercent(GetString(obj, fields.Volume)),
                Muted = GetBool(obj, fields.Muted),
                IsDefault = !string.IsNullOrEmpty(defaultName) && name == defaultName,
            });
        }
        return result;
    }

    private static IReadOnlyList<AudioStream> ParseStreams(JsonArray items, AudioFieldMap fields)
    {
        var result = new List<AudioStream>(items.Count);
        foreach (var item in items)
        {
            if (item is not JsonObject obj) continue;
            result.Add(new AudioStream
            {
                Id = GetInt(obj, fields.Id),
                AppName = GetString(obj, fields.AppName),
                SinkId = GetInt(obj, fields.SinkId),
                Volume = ParseVolumePercent(GetString(obj, fields.Volume)),
                Muted = GetBool(obj, fields.Muted),
            });
        }
        return result;
    }

    /// <summary>
    /// Navigates a dot-notation path through a JsonObject.
    /// Supports "*" wildcard to pick the first child property, e.g.
    /// "volume.*.value_percent" works for both "front-left" and "mono" channels.
    /// </summary>
    private static JsonNode? Navigate(JsonObject root, string path)
    {
        if (string.IsNullOrEmpty(path)) return null;

        JsonNode? current = root;
        foreach (var segment in path.Split('.'))
        {
            if (current is not JsonObject obj) return null;

            if (segment == "*")
            {
                // Pick the first child property
                using var enumerator = obj.GetEnumerator();
                current = enumerator.MoveNext() ? enumerator.Current.Value : null;
            }
            else if (obj.TryGetPropertyValue(segment, out var next))
            {
                current = next;
            }
            else
            {
                return null;
            }
        }
        return current;
    }

    private static string GetString(JsonObject obj, string path)
    {
        var node = Navigate(obj, path);
        return node?.GetValue<string>() ?? node?.ToString() ?? string.Empty;
    }

    private static int GetInt(JsonObject obj, string path)
    {
        var node = Navigate(obj, path);
        if (node is null) return 0;
        if (node.GetValueKind() == JsonValueKind.Number)
            return node.GetValue<int>();
        if (int.TryParse(node.ToString(), out var val))
            return val;
        return 0;
    }

    private static bool GetBool(JsonObject obj, string path)
    {
        var node = Navigate(obj, path);
        if (node is null) return false;
        if (node.GetValueKind() is JsonValueKind.True or JsonValueKind.False)
            return node.GetValue<bool>();
        return string.Equals(node.ToString(), "true", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Parses "74%" → 0.74 or "0.74" → 0.74</summary>
    private static double ParseVolumePercent(string value)
    {
        if (string.IsNullOrEmpty(value)) return 0;
        value = value.Trim();
        if (value.EndsWith('%'))
        {
            if (double.TryParse(value[..^1], System.Globalization.CultureInfo.InvariantCulture, out var pct))
                return pct / 100.0;
        }
        if (double.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, out var raw))
            return raw;
        return 0;
    }

    private static (string exe, string args) SplitCommand(string command)
    {
        var trimmed = command.Trim();
        var spaceIndex = trimmed.IndexOf(' ');
        if (spaceIndex < 0) return (trimmed, string.Empty);
        return (trimmed[..spaceIndex], trimmed[(spaceIndex + 1)..]);
    }
}
