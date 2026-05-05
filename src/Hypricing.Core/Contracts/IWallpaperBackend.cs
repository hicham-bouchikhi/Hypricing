namespace Hypricing.Core.Contracts;

public interface IWallpaperBackend
{
    Task SetWallpaperAsync(string monitor, string imagePath, CancellationToken ct = default);
    Task<IReadOnlyList<MonitorWallpaper>> GetActiveWallpapersAsync(CancellationToken ct = default);
}

public interface IWallpaperTransitions
{
    Task SetWallpaperAsync(string monitor, string imagePath, WallpaperTransition transition, CancellationToken ct = default);
}

public sealed class MonitorWallpaper
{
    public string Monitor { get; init; } = string.Empty;
    public string? ImagePath { get; init; }
}

public sealed class WallpaperTransition
{
    public string Type { get; init; } = "none";
    public double Duration { get; init; } = 1.0;
    public int Fps { get; init; } = 30;
}
