# Hyprising — HyprlangParser Design Document

## Overview

The `HyprlangParser` is the foundation of Hyprising. It is a standalone .NET 10 library
that parses `hyprland.conf` (Hyprlang syntax) into an in-memory AST, allows modification,
and writes back to disk preserving everything it does not manage.

## References

- [tree-sitter-hyprlang](https://github.com/tree-sitter-grammars/tree-sitter-hyprlang) — grammar specification used as the authoritative syntax reference
- [Hyprland Wiki — Variables](https://wiki.hypr.land/Configuring/Variables/) — semantic meaning of all configuration options
- [Hyprland Wiki — Keywords](https://wiki.hypr.land/Configuring/Keywords/) — semantic meaning of all keywords (`bind`, `monitor`, `env`, `exec-once`, etc.)
- [hyprwm/hyprlang](https://github.com/hyprwm/hyprlang) — official C++ Hyprlang implementation

---

## Architecture

### Pipeline

```
text file
   ↓
Lexer        → stream of tokens
   ↓
Parser       → AST (tree of nodes)
   ↓
[modify]     → mutate nodes in memory
   ↓
Writer       → serialize AST back to text
```

### AST Node Types

| Node            | Example                            |
|-----------------|------------------------------------|
| `DeclarationNode` | `$myvar = somevalue`             |
| `AssignmentNode`  | `gaps_in = 5`                    |
| `KeywordNode`     | `bind = SUPER,Q,killactive`      |
| `SectionNode`     | `general { ... }`                |
| `ExecNode`        | `exec-once = waybar`             |
| `SourceNode`      | `source = ~/.config/hypr/other.conf` |
| `CommentNode`     | `# this is a comment`            |
| `EmptyLineNode`   | (blank line)                     |
| `RawNode`         | anything unrecognized → preserved verbatim |

### Key Principle

> **What is not understood is preserved verbatim.**
> The parser never loses data. Unknown lines become `RawNode` and are written back as-is.

---

## Writer Rules

- Reconstruct file from AST nodes in order
- Managed nodes → regenerated from model
- `CommentNode` / `EmptyLineNode` / `RawNode` → written back exactly as read
- No trailing newline added or removed unless originally present

---

## Test Matrix

### 1. Declarations (`$variable = value`)

| ID   | Input                        | Expected Node       | Expected Name | Expected Value |
|------|------------------------------|---------------------|---------------|----------------|
| D-01 | `$myvar = hello`             | `DeclarationNode`   | `myvar`       | `hello`        |
| D-02 | `$cursor_size = 24`          | `DeclarationNode`   | `cursor_size` | `24`           |
| D-03 | `$mod = SUPER`               | `DeclarationNode`   | `mod`         | `SUPER`        |
| D-04 | `$empty =`                   | `DeclarationNode`   | `empty`       | `""`           |
| D-05 | `$var = value with spaces`   | `DeclarationNode`   | `var`         | `value with spaces` |
| D-06 | `$var=nospace`               | `DeclarationNode`   | `var`         | `nospace`      |

---

### 2. Assignments (`key = value`)

| ID   | Input                        | Expected Key   | Expected Value |
|------|------------------------------|----------------|----------------|
| A-01 | `gaps_in = 5`                | `gaps_in`      | `5`            |
| A-02 | `gaps_out = 20`              | `gaps_out`     | `20`           |
| A-03 | `col.active_border = rgba(33ccffee) rgba(00ff99ee) 45deg` | `col.active_border` | `rgba(33ccffee) rgba(00ff99ee) 45deg` |
| A-04 | `enabled = true`             | `enabled`      | `true`         |
| A-05 | `enabled = false`            | `enabled`      | `false`        |
| A-06 | `rounding = 0`               | `rounding`     | `0`            |
| A-07 | `key =`                      | `key`          | `""`           |

---

### 3. Keywords (`keyword = params`)

| ID   | Input                                        | Expected Keyword | Expected Params |
|------|----------------------------------------------|------------------|-----------------|
| K-01 | `bind = SUPER,Q,killactive`                  | `bind`           | `SUPER,Q,killactive` |
| K-02 | `monitor = DP-1,1920x1080@144,0x0,1`         | `monitor`        | `DP-1,1920x1080@144,0x0,1` |
| K-03 | `env = XCURSOR_SIZE,24`                      | `env`            | `XCURSOR_SIZE,24` |
| K-04 | `env = QT_QPA_PLATFORM,wayland`              | `env`            | `QT_QPA_PLATFORM,wayland` |
| K-05 | `windowrule = float,^(pavucontrol)$`         | `windowrule`     | `float,^(pavucontrol)$` |
| K-06 | `bind = $mod,Return,exec,kitty`              | `bind`           | `$mod,Return,exec,kitty` |

---

### 4. Sections

| ID   | Input                                         | Expected Name | Children count |
|------|-----------------------------------------------|---------------|----------------|
| S-01 | `general { gaps_in = 5 }`                     | `general`     | 1              |
| S-02 | `general { gaps_in = 5\n gaps_out = 10 }`     | `general`     | 2              |
| S-03 | `decoration { blur { enabled = true } }`      | `decoration`  | 1 (SectionNode)|
| S-04 | `input { kb_layout = us\n }`                  | `input`       | 1              |
| S-05 | `general { }`                                 | `general`     | 0              |

---

### 5. Exec directives

| ID   | Input                          | Expected Type  | Expected Command |
|------|--------------------------------|----------------|------------------|
| E-01 | `exec-once = waybar`           | `ExecNode`     | `waybar`, Once=true |
| E-02 | `exec-once = dunst`            | `ExecNode`     | `dunst`, Once=true |
| E-03 | `exec = ~/.config/hypr/start.sh` | `ExecNode`   | `~/.config/hypr/start.sh`, Once=false |
| E-04 | `exec-shutdown = poweroff`     | `ExecNode`     | `poweroff`, Shutdown=true |

---

### 6. Source directives

| ID   | Input                                        | Expected Path                    |
|------|----------------------------------------------|----------------------------------|
| SR-01 | `source = ~/.config/hypr/monitors.conf`     | `~/.config/hypr/monitors.conf`   |
| SR-02 | `source = ~/.config/hypr/keybinds.conf`     | `~/.config/hypr/keybinds.conf`   |

---

### 7. Comments

| ID   | Input                        | Expected Node   | Preserved verbatim |
|------|------------------------------|-----------------|--------------------|
| C-01 | `# this is a comment`        | `CommentNode`   | yes                |
| C-02 | `# gaps_in = 5`              | `CommentNode`   | yes (not parsed as assignment) |
| C-03 | `gaps_in = 5 # inline`       | `AssignmentNode`| value = `5`, comment preserved |

---

### 8. Empty lines

| ID   | Input | Expected Node    | Preserved |
|------|-------|------------------|-----------|
| BL-01 | `\n` | `EmptyLineNode`  | yes       |
| BL-02 | multiple blank lines | multiple `EmptyLineNode` | yes |

---

### 9. Round-trip integrity

> Parse a file → write it back → the output must be byte-for-byte identical to the input (for files with no modifications).

| ID   | Input file                        | Expected output          |
|------|-----------------------------------|--------------------------|
| RT-01 | Minimal config (3 lines)         | Identical to input       |
| RT-02 | Config with comments              | Comments preserved       |
| RT-03 | Config with blank lines           | Blank lines preserved    |
| RT-04 | Config with nested sections       | Structure preserved      |
| RT-05 | Full example hyprland.conf        | Identical to input       |

---

### 10. Modification + write-back

| ID   | Action                                      | Expected output                        |
|------|---------------------------------------------|----------------------------------------|
| M-01 | Change `$myvar` value → write back          | Only that line changed                 |
| M-02 | Add new `DeclarationNode` → write back      | New line appended in declarations      |
| M-03 | Remove `exec-once = waybar` → write back    | Line gone, rest intact                 |
| M-04 | Change `gaps_in` inside section → write back | Only that value changed               |
| M-05 | Add new `exec-once` → write back            | New exec-once line present             |

---

### 11. Edge cases

| ID   | Input                                    | Expected behavior                      |
|------|------------------------------------------|----------------------------------------|
| EC-01 | Completely empty file                   | Empty AST, writes empty file           |
| EC-02 | File with only comments                 | All `CommentNode`, round-trip identical|
| EC-03 | Unknown/future keyword                  | `RawNode`, preserved verbatim          |
| EC-04 | Deeply nested sections (3+ levels)      | Parsed correctly                       |
| EC-05 | `source=` with no space around `=`      | Parsed correctly                       |
| EC-06 | Windows line endings (CRLF)             | Handled gracefully                     |
| EC-07 | Missing closing `}`                     | Meaningful parse error thrown          |
| EC-08 | `device:my-keyboard { }`               | `SectionNode` with device name         |

---

## AOT Constraints

- No runtime reflection
- No `dynamic` types
- JSON deserialization (for `hyprctl -j`) uses `System.Text.Json` source generators
- All node types are plain sealed classes with no attributes requiring reflection

---

## Out of Scope for Parser

The parser does NOT:
- Follow `source =` includes (that is the responsibility of `HyprlandService`)
- Validate semantic correctness (e.g. whether a monitor name is valid)
- Understand what each keyword's params mean (that is the semantic layer)
