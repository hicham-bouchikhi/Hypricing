# Hypricing

A GUI settings manager for [Hyprland](https://hyprland.org). Provides a graphical interface over existing Linux tools and manages Hyprland configuration files directly.

## Features

- **Variables** — add, edit, and remove `$var` declarations and `env` environment variables
- **Keybindings** — manage `bind`, `binde`, `bindm` and other bind variants
- **Display** — drag-and-drop monitor layout with edge snapping
- **Startup** — manage `exec`, `exec-once`, and `exec-shutdown` entries
- **Backups** — create, restore, and delete zip backups of all config files
- **Multi-file support** — follows `source =` includes across config files
- **Native AOT** — 18MB self-contained binary, no runtime needed

## Screenshots

![Display](assets/display.png)

## Install

### AUR (Arch Linux)

```bash
yay -S hypricing-git
```

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
| v0.1 | Parser, variables, keybindings, display, startup, backups, Native AOT, AUR packaging |
| v0.2 | Audio page |
| v0.3 | Power + Battery page |
| v0.4 | Bluetooth page |
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
