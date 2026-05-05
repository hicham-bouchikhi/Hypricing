using Hypricing.Core.Backends.Wallpaper;
using Hypricing.Core.Contracts;
using Hypricing.Core.Infrastructure;

namespace Hypricing.Core.Services;

public sealed class WallpaperService(CliRunner cli)
{
    private static string ConfigPath =>
        Path.Combine(
            Environment.GetEnvironmentVariable("XDG_CONFIG_HOME")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config"),
            "hypricing", "wallpaper-folder");

    public IWallpaperBackend? Backend { get; private set; }
    public bool SupportsTransitions => Backend is IWallpaperTransitions;
    public string? Error { get; private set; }
    public string? SavedFolder { get; private set; }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        Backend = null;
        Error = null;

        try
        {
            await cli.RunAsync("which", "awww", ct);
            Backend = new AwwwBackend(cli);
        }
        catch
        {
            Error = "awww not found. Install awww to use wallpaper management.";
        }

        if (File.Exists(ConfigPath))
        {
            var saved = (await File.ReadAllTextAsync(ConfigPath, ct)).Trim();
            if (Directory.Exists(saved))
                SavedFolder = saved;
        }
    }

    public void SaveFolder(string path)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
            File.WriteAllText(ConfigPath, path);
            SavedFolder = path;
        }
        catch { }
    }
}
