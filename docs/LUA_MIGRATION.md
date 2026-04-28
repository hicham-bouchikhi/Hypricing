# Hypricing — Lua Migration (Hyprland ≥ 0.55)

## Background

Hyprland 0.55 deprecates Hyprlang (`.conf`) in favour of Lua (`.lua`).
Hypricing currently targets 0.54 and below. This document describes the architecture
for adding Lua support while keeping the 0.54 path fully intact.

---

## Version Detection

`HyprlandService` will detect the active Hyprland version on startup via `CliRunner`:

```
hyprctl version
```

The first line contains the version string (e.g. `Hyprland 0.55.0`).
A helper `HyprlandVersionDetector` in `Hypricing.Core.Infrastructure` parses this string
and returns a `Version` object. `HyprlandService` stores it and uses it to choose the
correct config backend for all subsequent operations.

| Detected version | Config file                      | Parser backend            |
|------------------|----------------------------------|---------------------------|
| `< 0.55`         | `~/.config/hypr/hyprland.conf`   | `Hypricing.HyprlangParser` |
| `>= 0.55`        | `~/.config/hypr/hyprland.lua`    | `Hypricing.LuaParser`      |

The Desktop layer never sees the version — the switch happens entirely inside
`HyprlandService`. ViewModels and Views are unchanged.

---

## New Project: `Hypricing.LuaParser`

A new pure library, mirroring the role of `Hypricing.HyprlangParser` but for Lua.

**Constraints:** Pure library. No I/O. No file system access. No external dependencies. AOT-safe.

### Solution Structure

```
Hypricing/
├── src/
│   ├── Hypricing.HyprlangParser/     ← unchanged (0.54)
│   ├── Hypricing.LuaParser/          ← NEW (0.55+)
│   ├── Hypricing.Core/               ← references both parsers
│   └── Hypricing.Desktop/            ← unchanged
└── tests/
    ├── Hypricing.HyprlangParser.Tests/
    ├── Hypricing.LuaParser.Tests/    ← NEW
    └── Hypricing.Core.Tests/
```

### Pipeline

```
text file
   ↓
Lexer        → character-level scanner (ReadOnlySpan<char>, Range-based)
   ↓
Parser       → AST (recursive descent, top-level hl.* call dispatch)
   ↓
[modify]     → mutate nodes in memory
   ↓
Writer       → serialize AST back to text (single StringBuilder pass)
```

Same pipeline and same invariants as `HyprlangParser`. The only difference is the
syntax being parsed.

---

## The Lua API (Hyprland ≥ 0.55)

Hyprland 0.55 config is a real Lua script. All Hyprland-specific calls go through
the `hl` global table:

```lua
-- Options (replaces sections like general { }, decoration { })
hl.config({
    general  = { border_size = 2, gaps_in = 5 },
    decoration = { rounding = 12,
                   blur = { enabled = true, size = 3 } },
})

-- Monitor
hl.monitor({ output = "DP-1", mode = "2560x1440@144", position = "0x0", scale = 1 })
hl.monitor({ output = "", mode = "preferred", position = "auto", scale = 1 })

-- Keybind
hl.bind("SUPER + Q", hl.dsp.exec_cmd("kitty"))
hl.bind("SUPER + SHIFT + Q", hl.dsp.killactive())

-- Autostart
hl.on("hyprland.start", function()
    hl.dsp.exec_cmd("waybar")()
    hl.dsp.exec_cmd("mako")()
end)

-- Environment variable
hl.env("XCURSOR_SIZE", "24")

-- Window rule
hl.window_rule({ match = { class = "pavucontrol" }, float = true })

-- Workspace rule
hl.workspace_rule("1", { monitor = "DP-1", default = true })

-- Per-device input
hl.device({ name = "logitech-mx-master-3", sensitivity = -0.3 })

-- Permission
hl.permission({ binary = "/usr/bin/grim", type = "screencopy", mode = "allow" })

-- Source another Lua file (replaces source = ...)
require("monitors")   -- loads ~/.config/hypr/monitors.lua
```

---

## AST Node Types

| Node               | Lua construct                                   | Key fields                                     |
|--------------------|-------------------------------------------------|------------------------------------------------|
| `HlConfigNode`     | `hl.config({ section = { key = val } })`        | `Sections` (list of `HlConfigSection`)         |
| `HlMonitorNode`    | `hl.monitor({ output = ..., mode = ... })`      | `Output`, `Mode`, `Position`, `Scale`, `Extra` |
| `HlEnvNode`        | `hl.env("KEY", "VALUE")`                        | `Key`, `Value`                                 |
| `HlBindNode`       | `hl.bind("MOD + KEY", action)`                  | `RawArgs` (parsed lazily)                      |
| `HlExecNode`       | `hl.on("hyprland.start", function() … end)`     | `Event`, `Commands`                            |
| `HlWindowRuleNode` | `hl.window_rule({ match = { … }, … })`          | `RawArgs`                                      |
| `HlDeviceNode`     | `hl.device({ name = …, … })`                    | `RawArgs`                                      |
| `HlPermissionNode` | `hl.permission({ binary = …, … })`              | `RawArgs`                                      |
| `HlRequireNode`    | `require("path")`                               | `Path`                                         |
| `LuaCommentNode`   | `-- comment`                                    | verbatim (Range)                               |
| `LuaEmptyLineNode` | blank line                                      | —                                              |
| `LuaRawNode`       | anything unrecognized → preserved verbatim      | raw text (Range)                               |

### `HlConfigSection` (child of `HlConfigNode`)

```
HlConfigSection
  Name        → "general", "decoration", "input", …
  Options     → list of (Key, Value) — raw strings, same as HyprlangParser
  SubSections → list of HlConfigSection   (e.g. blur inside decoration)
```

All values are stored as **raw strings** — same invariant as `HyprlangParser`.
Type interpretation is the exclusive responsibility of `OptionRegistry` in Core.

---

## Round-trip Guarantee

> `Write(Parse(text)) == text` for unmodified ASTs.

Implementation strategy is identical to `HyprlangParser`:

- Each top-level node stores an `OriginalSpan` (`Range`) into the original source string.
- The writer slices `originalText[span]` for clean nodes — no reconstruction.
- Only dirty (modified or programmatically created) nodes are serialized from fields.

The challenge vs Hyprlang: `hl.*` calls are multi-line. The span covers the entire
call including all interior whitespace and newlines, so the round-trip guarantee holds
the same way — any content the parser did not understand is preserved inside the span.

### Multi-line span extraction

The parser scans to the **matching closing parenthesis** of each `hl.*()` call,
tracking depth of `()`, `{}`, and `function ... end` blocks, and correctly
skipping string literals so that delimiters inside strings are ignored.

---

## Key Principle (same as HyprlangParser)

> **What is not understood is preserved verbatim.**
> The parser never loses data. Unknown statements become `LuaRawNode` and are written
> back as-is. The cost of not recognizing a statement is near zero — a `LuaRawNode`
> is just a `Range` into the original input buffer.

---

## `HyprlandService` Dual-Backend Design

```csharp
// Core — HyprlandService (simplified)

private IConfigBackend _backend;   // set after version detection

public async Task LoadAsync(…)
{
    var version = await HyprlandVersionDetector.DetectAsync(cli, ct);
    _backend = version >= new Version(0, 55)
        ? new LuaConfigBackend(cli)
        : new HyprlangConfigBackend(cli);

    await _backend.LoadAsync(path, ct);
}
```

Both backends expose the same interface (`IConfigBackend`), which mirrors the current
`HyprlandService` API surface:

```csharp
interface IConfigBackend
{
    IReadOnlyList<string> ConfigPaths { get; }
    Task LoadAsync(string? path, CancellationToken ct);
    Task SaveAsync(CancellationToken ct);

    // Options
    string? GetSectionValue(string section, string? device, string key);
    void SetSectionValue(string section, string? device, string key, string? value);

    // Keywords
    IReadOnlyList<…> GetMonitors();
    void AddMonitor(…);
    void RemoveMonitor(…);

    IReadOnlyList<…> GetExecEntries();
    void AddExecEntry(…);
    void RemoveExecEntry(…);

    IReadOnlyList<…> GetEnvironmentVariables();
    IReadOnlyList<…> GetKeybindings();
    // …
}
```

The Desktop layer calls `HyprlandService` methods exactly as today — it never
touches the backend directly and is unaware of the version fork.

---

## `require()` vs `source =`

| Concept | Hyprlang (< 0.55) | Lua (>= 0.55) |
|---------|--------------------|----------------|
| Include another file | `source = ~/.config/hypr/other.conf` | `require("other")` |
| Resolution | Absolute or relative path | Relative to `~/.config/hypr/`, no `.lua` extension |

`LuaConfigBackend.LoadAsync` follows `require()` calls the same way
`HyprlangConfigBackend` follows `source =` includes — recursively parsing all
included files into a unified in-memory view.

---

## `OptionRegistry` Impact

None. `OptionRegistry` maps config keys to `OptionType` using `"section.key"` strings.
This mapping is independent of the config format and remains unchanged.

The only practical difference: Lua section names are the same as Hyprlang section names
(`general`, `decoration`, `input`, etc.) because Hyprland preserved them intentionally.

---

## AOT Constraints

Same as the rest of the codebase:
- No runtime reflection in `Hypricing.LuaParser`
- No `dynamic` types
- All node types are plain `sealed class` with no attributes requiring reflection
- `IConfigBackend` can be used without reflection — it is a plain interface with
  concrete implementations resolved at startup

---

## Versioned Delivery Plan

| Version | Scope |
|---------|-------|
| v1.0    | Ship current 0.54 Hyprlang support |
| v1.1    | `HyprlandVersionDetector` + `IConfigBackend` interface + `HyprlangConfigBackend` wrapper |
| v1.2    | `Hypricing.LuaParser` — `HlConfigNode`, `HlMonitorNode`, `HlEnvNode`, `HlRequireNode`, round-trip tests |
| v1.3    | `LuaConfigBackend` — full lifecycle: load, modify, save |
| v1.4    | `HlBindNode`, `HlExecNode` — keybindings + autostart pages on Lua |
| v1.5    | Polish, edge cases, full test matrix |

