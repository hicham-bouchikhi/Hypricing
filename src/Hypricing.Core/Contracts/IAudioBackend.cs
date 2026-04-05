namespace Hypricing.Core.Contracts;

/// <summary>
/// Contract for audio backends. Any backend (PipeWire, PulseAudio, custom)
/// must provide these operations. All data flows through these models.
/// </summary>
public interface IAudioBackend
{
    /// <summary>List all output devices (speakers, headphones, HDMI).</summary>
    Task<IReadOnlyList<AudioDevice>> ListSinksAsync(CancellationToken ct = default);

    /// <summary>List all input devices (microphones).</summary>
    Task<IReadOnlyList<AudioDevice>> ListSourcesAsync(CancellationToken ct = default);

    /// <summary>List all running audio streams (apps currently playing).</summary>
    Task<IReadOnlyList<AudioStream>> ListStreamsAsync(CancellationToken ct = default);

    /// <summary>Set volume for a device (0.0 – 1.5).</summary>
    Task SetVolumeAsync(int deviceId, double volume, CancellationToken ct = default);

    /// <summary>Toggle mute on a device.</summary>
    Task ToggleMuteAsync(int deviceId, CancellationToken ct = default);

    /// <summary>Set the default output device.</summary>
    Task SetDefaultSinkAsync(int deviceId, string deviceName, CancellationToken ct = default);

    /// <summary>Set the default input device.</summary>
    Task SetDefaultSourceAsync(int deviceId, string deviceName, CancellationToken ct = default);

    /// <summary>Move a stream to a different sink.</summary>
    Task MoveStreamAsync(int streamId, int sinkId, CancellationToken ct = default);

    /// <summary>Set volume for a specific stream (0.0 – 1.5).</summary>
    Task SetStreamVolumeAsync(int streamId, double volume, CancellationToken ct = default);
}

/// <summary>
/// Represents an audio output (sink) or input (source).
/// </summary>
public sealed class AudioDevice
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public double Volume { get; init; }
    public bool Muted { get; init; }
    public bool IsDefault { get; init; }
}

/// <summary>
/// Represents a running audio stream (an app playing audio).
/// </summary>
public sealed class AudioStream
{
    public int Id { get; init; }
    public string AppName { get; init; } = string.Empty;
    public int SinkId { get; init; }
    public double Volume { get; init; }
    public bool Muted { get; init; }
}
