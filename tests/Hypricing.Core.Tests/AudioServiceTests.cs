using System.Text.Json;
using Hypricing.Core.Backends.Audio;
using Hypricing.Core.Infrastructure;
using Hypricing.Core.Services;

namespace Hypricing.Core.Tests;

[Collection("env-sensitive")]
public class AudioServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string? _savedXdgConfigHome;

    public AudioServiceTests()
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
        var preset = new AudioPreset { Name = "test", Detect = "test-tool" };
        var json = JsonSerializer.Serialize(preset, AudioPresetContext.Default.AudioPreset);
        var configPath = Path.Combine(_tempDir, "hypricing");
        Directory.CreateDirectory(configPath);
        File.WriteAllText(Path.Combine(configPath, "audio.json"), json);

        var service = new AudioService(new RecordingCliRunner());
        await service.InitializeAsync();

        Assert.NotNull(service.Backend);
        Assert.Null(service.Error);
    }

    [Fact]
    public async Task InitializeAsync_WithMalformedUserConfig_SetsError()
    {
        var configPath = Path.Combine(_tempDir, "hypricing");
        Directory.CreateDirectory(configPath);
        File.WriteAllText(Path.Combine(configPath, "audio.json"), "not valid json {{");

        var service = new AudioService(new RecordingCliRunner());
        await service.InitializeAsync();

        Assert.Null(service.Backend);
        Assert.NotNull(service.Error);
        Assert.Contains("Failed to load user audio config", service.Error);
    }

    [Fact]
    public async Task InitializeAsync_NoUserConfig_NoToolAvailable_SetsError()
    {
        var service = new AudioService(new ThrowingCliRunner());
        await service.InitializeAsync();

        Assert.Null(service.Backend);
        Assert.NotNull(service.Error);
        Assert.Contains("No supported audio backend", service.Error);
    }

    [Fact]
    public async Task InitializeAsync_NoUserConfig_ToolAvailable_SetsBackend()
    {
        // FakeCliRunner succeeds for all calls (including "which wpctl") → backend auto-detected
        var service = new AudioService(new RecordingCliRunner());
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
