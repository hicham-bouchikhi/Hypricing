# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What This Is

Hypricing is a GUI settings manager for Hyprland (Linux Wayland compositor). It parses and edits `hyprland.conf` directly and wraps Linux CLI tools (`hyprctl`, `pactl`, `wpctl`, `bluetoothctl`, `upower`, `powerprofilesctl`) behind a graphical interface. Ships as a single Native AOT binary.

**Stack:** .NET 10 · Avalonia UI 11 · Native AOT · Linux x64 · xUnit

## Build & Test Commands

```bash
# Build (debug)
dotnet build

# Run the app
dotnet run --project src/Hypricing.Desktop

# Run all tests
dotnet test

# Run a single test project
dotnet test tests/Hypricing.HyprlangParser.Tests
dotnet test tests/Hypricing.Core.Tests

# Run a single test by name
dotnet test --filter "FullyQualifiedName~DeclarationTests.ParsesSimpleDeclaration"

# Publish AOT binary
dotnet publish src/Hypricing.Desktop/Hypricing.Desktop.csproj -c Release -r linux-x64 --self-contained true -o publish
```

The solution file is `Hypricing.slnx` (XML-based .NET solution format).

## Architecture

Three layers, dependencies flow downward only:

```
Desktop  (Avalonia UI — Views + ViewModels, MVVM)
   ↓
Core     (Business logic — Services, Models, Audio backends)
   ↓
HyprlangParser  (Pure library — text → AST → text, no I/O, no dependencies)
```

### HyprlangParser

Hand-written recursive descent parser for Hyprlang syntax. Key invariants:
- **Round-trip guarantee:** `Write(Parse(text)) == text` for unmodified ASTs. Unmodified nodes store `Range` references into the original input — the writer slices the original string, not reconstructed text.
- **Nothing is lost:** Unrecognized lines become `RawNode` and are preserved verbatim.
- **Original input must stay alive** for the AST's lifetime — nodes hold `Range` references, not copies.
- `$var = x` declarations and `exec`/`source` directives are top-level only — inside sections they become `RawNode`.
- Assignment vs keyword distinction: top-level commas (not inside parens) → `KeywordNode`, otherwise → `AssignmentNode`.

### Core

- `HyprlandService` owns the full config lifecycle: locate → resolve `source=` includes → parse → unified view → write modified files → `hyprctl reload`.
- `AudioService` / `JsonAudioBackend` — contract-based audio via JSON presets (embedded in `src/Hypricing.Core/Presets/`). Auto-detects PipeWire vs PulseAudio; users can override at `~/.config/hypricing/audio.json`.
- All CLI invocations go through `CliRunner` (single seam for testing).
- `OptionRegistry` maps config keys to typed metadata (`OptionType` enum). To add a new Hyprland option, add one registry entry — the UI renders the right widget automatically.

### Desktop

MVVM pattern. One ViewModel per page, bound to a corresponding View. No business logic in this layer.

## AOT Constraints

These apply everywhere in the codebase:
- No runtime reflection. `System.Text.Json` must use source generators.
- No `dynamic` types.
- No `Enum.GetValues<T>()` — not AOT-safe.
- `InvariantGlobalization` is enabled in the Desktop project.

## Locale

The user's system uses `LC_NUMERIC=fr_FR` (comma decimal separator). Always use `CultureInfo.InvariantCulture` for all non-display number formatting/parsing (volume percentages, config values, JSON paths, etc.).

## Parser Values

The parser stores all values as raw strings. Type interpretation belongs exclusively to `OptionRegistry` in Core. The parser has no semantic knowledge of what any option means.
