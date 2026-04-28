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

    [Fact]
    public async Task AddDeclarationAndSave()
    {
        var path = WriteTempConfig("$existing = val\n");

        var service = CreateService();
        await service.LoadAsync(path);

        service.AddDeclaration("newvar", "hello");
        await service.SaveAsync();

        var saved = await File.ReadAllTextAsync(path);
        Assert.Contains("$newvar = hello", saved);
        Assert.Contains("$existing = val", saved);
    }

    [Fact]
    public async Task RemoveDeclarationAndSave()
    {
        var path = WriteTempConfig("$keep = yes\n$remove = no\n");

        var service = CreateService();
        await service.LoadAsync(path);

        var decls = service.GetDeclarations();
        service.RemoveDeclaration(decls.First(d => d.Name == "remove"));
        await service.SaveAsync();

        var saved = await File.ReadAllTextAsync(path);
        Assert.Contains("$keep = yes", saved);
        Assert.DoesNotContain("$remove", saved);
    }

    [Fact]
    public async Task AddEnvironmentVariableAndSave()
    {
        var path = WriteTempConfig("env = EXISTING,val\n");

        var service = CreateService();
        await service.LoadAsync(path);

        service.AddEnvironmentVariable("NEW_VAR", "new_val");
        await service.SaveAsync();

        var saved = await File.ReadAllTextAsync(path);
        Assert.Contains("env = NEW_VAR,new_val", saved);
        Assert.Contains("env = EXISTING,val", saved);
    }

    [Fact]
    public async Task RemoveEnvironmentVariableAndSave()
    {
        var path = WriteTempConfig("env = KEEP,yes\nenv = REMOVE,no\n");

        var service = CreateService();
        await service.LoadAsync(path);

        var envVars = service.GetEnvironmentVariables();
        service.RemoveEnvironmentVariable(envVars.First(e => e.Params.Contains("REMOVE")));
        await service.SaveAsync();

        var saved = await File.ReadAllTextAsync(path);
        Assert.Contains("env = KEEP,yes", saved);
        Assert.DoesNotContain("REMOVE", saved);
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

    /// <summary>CliRunner that returns a canned response for hyprctl monitors -j.</summary>
    private sealed class HyprctlStubCliRunner(string monitorsJson) : CliRunner
    {
        public override Task<string> RunAsync(string command, string arguments, CancellationToken ct = default)
        {
            if (command == "hyprctl" && arguments == "monitors -j")
                return Task.FromResult(monitorsJson);
            // Other calls (e.g. hyprctl reload) return empty
            return Task.FromResult(string.Empty);
        }
    }

    [Fact]
    public async Task GetMonitorInfoAsync_ParsesNamesAndModes()
    {
        const string json = """
            [
              {
                "name": "DP-1",
                "availableModes": ["1920x1080@144.00Hz", "1920x1080@60.00Hz", "2560x1440@144.00Hz"]
              },
              {
                "name": "HDMI-A-1",
                "availableModes": ["1920x1080@60.00Hz"]
              }
            ]
            """;

        var path = WriteTempConfig("monitor = DP-1,1920x1080@144,0x0,1\n");
        var service = new HyprlandService(new HyprctlStubCliRunner(json));
        await service.LoadAsync(path);

        var infos = await service.GetMonitorInfoAsync();

        Assert.Equal(2, infos.Count);

        Assert.Equal("DP-1", infos[0].Name);
        Assert.Equal(3, infos[0].AvailableModes.Count);
        Assert.Contains("1920x1080@144.00Hz", infos[0].AvailableModes);
        Assert.Contains("2560x1440@144.00Hz", infos[0].AvailableModes);

        Assert.Equal("HDMI-A-1", infos[1].Name);
        Assert.Single(infos[1].AvailableModes);
        Assert.Equal("1920x1080@60.00Hz", infos[1].AvailableModes[0]);
    }

    [Fact]
    public async Task GetMonitorInfoAsync_ReturnsEmptyOnEmptyJson()
    {
        var path = WriteTempConfig("$var = value\n");
        var service = new HyprlandService(new HyprctlStubCliRunner(""));
        await service.LoadAsync(path);

        var infos = await service.GetMonitorInfoAsync();

        Assert.Empty(infos);
    }

    [Fact]
    public async Task GetMonitorInfoAsync_ReturnsEmptyOnEmptyArray()
    {
        var path = WriteTempConfig("$var = value\n");
        var service = new HyprlandService(new HyprctlStubCliRunner("[]"));
        await service.LoadAsync(path);

        var infos = await service.GetMonitorInfoAsync();

        Assert.Empty(infos);
    }
}
