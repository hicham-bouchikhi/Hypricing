using System.Globalization;
using Hypricing.Core.Contracts;
using Hypricing.Core.Infrastructure;

namespace Hypricing.Core.Backends.Wallpaper;

public sealed class AwwwBackend(CliRunner cli) : IWallpaperBackend, IWallpaperTransitions
{
    private const string DisplayingPrefix = "currently displaying: image: ";

    public async Task<IReadOnlyList<MonitorWallpaper>> GetActiveWallpapersAsync(CancellationToken ct = default)
    {
        var output = await cli.RunAsync("awww", "query", ct);
        return ParseQueryOutput(output);
    }

    public Task SetWallpaperAsync(string monitor, string imagePath, CancellationToken ct = default)
    {
        var args = $"img -o {monitor} {QuotePath(imagePath)}";
        return cli.RunAsync("awww", args, ct);
    }

    public Task SetWallpaperAsync(string monitor, string imagePath, WallpaperTransition transition, CancellationToken ct = default)
    {
        var duration = transition.Duration.ToString("F2", CultureInfo.InvariantCulture);
        var args = $"img --transition-type {transition.Type} --transition-duration {duration} --transition-fps {transition.Fps} -o {monitor} {QuotePath(imagePath)}";
        return cli.RunAsync("awww", args, ct);
    }

    public static IReadOnlyList<MonitorWallpaper> ParseQueryOutput(string output)
    {
        var result = new List<MonitorWallpaper>();
        foreach (var rawLine in output.Split('\n'))
        {
            var line = rawLine.Trim();
            if (!line.StartsWith(": ", StringComparison.Ordinal))
                continue;

            line = line[2..];

            var colonIdx = line.IndexOf(':');
            if (colonIdx < 0) continue;
            var monitor = line[..colonIdx].Trim();

            var displayingIdx = line.IndexOf(DisplayingPrefix, StringComparison.Ordinal);
            if (displayingIdx < 0) continue;
            var imageValue = line[(displayingIdx + DisplayingPrefix.Length)..].Trim();

            string? imagePath = imageValue.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                ? null
                : imageValue;

            result.Add(new MonitorWallpaper { Monitor = monitor, ImagePath = imagePath });
        }
        return result;
    }

    private static string QuotePath(string path) => $"\"{path}\"";
}
