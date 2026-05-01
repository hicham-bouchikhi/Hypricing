using System.Reflection;
using System.Text.Json;
using Hypricing.Core.Backends.Idle;
using Hypricing.Core.Contracts;
using Hypricing.Core.Infrastructure;

namespace Hypricing.Core.Services;

public sealed class IdleService(CliRunner cli)
{
    private HyprlangIdleBackend? _backend;

    public IIdleBackend? Backend => _backend;
    public string? ActivePresetName => _backend?.PresetName;
    public string? Error { get; private set; }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        _backend = null;
        Error = null;

        var userConfig = GetUserConfigPath();
        if (File.Exists(userConfig))
        {
            try
            {
                var json = await File.ReadAllTextAsync(userConfig, ct);
                var preset = JsonSerializer.Deserialize(json, IdlePresetContext.Default.IdlePreset);
                if (preset is not null)
                {
                    _backend = new HyprlangIdleBackend(cli, preset);
                    return;
                }
            }
            catch
            {
                Error = $"Failed to load user idle config: {userConfig}";
                return;
            }
        }

        foreach (var preset in LoadBuiltInPresets())
        {
            if (await IsToolAvailableAsync(preset.Detect, ct))
            {
                _backend = new HyprlangIdleBackend(cli, preset);

                var dir = Path.GetDirectoryName(userConfig)!;
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                var json = JsonSerializer.Serialize(preset, IdlePresetContext.Default.IdlePreset);
                await File.WriteAllTextAsync(userConfig, json, ct);
                return;
            }
        }

        Error = "No supported idle daemon found. Install hypridle or swayidle.";
    }

    private static string GetUserConfigPath()
    {
        var configDir = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME")
                        ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
        return Path.Combine(configDir, "hypricing", "idle.json");
    }

    private static IReadOnlyList<IdlePreset> LoadBuiltInPresets()
    {
        var assembly = Assembly.GetExecutingAssembly();
        const string prefix = "Hypricing.Core.Presets.";
        var presets = new List<IdlePreset>();

        foreach (var resourceName in assembly.GetManifestResourceNames())
        {
            if (!resourceName.StartsWith(prefix) || !resourceName.Contains("idle-"))
                continue;

            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream is null) continue;

            var preset = JsonSerializer.Deserialize(stream, IdlePresetContext.Default.IdlePreset);
            if (preset is not null)
                presets.Add(preset);
        }

        return presets;
    }

    private async Task<bool> IsToolAvailableAsync(string tool, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(tool)) return false;
        try { await cli.RunAsync("which", tool, ct); return true; }
        catch { return false; }
    }
}
