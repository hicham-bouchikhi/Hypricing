using System.Text.Json.Serialization;

namespace Hypricing.Core.Backends.Audio;

/// <summary>
/// JSON-serializable preset defining how to interact with an audio backend.
/// Each command has a shell command template and optional field mappings.
/// </summary>
public sealed class AudioPreset
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("detect")]
    public string Detect { get; set; } = string.Empty;

    [JsonPropertyName("commands")]
    public AudioPresetCommands Commands { get; set; } = new();
}

public sealed class AudioPresetCommands
{
    [JsonPropertyName("listSinks")]
    public AudioQueryCommand ListSinks { get; set; } = new();

    [JsonPropertyName("listSources")]
    public AudioQueryCommand ListSources { get; set; } = new();

    [JsonPropertyName("listStreams")]
    public AudioQueryCommand ListStreams { get; set; } = new();

    [JsonPropertyName("setVolume")]
    public string SetVolume { get; set; } = string.Empty;

    [JsonPropertyName("toggleMute")]
    public string ToggleMute { get; set; } = string.Empty;

    [JsonPropertyName("setDefaultSink")]
    public string SetDefaultSink { get; set; } = string.Empty;

    [JsonPropertyName("setDefaultSource")]
    public string SetDefaultSource { get; set; } = string.Empty;

    [JsonPropertyName("getDefaultSink")]
    public string GetDefaultSink { get; set; } = string.Empty;

    [JsonPropertyName("getDefaultSource")]
    public string GetDefaultSource { get; set; } = string.Empty;

    [JsonPropertyName("moveStream")]
    public string MoveStream { get; set; } = string.Empty;

    [JsonPropertyName("setStreamVolume")]
    public string SetStreamVolume { get; set; } = string.Empty;
}

/// <summary>
/// A command that returns data. Includes field mappings from the output JSON
/// to our contract models.
/// </summary>
public sealed class AudioQueryCommand
{
    [JsonPropertyName("run")]
    public string Run { get; set; } = string.Empty;

    [JsonPropertyName("format")]
    public string Format { get; set; } = "json";

    [JsonPropertyName("fields")]
    public AudioFieldMap Fields { get; set; } = new();
}

/// <summary>
/// Maps JSON paths (dot-notation) from command output to contract fields.
/// </summary>
public sealed class AudioFieldMap
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("volume")]
    public string Volume { get; set; } = string.Empty;

    [JsonPropertyName("muted")]
    public string Muted { get; set; } = string.Empty;

    [JsonPropertyName("sinkId")]
    public string SinkId { get; set; } = string.Empty;

    [JsonPropertyName("appName")]
    public string AppName { get; set; } = string.Empty;
}

/// <summary>
/// AOT-safe JSON serialization context for presets.
/// </summary>
[JsonSerializable(typeof(AudioPreset))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
public sealed partial class AudioPresetContext : JsonSerializerContext;
