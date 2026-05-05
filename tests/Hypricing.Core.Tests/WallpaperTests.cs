using Hypricing.Core.Backends.Wallpaper;
using Hypricing.Core.Contracts;
using Hypricing.Core.Infrastructure;
using Hypricing.Core.Services;

namespace Hypricing.Core.Tests;

public class WallpaperTests
{
    private const string SampleQuery = """
        : HDMI-A-1: 5120x1440, scale: 1, currently displaying: image: 0x000000ff
        : DP-1: 2560x1440, scale: 1, currently displaying: image: /home/user/Pictures/berserk.png
        : HDMI-A-2: 2560x1440, scale: 1, currently displaying: image: /home/user/Pictures/summer.jpeg
        """;

    // --- AwwwBackend parsing tests (no CLI) ---

    [Fact]
    public void ParsesActiveWallpapers_ReturnsImagePath()
    {
        var results = AwwwBackend.ParseQueryOutput(SampleQuery);

        var dp1 = results.Single(m => m.Monitor == "DP-1");
        Assert.Equal("/home/user/Pictures/berserk.png", dp1.ImagePath);
    }

    [Fact]
    public void ParsesActiveWallpapers_SolidColorReturnsNull()
    {
        var results = AwwwBackend.ParseQueryOutput(SampleQuery);

        var hdmi1 = results.Single(m => m.Monitor == "HDMI-A-1");
        Assert.Null(hdmi1.ImagePath);
    }

    [Fact]
    public void ParsesAllThreeMonitors()
    {
        var results = AwwwBackend.ParseQueryOutput(SampleQuery);

        Assert.Equal(3, results.Count);
        Assert.Contains(results, m => m.Monitor == "HDMI-A-1");
        Assert.Contains(results, m => m.Monitor == "DP-1");
        Assert.Contains(results, m => m.Monitor == "HDMI-A-2");
    }

    [Fact]
    public void ParseQueryOutput_EmptyInput_ReturnsEmpty()
    {
        var results = AwwwBackend.ParseQueryOutput(string.Empty);
        Assert.Empty(results);
    }

    // --- AwwwBackend CLI tests ---

    [Fact]
    public async Task SetWallpaperAsync_CallsCorrectCommand()
    {
        var spy = new SpyCliRunner();
        var backend = new AwwwBackend(spy);

        await backend.SetWallpaperAsync("DP-1", "/home/user/Pictures/berserk.png");

        Assert.Single(spy.Invocations);
        var (cmd, args) = spy.Invocations[0];
        Assert.Equal("awww", cmd);
        Assert.Contains("-o DP-1", args);
        Assert.Contains("berserk.png", args);
    }

    [Fact]
    public async Task SetWallpaperAsync_WithTransition_IncludesTransitionArgs()
    {
        var spy = new SpyCliRunner();
        var backend = new AwwwBackend(spy);
        var transition = new WallpaperTransition { Type = "fade", Duration = 1.5, Fps = 60 };

        await backend.SetWallpaperAsync("DP-1", "/home/user/Pictures/berserk.png", transition);

        var (_, args) = spy.Invocations[0];
        Assert.Contains("--transition-type fade", args);
        Assert.Contains("--transition-duration 1.50", args);
        Assert.Contains("--transition-fps 60", args);
    }

    // --- WallpaperService tests ---

    [Fact]
    public async Task InitializeAsync_WhenAwwwFound_SetsBackend()
    {
        var service = new WallpaperService(new AwwwFoundStub());

        await service.InitializeAsync();

        Assert.NotNull(service.Backend);
        Assert.Null(service.Error);
        Assert.True(service.SupportsTransitions);
    }

    [Fact]
    public async Task InitializeAsync_WhenAwwwMissing_SetsError()
    {
        var service = new WallpaperService(new AwwwMissingStub());

        await service.InitializeAsync();

        Assert.Null(service.Backend);
        Assert.NotNull(service.Error);
    }

    // --- Helpers ---

    private sealed class SpyCliRunner : CliRunner
    {
        public List<(string Command, string Arguments)> Invocations { get; } = [];

        public override Task<string> RunAsync(string command, string arguments, CancellationToken ct = default)
        {
            Invocations.Add((command, arguments));
            return Task.FromResult(string.Empty);
        }
    }

    private sealed class AwwwFoundStub : CliRunner
    {
        public override Task<string> RunAsync(string command, string arguments, CancellationToken ct = default)
            => Task.FromResult(string.Empty);
    }

    private sealed class AwwwMissingStub : CliRunner
    {
        public override Task<string> RunAsync(string command, string arguments, CancellationToken ct = default)
            => throw new InvalidOperationException("which: awww not found");
    }
}
