using Hypricing.Core.Infrastructure;
using Hypricing.Core.Services;

namespace Hypricing.Core.Tests;

[Collection("env-sensitive")]
public class IdleServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string? _savedXdgConfigHome;

    public IdleServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"hypricing-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _savedXdgConfigHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", _tempDir);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", _savedXdgConfigHome);
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public async Task InitializeAsync_WithValidUserConfig_SetsBackend()
    {
        // Minimal valid camelCase IdlePreset JSON
        const string json = """
            {
              "name": "test-idle",
              "detect": "test-tool",
              "configPath": "/tmp/idle.conf",
              "daemon": {"start": "", "stop": "", "restart": "", "status": ""},
              "fields": {
                "general": {"lockCmd": "lock_cmd", "unlockCmd": "unlock_cmd", "beforeSleepCmd": "before_sleep_cmd", "afterSleepCmd": "after_sleep_cmd"},
                "listener": {"timeout": "timeout", "onTimeout": "on-timeout", "onResume": "on-resume"}
              }
            }
            """;
        var configPath = Path.Combine(_tempDir, "hypricing");
        Directory.CreateDirectory(configPath);
        File.WriteAllText(Path.Combine(configPath, "idle.json"), json);

        var service = new IdleService(new RecordingCliRunner());
        await service.InitializeAsync();

        Assert.NotNull(service.Backend);
        Assert.Null(service.Error);
    }

    [Fact]
    public async Task InitializeAsync_WithMalformedUserConfig_SetsError()
    {
        var configPath = Path.Combine(_tempDir, "hypricing");
        Directory.CreateDirectory(configPath);
        File.WriteAllText(Path.Combine(configPath, "idle.json"), "not valid json {{");

        var service = new IdleService(new RecordingCliRunner());
        await service.InitializeAsync();

        Assert.Null(service.Backend);
        Assert.NotNull(service.Error);
        Assert.Contains("Failed to load user idle config", service.Error);
    }

    [Fact]
    public async Task InitializeAsync_NoUserConfig_NoToolAvailable_SetsError()
    {
        var service = new IdleService(new ThrowingCliRunner());
        await service.InitializeAsync();

        Assert.Null(service.Backend);
        Assert.NotNull(service.Error);
        Assert.Contains("No supported idle daemon", service.Error);
    }

    [Fact]
    public async Task InitializeAsync_NoUserConfig_ToolAvailable_SetsBackend()
    {
        // FakeCliRunner succeeds for all "which" calls → hypridle preset auto-detected
        var service = new IdleService(new RecordingCliRunner());
        await service.InitializeAsync();

        Assert.NotNull(service.Backend);
        Assert.Null(service.Error);
    }

    private sealed class RecordingCliRunner : CliRunner
    {
        public override Task<string> RunAsync(string command, string arguments, CancellationToken ct = default)
            => Task.FromResult(string.Empty);
    }

    private sealed class ThrowingCliRunner : CliRunner
    {
        public override Task<string> RunAsync(string command, string arguments, CancellationToken ct = default)
            => throw new InvalidOperationException("tool not found");
    }
}
