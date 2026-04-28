using Hypricing.Core.Backends.Audio;
using Hypricing.Core.Infrastructure;

namespace Hypricing.Core.Tests;

public class JsonAudioBackendTests
{
    private static AudioPreset CreatePipewirePreset() => new()
    {
        Name = "test-pipewire",
        Detect = "wpctl",
        Commands = new AudioPresetCommands
        {
            ListSinks = new AudioQueryCommand
            {
                Run = "echo sinks",
                Format = "json",
                Fields = new AudioFieldMap
                {
                    Id = "index",
                    Name = "name",
                    Description = "description",
                    Volume = "volume.front-left.value_percent",
                    Muted = "mute",
                },
            },
            ListSources = new AudioQueryCommand
            {
                Run = "echo sources",
                Format = "json",
                Fields = new AudioFieldMap
                {
                    Id = "index",
                    Name = "name",
                    Description = "description",
                    Volume = "volume.front-left.value_percent",
                    Muted = "mute",
                },
            },
            ListStreams = new AudioQueryCommand
            {
                Run = "echo streams",
                Format = "json",
                Fields = new AudioFieldMap
                {
                    Id = "index",
                    AppName = "properties.application.name",
                    SinkId = "sink",
                    Volume = "volume.front-left.value_percent",
                    Muted = "mute",
                },
            },
            SetVolume = "wpctl set-volume {id} {volume}",
            ToggleMute = "wpctl set-mute {id} toggle",
            SetDefaultSink = "wpctl set-default {id}",
            SetDefaultSource = "wpctl set-default {id}",
            MoveStream = "pactl move-sink-input {streamId} {sinkId}",
            SetStreamVolume = "pactl set-sink-input-volume {streamId} {volume}",
        },
    };

    [Fact]
    public async Task ListSinks_ParsesJsonOutput()
    {
        const string json = """
        [
            {
                "index": 61,
                "name": "alsa_output.pci",
                "description": "Speakers",
                "mute": false,
                "volume": {
                    "front-left": { "value_percent": "75%" }
                }
            },
            {
                "index": 148,
                "name": "bluez_output.airpods",
                "description": "AirPods",
                "mute": true,
                "volume": {
                    "front-left": { "value_percent": "50%" }
                }
            }
        ]
        """;

        var cli = new FakeCliRunner(json);
        var backend = new JsonAudioBackend(cli, CreatePipewirePreset());

        var sinks = await backend.ListSinksAsync();

        Assert.Equal(2, sinks.Count);
        Assert.Equal(61, sinks[0].Id);
        Assert.Equal("alsa_output.pci", sinks[0].Name);
        Assert.Equal("Speakers", sinks[0].Description);
        Assert.Equal(0.75, sinks[0].Volume, 0.01);
        Assert.False(sinks[0].Muted);
        Assert.Equal(148, sinks[1].Id);
        Assert.True(sinks[1].Muted);
        Assert.Equal(0.50, sinks[1].Volume, 0.01);
    }

    [Fact]
    public async Task ListStreams_ParsesNestedAppName()
    {
        const string json = """
        [
            {
                "index": 649,
                "sink": 148,
                "mute": false,
                "volume": {
                    "front-left": { "value_percent": "100%" }
                },
                "properties": {
                    "application": { "name": "Discord" }
                }
            }
        ]
        """;

        var cli = new FakeCliRunner(json);
        var backend = new JsonAudioBackend(cli, CreatePipewirePreset());

        var streams = await backend.ListStreamsAsync();

        Assert.Single(streams);
        Assert.Equal(649, streams[0].Id);
        Assert.Equal("Discord", streams[0].AppName);
        Assert.Equal(148, streams[0].SinkId);
        Assert.Equal(1.0, streams[0].Volume, 0.01);
        Assert.False(streams[0].Muted);
    }

    [Fact]
    public async Task ListSinks_EmptyOutput_ReturnsEmptyList()
    {
        var cli = new FakeCliRunner("");
        var backend = new JsonAudioBackend(cli, CreatePipewirePreset());

        var sinks = await backend.ListSinksAsync();

        Assert.Empty(sinks);
    }

    [Fact]
    public async Task SetVolume_FormatsCommandCorrectly()
    {
        var cli = new FakeCliRunner("");
        var backend = new JsonAudioBackend(cli, CreatePipewirePreset());

        await backend.SetVolumeAsync(61, 0.75);

        Assert.Single(cli.Invocations);
        Assert.Equal("wpctl", cli.Invocations[0].Command);
        Assert.Equal("set-volume 61 0.75", cli.Invocations[0].Arguments);
    }

    [Fact]
    public async Task ToggleMute_FormatsCommandCorrectly()
    {
        var cli = new FakeCliRunner("");
        var backend = new JsonAudioBackend(cli, CreatePipewirePreset());

        await backend.ToggleMuteAsync(61);

        Assert.Single(cli.Invocations);
        Assert.Equal("wpctl", cli.Invocations[0].Command);
        Assert.Equal("set-mute 61 toggle", cli.Invocations[0].Arguments);
    }

    [Fact]
    public async Task MoveStream_FormatsCommandCorrectly()
    {
        var cli = new FakeCliRunner("");
        var backend = new JsonAudioBackend(cli, CreatePipewirePreset());

        await backend.MoveStreamAsync(649, 61);

        Assert.Single(cli.Invocations);
        Assert.Equal("pactl", cli.Invocations[0].Command);
        Assert.Equal("move-sink-input 649 61", cli.Invocations[0].Arguments);
    }

    [Fact]
    public async Task SetDefaultSink_FormatsCommandCorrectly()
    {
        var cli = new FakeCliRunner("");
        var backend = new JsonAudioBackend(cli, CreatePipewirePreset());

        await backend.SetDefaultSinkAsync(148, "bluez_output.airpods");

        Assert.Single(cli.Invocations);
        Assert.Equal("wpctl", cli.Invocations[0].Command);
        Assert.Equal("set-default 148", cli.Invocations[0].Arguments);
    }

    [Fact]
    public async Task VolumePercent_ParsesWithAndWithoutSymbol()
    {
        const string json = """
        [
            {
                "index": 1,
                "name": "a",
                "description": "A",
                "mute": false,
                "volume": { "front-left": { "value_percent": "101%" } }
            }
        ]
        """;

        var cli = new FakeCliRunner(json);
        var backend = new JsonAudioBackend(cli, CreatePipewirePreset());

        var sinks = await backend.ListSinksAsync();

        Assert.Equal(1.01, sinks[0].Volume, 0.01);
    }

    /// <summary>CLI runner that returns a fixed response and records invocations.</summary>
    private sealed class FakeCliRunner(string response) : CliRunner
    {
        public List<(string Command, string Arguments)> Invocations { get; } = [];

        public override Task<string> RunAsync(string command, string arguments, CancellationToken ct = default)
        {
            Invocations.Add((command, arguments));
            return Task.FromResult(response);
        }
    }
}
