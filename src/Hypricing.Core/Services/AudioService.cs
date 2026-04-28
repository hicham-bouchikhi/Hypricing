using System.Reflection;
using System.Text.Json;
using Hypricing.Core.Backends.Audio;
using Hypricing.Core.Contracts;
using Hypricing.Core.Infrastructure;

namespace Hypricing.Core.Services;

/// <summary>
/// Manages audio backend lifecycle: auto-detects or loads a user-configured preset,
/// then delegates all operations to the <see cref="JsonAudioBackend"/>.
/// </summary>
public sealed class AudioService(CliRunner cli)
{
    private JsonAudioBackend? _backend;

    public IAudioBackend? Backend => _backend;
    public string? ActivePresetName => _backend?.PresetName;
    public string? Error { get; private set; }

    /// <summary>
    /// Initializes the audio backend. Checks for a user config first,
    /// then falls back to auto-detecting from built-in presets.
    /// </summary>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        _backend = null;
        Error = null;

        // 1. Check user config
        var userConfig = GetUserConfigPath();
        if (File.Exists(userConfig))
        {
            try
            {
                var json = await File.ReadAllTextAsync(userConfig, ct);
                var preset = JsonSerializer.Deserialize(json, AudioPresetContext.Default.AudioPreset);
                if (preset is not null)
                {
                    _backend = new JsonAudioBackend(cli, preset);
                    return;
                }
            }
            catch
            {
                Error = $"Failed to load user audio config: {userConfig}";
                return;
            }
        }

        // 2. Auto-detect from built-in presets
        foreach (var preset in LoadBuiltInPresets())
        {
            if (await IsToolAvailableAsync(preset.Detect, ct))
            {
                _backend = new JsonAudioBackend(cli, preset);

                // Write detected preset to user config for future editing
                var dir = Path.GetDirectoryName(userConfig)!;
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                var json = JsonSerializer.Serialize(preset, AudioPresetContext.Default.AudioPreset);
                await File.WriteAllTextAsync(userConfig, json, ct);
                return;
            }
        }

        Error = "No supported audio backend found. Configure one at: " + userConfig;
    }

    private static string GetUserConfigPath()
    {
        var configDir = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME")
                        ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
        return Path.Combine(configDir, "hypricing", "audio.json");
    }

    private static IReadOnlyList<AudioPreset> LoadBuiltInPresets()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var prefix = "Hypricing.Core.Presets.";
        var presets = new List<AudioPreset>();

        foreach (var resourceName in assembly.GetManifestResourceNames())
        {
            if (!resourceName.StartsWith(prefix) || !resourceName.Contains("audio-"))
                continue;

            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream is null) continue;

            var preset = JsonSerializer.Deserialize(stream, AudioPresetContext.Default.AudioPreset);
            if (preset is not null)
                presets.Add(preset);
        }

        // PipeWire first (most common on Hyprland)
        presets.Sort((a, b) => string.Compare(b.Detect, a.Detect, StringComparison.Ordinal));
        return presets;
    }

    private async Task<bool> IsToolAvailableAsync(string tool, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(tool)) return false;
        try
        {
            await cli.RunAsync("which", tool, ct);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
