# Hypricing — Project Architecture

## Overview

Hypricing is a GUI settings manager for Hyprland. It orchestrates existing Linux tools
(hyprctl, wpctl, bluetoothctl, upower, powerprofilesctl) and manages Hyprland config files
directly. It does not replace any tool — it is a thin, composable layer on top of them.

**Stack:** .NET 10 · Avalonia UI 11 · AOT · Linux x64

---

## Solution Structure

```
Hypricing/
├── src/
│   ├── Hypricing.HyprlangParser/
│   ├── Hypricing.Core/
│   └── Hypricing.Desktop/
└── tests/
    ├── Hypricing.HyprlangParser.Tests/
    └── Hypricing.Core.Tests/
```

---

## Layer Overview

```
┌─────────────────────────────────────┐
│         Hypricing.Desktop           │  Avalonia UI — Views + ViewModels
├─────────────────────────────────────┤
│          Hypricing.Core             │  Business logic — Services + Models
├─────────────────────────────────────┤
│      Hypricing.HyprlangParser       │  Pure parser — text → AST → text
└─────────────────────────────────────┘
```

Dependencies flow **downward only**. Desktop depends on Core. Core depends on Parser.
Parser depends on nothing.

---

## 1. Hypricing.HyprlangParser

**Role:** Parse a single Hyprlang file into an AST. Modify it. Write it back.
**Constraints:** Pure library. No I/O. No file system access. No dependencies.

### Structure

```
Hypricing.HyprlangParser/
├── Lexer.cs                  # text → token stream
├── Parser.cs                 # token stream → AST
├── Writer.cs                 # AST → text
├── Nodes/
│   ├── ConfigNode.cs         # root node (list of top-level nodes)
│   ├── DeclarationNode.cs    # $var = value
│   ├── AssignmentNode.cs     # key = value
│   ├── KeywordNode.cs        # keyword = param1,param2,...
│   ├── SectionNode.cs        # name { children }
│   ├── ExecNode.cs           # exec-once / exec / exec-shutdown
│   ├── SourceNode.cs         # source = path
│   ├── CommentNode.cs        # # comment
│   ├── EmptyLineNode.cs      # blank line
│   └── RawNode.cs            # unrecognized → preserved verbatim
└── Exceptions/
    └── ParseException.cs
```

### Contract

```csharp
// Parse
ConfigNode config = HyprlangParser.Parse(string text);

// Modify
config.Declarations["myvar"].Value = "newvalue";

// Write back
string result = HyprlangWriter.Write(config);
```

### Key Rules

- Unknown content → `RawNode` (never lost, never modified)
- Round-trip: `Write(Parse(text)) == text` for unmodified configs
- No file I/O — caller provides the string, caller writes the file

---

## 2. Hypricing.Core

**Role:** Business logic. Knows about Hyprland, the file system, and external tools.
**Depends on:** `HyprlangParser`

### Structure

```
Hypricing.Core/
├── Services/
│   ├── HyprlandService.cs        # owns hyprland.conf lifecycle
│   ├── AudioService.cs           # wraps wpctl / pactl
│   ├── BluetoothService.cs       # wraps bluetoothctl
│   ├── PowerService.cs           # wraps upower + powerprofilesctl
│   └── IdleService.cs            # manages hypridle.conf
├── Semantic/
│   ├── OptionRegistry.cs         # known options catalog
│   └── Definitions/              # option definitions per section
│       ├── GeneralOptions.cs
│       ├── DecorationOptions.cs
│       └── ...
├── Models/
│   ├── Monitor.cs
│   ├── AudioDevice.cs
│   ├── BluetoothDevice.cs
│   ├── PowerProfile.cs
│   └── ExecEntry.cs
└── Infrastructure/
    ├── CliRunner.cs               # Process.Start wrapper
    └── ConfigFileLocator.cs       # resolves ~/.config/hypr/
```

### HyprlandService responsibilities

```
1. Locate hyprland.conf
2. Read all source= includes recursively
3. Parse each file via HyprlangParser
4. Provide unified view of the full config
5. On save → write back only the files that changed
6. Call hyprctl reload
7. Verify source= lines exist, repair if missing
```

### Semantic Layer (OptionRegistry)

Maps known options to their type and metadata:

```csharp
public record OptionDefinition(
    string Section,
    string Key,
    OptionType Type,
    object? Default,
    string Description
);
```

**Extensible by design.** Start with options you manage, ignore the rest.
Unknown options pass through as `RawNode` untouched.

### External Tool Map

| Service              | Tools used                          | Communication     |
|----------------------|-------------------------------------|-------------------|
| `HyprlandService`    | `hyprctl`                           | CLI + IPC socket  |
| `AudioService`       | `wpctl`, `pactl`                    | CLI stdout        |
| `BluetoothService`   | `bluetoothctl`                      | CLI stdout        |
| `PowerService`       | `powerprofilesctl`, `upower`        | CLI stdout        |
| `IdleService`        | `hypridle` (config only)            | Config file       |

All CLI calls go through `CliRunner` — one place to mock in tests.

---

## 3. Hypricing.Desktop

**Role:** Avalonia UI. Presents data from Core services. No business logic.
**Depends on:** `Hypricing.Core`
**Pattern:** MVVM (one ViewModel per page)

### Structure

```
Hypricing.Desktop/
├── App.axaml
├── Program.cs
├── Views/
│   ├── MainWindow.axaml
│   ├── DisplayView.axaml          # monitor drag-and-drop layout
│   ├── VariablesView.axaml        # $var declarations editor
│   ├── StartupView.axaml          # exec-once / exec manager
│   ├── AudioView.axaml
│   ├── BluetoothView.axaml
│   └── PowerView.axaml
├── ViewModels/
│   ├── MainWindowViewModel.cs
│   ├── DisplayViewModel.cs
│   ├── VariablesViewModel.cs
│   ├── StartupViewModel.cs
│   ├── AudioViewModel.cs
│   ├── BluetoothViewModel.cs
│   └── PowerViewModel.cs
└── Controls/
    └── MonitorCanvas.cs           # custom drag-and-drop monitor control
```

### Startup sequence

```
1. Check hyprland.conf exists
2. Check source= lines present → offer to add if missing
3. Load all services
4. Show main window
```

---

## 4. Tests

```
tests/
├── Hypricing.HyprlangParser.Tests/
│   └── (tests from PARSER_DESIGN.md test matrix)
└── Hypricing.Core.Tests/
    ├── HyprlandServiceTests.cs    # source= resolution, reload
    └── CliRunnerTests.cs          # mock CLI calls
```

---

## Versioned Delivery Plan

| Version | Scope |
|---------|-------|
| v0.1 | `HyprlangParser` — full parser + writer + tests passing |
| v0.2 | Variables page — read/write `$var` declarations |
| v0.3 | Display page — monitor layout drag-and-drop |
| v0.4 | Startup page — exec-once / exec manager |
| v0.5 | Audio page |
| v0.6 | Power + Battery page |
| v0.7 | Bluetooth page |
| v1.0 | Polish, AOT build, packaging |

---

## AOT Notes

- No runtime reflection anywhere
- `System.Text.Json` with source generators for all `hyprctl -j` deserialization
- Avalonia 11 AOT compatible with correct trimming config
- All node types are plain sealed classes
- `CliRunner` uses `Process.Start` — AOT safe

---

## Type Design Guidelines

### Use `sealed class` for

AST nodes — they are **mutable**, **polymorphic**, and **long-lived**:

```csharp
public sealed class SectionNode : ConfigNode
{
    public string Name { get; set; }
    public List<ConfigNode> Children { get; set; } = [];
}
```

- Mutated when user edits config
- Part of a tree (need base type)
- Value equality is meaningless here

### Use `record class` for

Read-only definitions and catalog entries — things you **compare by value** and **never mutate**:

```csharp
public record OptionDefinition(
    string Section,
    string Key,
    OptionType Type,
    object? Default,
    string Description
);
```

- Immutable once created
- Value equality makes sense (same section+key = same option)
- `with` expression useful for cloning with small changes

### Use `record struct` for

Small, short-lived value types with no heap pressure:

```csharp
public record struct Position(int X, int Y);
public record struct Resolution(int Width, int Height);
```

- Represent a single compound value
- Created and discarded frequently
- No polymorphism needed
- Stack allocated → zero GC pressure
