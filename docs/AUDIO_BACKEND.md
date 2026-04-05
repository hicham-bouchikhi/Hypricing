# Audio Backend

Hypricing uses a **contract-based, JSON-driven** audio backend. Instead of hardcoding
support for PipeWire or PulseAudio, every audio operation is described in a JSON
**preset** file. This means:

- PipeWire and PulseAudio work out of the box (shipped presets).
- Any other audio stack can be supported by writing a single JSON file.
- No recompilation needed.

## How it works

```
┌──────────────┐      ┌──────────────────┐      ┌───────────────┐
│  AudioView   │────▶│  JsonAudioBackend│────▶│  CLI tools    │
│  (Avalonia)  │      │  (reads preset)  │      │  wpctl, pactl │
└──────────────┘      └──────────────────┘      └───────────────┘
                              │
                      reads commands &
                      field mappings from
                              │
                      ┌───────▼───────┐
                      │  audio.json   │
                      │  (preset)     │
                      └───────────────┘
```

1. **AudioService** looks for a user preset at `~/.config/hypricing/audio.json`.
2. If none exists, it auto-detects from built-in presets by checking which CLI tool
   is installed (e.g., `which wpctl`). PipeWire is tried first.
3. The detected preset is written to `~/.config/hypricing/audio.json` so users can
   edit it later.
4. **JsonAudioBackend** reads the preset and executes the described shell commands,
   parsing their JSON output using the field mappings.

## Contract

All backends implement `IAudioBackend`:

| Method | Description |
|---|---|
| `ListSinksAsync` | List output devices (speakers, headphones, HDMI) |
| `ListSourcesAsync` | List input devices (microphones) |
| `ListStreamsAsync` | List running audio streams (apps playing sound) |
| `SetVolumeAsync` | Set device volume (0.0 – 1.5, i.e., up to 150%) |
| `ToggleMuteAsync` | Toggle mute on a device |
| `SetDefaultSinkAsync` | Set the default output device |
| `SetDefaultSourceAsync` | Set the default input device |
| `MoveStreamAsync` | Move a stream to a different output device |
| `SetStreamVolumeAsync` | Set volume for a specific stream |

Data models:

- **AudioDevice** — `Id`, `Name`, `Description`, `Volume`, `Muted`, `IsDefault`
- **AudioStream** — `Id`, `AppName`, `SinkId`, `Volume`, `Muted`

## Preset format

A preset is a JSON file with two top-level fields:

```json
{
  "name": "My Audio Stack",
  "detect": "my-audio-tool",
  "commands": { ... }
}
```

| Field | Description |
|---|---|
| `name` | Display name shown in the UI status bar |
| `detect` | CLI tool name — Hypricing runs `which <detect>` to check availability |
| `commands` | Object containing all audio operations (see below) |

### Query commands

Query commands (listing devices/streams) have this shape:

```json
{
  "run": "pactl -f json list sinks",
  "format": "json",
  "fields": {
    "id": "index",
    "name": "name",
    "description": "description",
    "volume": "volume.*.value_percent",
    "muted": "mute"
  }
}
```

| Field | Description |
|---|---|
| `run` | Shell command to execute. Must return a JSON array. |
| `format` | Output format. Currently only `"json"` is supported. |
| `fields` | Maps contract fields to JSON paths using **dot-notation**. |

#### Dot-notation paths

Paths walk into nested JSON objects. For example, given this `pactl` output:

```json
{
  "index": 42,
  "description": "Built-in Audio",
  "volume": {
    "front-left": {
      "value_percent": "74%"
    }
  }
}
```

The path `volume.front-left.value_percent` resolves to `"74%"`.

#### Wildcard `*`

Some devices use different channel names (`front-left` for stereo, `mono` for
mono microphones or Bluetooth). Use `*` to match the first child regardless of
its key name:

```
volume.*.value_percent
```

This resolves to `"74%"` whether the channel is called `front-left`, `mono`, or
anything else.

Volume values can be either `"74%"` (percentage string) or `0.74` (decimal) — both
are handled automatically.

#### Required fields per command

**listSinks / listSources:**
- `id` — device identifier (integer)
- `name` — internal name
- `description` — human-readable label
- `volume` — current volume level
- `muted` — boolean mute state

**listStreams:**
- `id` — stream identifier
- `appName` — application name
- `sinkId` — which output device the stream is routed to
- `volume` — stream volume
- `muted` — mute state

### Action commands

Action commands are plain strings with placeholders:

```json
{
  "setVolume": "pactl set-sink-volume {id} {volumePct}",
  "toggleMute": "pactl set-sink-mute {id} toggle",
  "setDefaultSink": "pactl set-default-sink {name}",
  "setDefaultSource": "pactl set-default-source {name}",
  "getDefaultSink": "pactl get-default-sink",
  "getDefaultSource": "pactl get-default-source",
  "moveStream": "pactl move-sink-input {streamId} {sinkId}",
  "setStreamVolume": "pactl set-sink-input-volume {streamId} {volumePct}"
}
```

| Placeholder | Replaced with |
|---|---|
| `{id}` | Device ID |
| `{name}` | Device name (internal identifier) |
| `{volume}` | Volume as decimal (e.g., `0.74`) |
| `{volumePct}` | Volume as percentage (e.g., `75%`) |
| `{streamId}` | Stream ID |
| `{sinkId}` | Target sink ID |

`getDefaultSink` and `getDefaultSource` are special — they take no placeholders and
must return the **name** of the default device (matched against the `name` field from
listing). This is how the "Default" badge is determined in the UI.

## Shipped presets

### PipeWire (`audio-pipewire.json`)

For systems using PipeWire (most modern Wayland setups).

- **Detected via:** `wpctl` (but all commands use `pactl` for consistent IDs)
- **Listing:** `pactl -f json`
- **Device control:** `pactl set-sink-volume`, `pactl set-sink-mute`, `pactl set-default-sink`
- **Stream control:** `pactl move-sink-input`, `pactl set-sink-input-volume`

### PulseAudio (`audio-pulseaudio.json`)

For systems using PulseAudio directly.

- **Detected via:** `pactl`
- **Listing:** `pactl -f json`
- **Device control:** `pactl set-sink-volume`, `pactl set-sink-mute`, `pactl set-default-sink`
- **Stream control:** `pactl move-sink-input`, `pactl set-sink-input-volume`

## Writing a custom preset

1. Copy one of the shipped presets as a starting point:
   ```sh
   cp ~/.config/hypricing/audio.json ~/.config/hypricing/audio.json.bak
   ```

2. Edit `~/.config/hypricing/audio.json`:
   - Change `name` to describe your setup.
   - Change `detect` to a tool from your stack.
   - Update each command under `commands` to use your CLI tools.
   - Update `fields` mappings to match your tool's JSON output.

3. To figure out the field mappings, run the list command manually and inspect the
   output:
   ```sh
   pactl -f json list sinks | jq '.[0]'
   ```
   Then trace the path to each field you need.

4. Restart Hypricing. The status bar at the bottom of the Audio page shows which
   preset is active.

## Tools that don't output JSON

The preset format currently requires `"format": "json"` — query commands must return
a JSON array. Both `pactl -f json` (PipeWire and PulseAudio) support this natively,
which covers the vast majority of Linux desktops.

If your audio tool outputs plain text (e.g., `amixer`, ALSA-only setups), you'll
need a small wrapper script that converts the output to JSON. For example:

```sh
#!/bin/sh
# amixer-sinks.sh — wraps amixer output into the JSON format Hypricing expects
amixer scontrols | awk '
  BEGIN { printf "[" }
  # ... parse and emit JSON objects ...
  END   { printf "]" }
'
```

Then reference it in your preset:

```json
"listSinks": {
  "run": "sh /path/to/amixer-sinks.sh",
  "format": "json",
  "fields": {
    "id": "index",
    "name": "name",
    "description": "description",
    "volume": "volume",
    "muted": "muted"
  }
}
```

The wrapper script is responsible for producing a JSON array that matches the field
paths you define. This keeps the backend generic — Hypricing never needs to know
about the specifics of your audio tool.

## File locations

| File | Location |
|---|---|
| User preset | `$XDG_CONFIG_HOME/hypricing/audio.json` (default: `~/.config/hypricing/audio.json`) |
| Built-in presets | Embedded in the binary at build time (`src/Hypricing.Core/Presets/audio-*.json`) |
