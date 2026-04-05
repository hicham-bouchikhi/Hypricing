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
public sealed class JsonAudioBackend : IAudioBackend
{
    private readonly CliRunner _cli;
    private readonly AudioPreset _preset;

    public JsonAudioBackend(CliRunner cli, AudioPreset preset)
    {
        _cli = cli;
        _preset = preset;
    }

    public string PresetName => _preset.Name;

    public async Task<IReadOnlyList<AudioDevice>> ListSinksAsync(CancellationToken ct = default)
    {
        var cmd = _preset.Commands.ListSinks;
        var json = await RunJsonCommandAsync(cmd.Run, ct);
        var defaultName = await GetDefaultNameAsync(_preset.Commands.GetDefaultSink, ct);
        return ParseDevices(json, cmd.Fields, defaultName);
    }

    public async Task<IReadOnlyList<AudioDevice>> ListSourcesAsync(CancellationToken ct = default)
    {
        var cmd = _preset.Commands.ListSources;
        var json = await RunJsonCommandAsync(cmd.Run, ct);
        var defaultName = await GetDefaultNameAsync(_preset.Commands.GetDefaultSource, ct);
        var devices = ParseDevices(json, cmd.Fields, defaultName);
        // Filter out monitor sources (loopback captures of output devices)
        return devices.Where(d => !d.Name.Contains(".monitor")).ToList();
    }

    public async Task<IReadOnlyList<AudioStream>> ListStreamsAsync(CancellationToken ct = default)
    {
        var cmd = _preset.Commands.ListStreams;
        var json = await RunJsonCommandAsync(cmd.Run, ct);
        return ParseStreams(json, cmd.Fields);
    }

    public Task SetVolumeAsync(int deviceId, double volume, CancellationToken ct = default)
    {
        var pct = (int)Math.Round(volume * 100);
        var cmd = _preset.Commands.SetVolume
            .Replace("{id}", deviceId.ToString())
            .Replace("{volumePct}", pct + "%")
            .Replace("{volume}", volume.ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
        return RunFireAndForgetAsync(cmd, ct);
    }

    public Task ToggleMuteAsync(int deviceId, CancellationToken ct = default)
    {
        var cmd = _preset.Commands.ToggleMute
            .Replace("{id}", deviceId.ToString());
        return RunFireAndForgetAsync(cmd, ct);
    }

    public Task SetDefaultSinkAsync(int deviceId, string deviceName, CancellationToken ct = default)
    {
        var cmd = _preset.Commands.SetDefaultSink
            .Replace("{id}", deviceId.ToString())
            .Replace("{name}", deviceName);
        return RunFireAndForgetAsync(cmd, ct);
    }

    public Task SetDefaultSourceAsync(int deviceId, string deviceName, CancellationToken ct = default)
    {
        var cmd = _preset.Commands.SetDefaultSource
            .Replace("{id}", deviceId.ToString())
            .Replace("{name}", deviceName);
        return RunFireAndForgetAsync(cmd, ct);
    }

    public Task MoveStreamAsync(int streamId, int sinkId, CancellationToken ct = default)
    {
        var cmd = _preset.Commands.MoveStream
            .Replace("{streamId}", streamId.ToString())
            .Replace("{sinkId}", sinkId.ToString());
        return RunFireAndForgetAsync(cmd, ct);
    }

    public Task SetStreamVolumeAsync(int streamId, double volume, CancellationToken ct = default)
    {
        var pct = (int)Math.Round(volume * 100);
        var cmd = _preset.Commands.SetStreamVolume
            .Replace("{streamId}", streamId.ToString())
            .Replace("{volumePct}", pct + "%")
            .Replace("{volume}", volume.ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
        return RunFireAndForgetAsync(cmd, ct);
    }

    private async Task<JsonArray> RunJsonCommandAsync(string command, CancellationToken ct = default)
    {
        var parts = SplitCommand(command);
        var output = await _cli.RunAsync(parts.exe, parts.args, ct);

        if (string.IsNullOrWhiteSpace(output))
            return [];

        var node = JsonNode.Parse(output);
        return node is JsonArray arr ? arr : [];
    }

    private Task RunFireAndForgetAsync(string command, CancellationToken ct = default)
    {
        var parts = SplitCommand(command);
        return _cli.RunAsync(parts.exe, parts.args, ct);
    }

    private async Task<string> GetDefaultNameAsync(string command, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(command)) return string.Empty;
        var parts = SplitCommand(command);
        var output = await _cli.RunAsync(parts.exe, parts.args, ct);
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
