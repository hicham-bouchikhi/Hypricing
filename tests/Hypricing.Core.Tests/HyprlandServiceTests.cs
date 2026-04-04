using Hypricing.Core.Infrastructure;
using Hypricing.Core.Services;
using Hypricing.HyprlangParser.Nodes;

namespace Hypricing.Core.Tests;

public class HyprlandServiceTests : IDisposable
{
    private readonly string _tempDir;

    public HyprlandServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"hypricing-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public async Task LoadAndReadDeclarations()
    {
        var path = WriteTempConfig("""
            $myvar = SUPER
            $terminal = kitty
            gaps_in = 5
            """);

        var service = CreateService();
        await service.LoadAsync(path);

        var declarations = service.GetDeclarations();

        Assert.Equal(2, declarations.Count);
        Assert.Equal("myvar", declarations[0].Name);
        Assert.Equal("SUPER", declarations[0].Value);
        Assert.Equal("terminal", declarations[1].Name);
        Assert.Equal("kitty", declarations[1].Value);
    }

    [Fact]
    public async Task ModifyAndSaveRoundTrip()
    {
        var path = WriteTempConfig("""
            $myvar = SUPER
            gaps_in = 5
            """);

        var service = CreateService();
        await service.LoadAsync(path);

        var declarations = service.GetDeclarations();
        declarations[0].Value = "ALT";

        await service.SaveAsync();

        // Re-read the file and verify
        var saved = await File.ReadAllTextAsync(path);
        Assert.Contains("$myvar = ALT", saved);
        Assert.Contains("gaps_in = 5", saved);
    }

    [Fact]
    public async Task SaveTriggersHyprctlReload()
    {
        var path = WriteTempConfig("$var = value\n");

        var spy = new SpyCliRunner();
        var service = new HyprlandService(spy);
        await service.LoadAsync(path);

        await service.SaveAsync();

        Assert.Single(spy.Invocations);
        Assert.Equal("hyprctl", spy.Invocations[0].Command);
        Assert.Equal("reload", spy.Invocations[0].Arguments);
    }

    [Fact]
    public async Task EmptyConfigReturnsNoDeclarations()
    {
        var path = WriteTempConfig("""
            general {
                gaps_in = 5
            }
            """);

        var service = CreateService();
        await service.LoadAsync(path);

        var declarations = service.GetDeclarations();
        Assert.Empty(declarations);
    }

    [Fact]
    public void GetDeclarationsBeforeLoadThrows()
    {
        var service = CreateService();
        Assert.Throws<InvalidOperationException>(() => service.GetDeclarations());
    }

    [Fact]
    public async Task SavePreservesUnmodifiedLines()
    {
        const string config = "$myvar = SUPER\n# this is a comment\ngaps_in = 5\n";
        var path = WriteTempConfig(config);

        var service = CreateService();
        await service.LoadAsync(path);

        // Save without modifications
        await service.SaveAsync();

        var saved = await File.ReadAllTextAsync(path);
        Assert.Equal(config, saved);
    }

    [Fact]
    public async Task DeclarationsRefreshAfterSave()
    {
        var path = WriteTempConfig("$myvar = SUPER\n");

        var service = CreateService();
        await service.LoadAsync(path);

        var declarations = service.GetDeclarations();
        declarations[0].Value = "ALT";
        await service.SaveAsync();

        // After save, re-parsed AST should reflect the new value
        var refreshed = service.GetDeclarations();
        Assert.Equal("ALT", refreshed[0].Value);
    }

    [Fact]
    public async Task LoadAndReadEnvironmentVariables()
    {
        var path = WriteTempConfig("""
            env = WAYLAND_DISPLAY,wayland-1
            env = GDK_BACKEND,wayland
            gaps_in = 5
            """);

        var service = CreateService();
        await service.LoadAsync(path);

        var envVars = service.GetEnvironmentVariables();

        Assert.Equal(2, envVars.Count);
        Assert.Equal("env", envVars[0].Keyword);
        Assert.Contains("WAYLAND_DISPLAY", envVars[0].Params);
        Assert.Contains("GDK_BACKEND", envVars[1].Params);
    }

    [Fact]
    public async Task ModifyEnvVarAndSaveRoundTrip()
    {
        var path = WriteTempConfig("env = GDK_BACKEND,wayland\n");

        var service = CreateService();
        await service.LoadAsync(path);

        var envVars = service.GetEnvironmentVariables();
        envVars[0].Params = "GDK_BACKEND,x11";

        await service.SaveAsync();

        var saved = await File.ReadAllTextAsync(path);
        Assert.Contains("env = GDK_BACKEND,x11", saved);
    }

    [Fact]
    public async Task LoadAndReadExecEntries()
    {
        var path = WriteTempConfig("""
            exec-once = waybar
            exec = swaybg -i ~/wallpaper.png
            exec-shutdown = notify-send bye
            """);

        var service = CreateService();
        await service.LoadAsync(path);

        var entries = service.GetExecEntries();

        Assert.Equal(3, entries.Count);
        Assert.Equal("waybar", entries[0].Command);
        Assert.Equal(ExecVariant.Once, entries[0].Variant);
        Assert.Equal("swaybg -i ~/wallpaper.png", entries[1].Command);
        Assert.Equal(ExecVariant.Reload, entries[1].Variant);
        Assert.Equal(ExecVariant.Shutdown, entries[2].Variant);
    }

    [Fact]
    public async Task AddExecEntryAndSave()
    {
        var path = WriteTempConfig("$var = value\n");

        var service = CreateService();
        await service.LoadAsync(path);

        service.AddExecEntry(ExecVariant.Once, "kitty");
        await service.SaveAsync();

        var saved = await File.ReadAllTextAsync(path);
        Assert.Contains("exec-once = kitty", saved);
        Assert.Contains("$var = value", saved);
    }

    [Fact]
    public async Task RemoveExecEntryAndSave()
    {
        var path = WriteTempConfig("exec-once = waybar\nexec-once = kitty\n");

        var service = CreateService();
        await service.LoadAsync(path);

        var entries = service.GetExecEntries();
        service.RemoveExecEntry(entries[0]);
        await service.SaveAsync();

        var saved = await File.ReadAllTextAsync(path);
        Assert.DoesNotContain("waybar", saved);
        Assert.Contains("exec-once = kitty", saved);
    }

    [Fact]
    public async Task FollowsSourceIncludes()
    {
        var mainPath = WriteTempFile("hyprland.conf", $"$mainvar = hello\nsource = {_tempDir}/autostart.conf\n");
        WriteTempFile("autostart.conf", "exec-once = waybar\nexec-once = kitty\n");

        var service = CreateService();
        await service.LoadAsync(mainPath);

        var declarations = service.GetDeclarations();
        var execs = service.GetExecEntries();

        Assert.Single(declarations);
        Assert.Equal("mainvar", declarations[0].Name);
        Assert.Equal(2, execs.Count);
        Assert.Equal("waybar", execs[0].Command);
        Assert.Equal("kitty", execs[1].Command);
    }

    [Fact]
    public async Task SourceIncludeSavesBackToCorrectFile()
    {
        var mainPath = WriteTempFile("hyprland.conf", $"$var = old\nsource = {_tempDir}/extra.conf\n");
        var extraPath = WriteTempFile("extra.conf", "exec-once = waybar\n");

        var service = CreateService();
        await service.LoadAsync(mainPath);

        // Modify declaration in main, exec in extra
        service.GetDeclarations()[0].Value = "new";
        service.GetExecEntries()[0].Command = "polybar";

        await service.SaveAsync();

        var mainSaved = await File.ReadAllTextAsync(mainPath);
        var extraSaved = await File.ReadAllTextAsync(extraPath);

        Assert.Contains("$var = new", mainSaved);
        Assert.Contains("exec-once = polybar", extraSaved);
    }

    [Fact]
    public async Task AddExecGoesToFileWithExistingExecs()
    {
        var mainPath = WriteTempFile("hyprland.conf", $"$var = val\nsource = {_tempDir}/autostart.conf\n");
        WriteTempFile("autostart.conf", "exec-once = waybar\n");

        var service = CreateService();
        await service.LoadAsync(mainPath);

        service.AddExecEntry(ExecVariant.Once, "kitty");
        await service.SaveAsync();

        // New exec should be in autostart.conf, not hyprland.conf
        var mainSaved = await File.ReadAllTextAsync(mainPath);
        var autostartSaved = await File.ReadAllTextAsync(Path.Combine(_tempDir, "autostart.conf"));

        Assert.DoesNotContain("kitty", mainSaved);
        Assert.Contains("exec-once = kitty", autostartSaved);
    }

    [Fact]
    public async Task CircularIncludeDoesNotLoop()
    {
        var mainPath = WriteTempFile("hyprland.conf", $"$var = val\nsource = {_tempDir}/hyprland.conf\n");

        var service = CreateService();
        await service.LoadAsync(mainPath);

        // Should load without infinite loop
        Assert.Single(service.GetDeclarations());
    }

    [Fact]
    public async Task MissingSourceFileIsSkipped()
    {
        var mainPath = WriteTempFile("hyprland.conf", $"$var = val\nsource = {_tempDir}/nonexistent.conf\n");

        var service = CreateService();
        await service.LoadAsync(mainPath);

        Assert.Single(service.GetDeclarations());
    }

    private string WriteTempConfig(string content) => WriteTempFile("hyprland.conf", content);

    private string WriteTempFile(string name, string content)
    {
        var path = Path.Combine(_tempDir, name);
        File.WriteAllText(path, content);
        return path;
    }

    private HyprlandService CreateService() => new(new SpyCliRunner());

    /// <summary>CliRunner that records invocations for verification.</summary>
    private sealed class SpyCliRunner : CliRunner
    {
        public List<(string Command, string Arguments)> Invocations { get; } = [];

        public override Task<string> RunAsync(string command, string arguments, CancellationToken ct = default)
        {
            Invocations.Add((command, arguments));
            return Task.FromResult(string.Empty);
        }
    }
}
