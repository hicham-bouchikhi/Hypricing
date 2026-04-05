# Hypricing

A GUI settings manager for [Hyprland](https://hyprland.org). Provides a graphical interface over existing Linux tools and manages Hyprland configuration files directly.

## Features

- **Variables** — add, edit, and remove `$var` declarations and `env` environment variables
- **Keybindings** — manage `bind`, `binde`, `bindm` and other bind variants
- **Display** — drag-and-drop monitor layout with edge snapping
- **Startup** — manage `exec`, `exec-once`, and `exec-shutdown` entries
- **Audio** — volume, mute, default device, stream routing (PipeWire + PulseAudio, extensible via JSON presets)
- **Backups** — create, restore, and delete zip backups of all config files
- **Multi-file support** — follows `source =` includes across config files
- **Native AOT** — 18MB self-contained binary, no runtime needed

## Screenshots

![Display](assets/display.png)

## Install

### AUR (Arch Linux)

Coming soon.

### From source

```bash
dotnet publish src/Hypricing.Desktop/Hypricing.Desktop.csproj -c Release -r linux-x64 --self-contained true -o publish
sudo cp publish/Hypricing.Desktop /usr/bin/hypricing
```

## Stack

- .NET 10
- Avalonia UI 11
- Native AOT
- Linux x64

## Roadmap

| Version | Scope |
|---|---|
| v0.1 | Parser, variables, keybindings, display, startup, backups, audio, Native AOT, AUR packaging |
| v0.2 | Power page (profiles, hypridle, battery) |
| v0.3 | Bluetooth page |
| v1.0 | Polish, themes, structured inputs |

## Project Structure

```
Hypricing/
├── src/
│   ├── Hypricing.HyprlangParser/   # Pure parser — text → AST → text
│   ├── Hypricing.Core/             # Business logic — services + models
│   └── Hypricing.Desktop/          # Avalonia UI — views + viewmodels
└── tests/
    ├── Hypricing.HyprlangParser.Tests/
    └── Hypricing.Core.Tests/
```

## License

See [LICENSE](LICENSE).
