# Hypricing

A GUI settings manager for [Hyprland](https://hyprland.org). Provides a graphical interface over existing Linux tools and manages Hyprland configuration files directly.

## Stack

- .NET 10
- Avalonia UI 11
- Native AOT
- Linux x64

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

Dependencies flow downward only. `Desktop` → `Core` → `HyprlangParser`.

## HyprlangParser

Standalone library that parses `hyprland.conf` (Hyprlang syntax) into an in-memory AST, allows modification, and serializes back to text. No I/O, no external dependencies.

```csharp
// Parse
var config = HyprlangParser.Parse(File.ReadAllText("hyprland.conf"));

// Read
var section = config.Children.OfType<SectionNode>().First(s => s.Name == "general");
var gaps = section.Children.OfType<AssignmentNode>().First(a => a.Key == "gaps_in");
Console.WriteLine(gaps.Value); // "5"

// Modify
gaps.Value = "8";

// Write back — only the modified line changes, everything else is byte-for-byte identical
File.WriteAllText("hyprland.conf", HyprlangWriter.Write(config));
```

### Supported Syntax

| Node | Example |
|---|---|
| Declaration | `$myvar = SUPER` |
| Assignment | `gaps_in = 5` |
| Keyword | `bind = SUPER,Q,killactive` |
| Section | `general { ... }` / `device:kb { ... }` |
| Exec | `exec-once = [workspace 1 silent] kitty` |
| Source | `source = ~/.config/hypr/keybinds.conf` |
| Comment | `# comment` / `gaps_in = 5 # inline` |
| Raw | anything unrecognized — preserved verbatim |

### Guarantees

- **Round-trip fidelity**: `Write(Parse(text)) == text` for unmodified ASTs
- **No data loss**: unrecognized content becomes a `RawNode` and is written back as-is
- **No I/O**: the caller is responsible for reading and writing files

## Core

Business logic layer that manages the Hyprland configuration lifecycle.

- **ConfigFileLocator** — resolves `hyprland.conf` via `$HYPRLAND_CONFIG`, `$XDG_CONFIG_HOME`, or `~/.config/hypr/`
- **CliRunner** — thin `Process.Start` wrapper, virtual methods for test subclassing
- **HyprlandService** — load, modify, save config + `hyprctl reload`

## Desktop

Avalonia UI with sidebar navigation and MVVM pattern.

**Current pages:**
- **Variables** — view and edit `$var` declarations and `env` environment variables
- Display, Startup, Audio, Power, Bluetooth — placeholders for future versions

## Building

```bash
dotnet build
```

## Testing

```bash
dotnet test
```

## Running

```bash
dotnet run --project src/Hypricing.Desktop
```

## Roadmap

| Version | Scope |
|---|---|
| v0.1 | HyprlangParser — parser, writer, tests |
| v0.2 | Variables page — read/write `$var` declarations and `env` variables |
| v0.3 | Display page — monitor layout drag-and-drop |
| v0.4 | Startup page — exec manager |
| v0.5 | Audio page |
| v0.6 | Power + Battery page |
| v0.7 | Bluetooth page |
| v1.0 | Polish, AOT build, packaging |

## License

See [LICENSE](LICENSE).
