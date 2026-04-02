# Hypricing — Architecture

## Overview

Hypricing is a GUI settings manager for Hyprland. It provides a graphical interface over
existing Linux tools (`hyprctl`, `wpctl`, `bluetoothctl`, `upower`, `powerprofilesctl`) and
manages Hyprland configuration files directly. Hypricing does not replace any underlying tool —
it is a composable orchestration layer.

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

Dependencies flow **downward only**. `Desktop` depends on `Core`. `Core` depends on
`HyprlangParser`. `HyprlangParser` has no dependencies.

---

## 1. Hypricing.HyprlangParser

**Role:** Parse a single Hyprlang file into an AST, allow in-memory modification, and
serialize the AST back to text.

**Constraints:** Pure library. No I/O. No file system access. No external dependencies.

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
│   └── RawNode.cs            # unrecognized content — preserved verbatim
└── Exceptions/
    └── ParseException.cs
```

### Public API

```csharp
// Parse
ConfigNode config = HyprlangParser.Parse(string text);

// Modify
config.Declarations["myvar"].Value = "newvalue";

// Write back
string result = HyprlangWriter.Write(config);
```

### Invariants

- Any content not recognized by the parser becomes a `RawNode` and is written back verbatim.
  No data is ever lost.
- Round-trip guarantee: `Write(Parse(text)) == text` for unmodified ASTs.
- The library performs no file I/O. The caller is responsible for reading and writing files.

### Implementation

The parser is a hand-written **recursive descent parser** operating on `ReadOnlySpan<char>`
to minimize heap allocations. See [Performance Notes](#performance-notes) for details.

---

## 2. Hypricing.Core

**Role:** Business logic layer. Manages the Hyprland configuration lifecycle and
communicates with external system tools.

**Depends on:** `Hypricing.HyprlangParser`

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
│   └── Definitions/
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

### HyprlandService

`HyprlandService` is the only component with full knowledge of the configuration lifecycle:

1. Locate `hyprland.conf` via `ConfigFileLocator`
2. Recursively resolve and parse all `source=` includes via `HyprlangParser`
3. Provide a unified in-memory view of the full configuration
4. On save, write back only files that were modified
5. Invoke `hyprctl reload`
6. Verify that managed `source=` lines are present in `hyprland.conf` and offer to repair
   them if missing

### Semantic Layer

The `OptionRegistry` maps known configuration options to their type and metadata:

```csharp
public record OptionDefinition(
    string Section,
    string Key,
    OptionType Type,
    object? Default,
    string Description
);
```

Options not present in the registry pass through the parser as `RawNode` and are never
modified. The registry is designed to grow incrementally — contributors can add definitions
for new options or third-party plugins without touching parser or UI code.

### External Tool Map

| Service              | Tools                               | Communication     |
|----------------------|-------------------------------------|-------------------|
| `HyprlandService`    | `hyprctl`                           | CLI + IPC socket  |
| `AudioService`       | `wpctl`, `pactl`                    | CLI stdout        |
| `BluetoothService`   | `bluetoothctl`                      | CLI stdout        |
| `PowerService`       | `powerprofilesctl`, `upower`        | CLI stdout        |
| `IdleService`        | `hypridle` (config only)            | Config file       |

All CLI invocations are routed through `CliRunner`, which is the single point of
abstraction for process execution and the primary seam for testing.

---

## 3. Hypricing.Desktop

**Role:** Avalonia UI layer. Consumes `Core` services and presents data to the user.
Contains no business logic.

**Depends on:** `Hypricing.Core`

**Pattern:** MVVM — one `ViewModel` per page, bound to a corresponding `View`.

### Structure

```
Hypricing.Desktop/
├── App.axaml
├── Program.cs
├── Views/
│   ├── MainWindow.axaml
│   ├── DisplayView.axaml
│   ├── VariablesView.axaml
│   ├── StartupView.axaml
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
    └── MonitorCanvas.cs           # custom drag-and-drop monitor layout control
```

### Startup Sequence

1. Verify `hyprland.conf` exists
2. Verify managed `source=` lines are present — offer to add them if missing
3. Initialize services
4. Display main window

---

## 4. Tests

```
tests/
├── Hypricing.HyprlangParser.Tests/
│   └── (see PARSER_DESIGN.md for full test matrix)
└── Hypricing.Core.Tests/
    ├── HyprlandServiceTests.cs
    └── CliRunnerTests.cs
```

---

## Versioned Delivery Plan

| Version | Scope |
|---------|-------|
| v0.1 | `HyprlangParser` — parser, writer, full test matrix passing |
| v0.2 | Variables page — read/write `$var` declarations |
| v0.3 | Display page — monitor layout drag-and-drop |
| v0.4 | Startup page — `exec-once` / `exec` manager |
| v0.5 | Audio page |
| v0.6 | Power + Battery page |
| v0.7 | Bluetooth page |
| v1.0 | Polish, AOT build, packaging |

---

## Type Design Guidelines

### `sealed class` — AST nodes

AST nodes are mutable, participate in a type hierarchy, and are long-lived. Reference
semantics are appropriate. Value equality is not meaningful.

```csharp
public sealed class SectionNode : ConfigNode
{
    public string Name { get; set; }
    public List<ConfigNode> Children { get; set; } = [];
}
```

### `record class` — definitions and catalog entries

Immutable descriptors where value equality is meaningful. Suitable for `OptionDefinition`
and similar read-only catalog types.

```csharp
public record OptionDefinition(
    string Section,
    string Key,
    OptionType Type,
    object? Default,
    string Description
);
```

### `record struct` — small value types

Compound values with no identity semantics, short-lived, and allocation-sensitive.

```csharp
public record struct Position(int X, int Y);
public record struct Resolution(int Width, int Height);
```

---

## Performance Notes

`HyprlangParser` is implemented as a hand-written recursive descent parser. The following
constraints apply to its internals to ensure minimal overhead:

- The lexer operates on `ReadOnlySpan<char>`. No strings are allocated during tokenization.
  Tokens are represented as `(TokenType, Range)` — two integers referencing a position in
  the original input buffer.
- The parser builds the AST from those ranges, allocating node objects only when a node is
  confirmed complete.
- Node child lists (`SectionNode.Children`) are pre-allocated with an estimated capacity to
  avoid repeated resizing.
- `sealed` is applied to all node types to enable JIT devirtualization.
- The writer uses `StringBuilder` or a stack-based equivalent. No intermediate string
  concatenation occurs in the serialization path.
- LINQ is not used in any hot path within the lexer or parser.

Hyprlang configuration files are typically 100–500 lines. At this scale, a compliant
implementation should parse a full file in the low microsecond range on modern hardware.
The goal is not to match Rust's zero-cost abstractions, but to remain within the same
order of magnitude through disciplined use of `Span<T>` and avoiding unnecessary allocation.

---

## AOT Constraints

- Runtime reflection is not permitted anywhere in the codebase.
- `System.Text.Json` deserialization of `hyprctl -j` output must use source generators.
- Avalonia 11 is AOT-compatible when trimming is configured correctly.
- `CliRunner` uses `Process.Start` which is AOT-safe.
