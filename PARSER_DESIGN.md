# Hypricing — HyprlangParser Design Document

## Overview

The `HyprlangParser` is the foundation of Hypricing. It is a standalone .NET 10 library
that parses `hyprland.conf` (Hyprlang syntax) into an in-memory AST and allows modification.
The caller is responsible for all file I/O — the parser only operates on strings.

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
Lexer        → stream of tokens (ReadOnlySpan<char>, zero allocation)
   ↓
Parser       → AST (recursive descent, nodes allocated only on confirmation)
   ↓
[modify]     → mutate nodes in memory
   ↓
Writer       → serialize AST back to text (single StringBuilder pass)
```

### Implementation Strategy

The parser is a **hand-written recursive descent parser**. No parser combinator libraries,
no code generation. The grammar from
[tree-sitter-hyprlang](https://github.com/tree-sitter-grammars/tree-sitter-hyprlang)
is the authoritative syntax reference and the test oracle — if tree-sitter parses it,
this parser must too.

The top-level dispatch:

```csharp
private ConfigNode ParseLine() => currentToken switch
{
    TokenType.Dollar    => ParseDeclaration(),
    TokenType.Source    => ParseSource(),
    TokenType.Exec      => ParseExec(),
    TokenType.Name      => PeekAhead() == '{' ? ParseSection() : ParseAssignmentOrKeyword(),
    TokenType.Comment   => ParseComment(),
    TokenType.NewLine   => ParseEmptyLine(),
    _                   => ParseRaw()   // → RawNode
};
```

### Lexer

- Operates on `ReadOnlySpan<char>` — reads directly from the original string buffer, zero copies
- Tokens are represented as `(TokenType, Range)` — two integers referencing a position in
  the original input, no heap allocation during tokenization

### AST Node Types

| Node              | Example                                       | Type        |
|-------------------|-----------------------------------------------|-------------|
| `DeclarationNode` | `$myvar = somevalue`                          | sealed class |
| `AssignmentNode`  | `gaps_in = 5`                                 | sealed class |
| `KeywordNode`     | `bind = SUPER,Q,killactive`                   | sealed class |
| `SectionNode`     | `general { ... }`                             | sealed class |
| `ExecNode`        | `exec-once = waybar`                          | sealed class |
| `SourceNode`      | `source = ~/.config/hypr/other.conf`          | sealed class |
| `CommentNode`     | `# this is a comment`                         | sealed class |
| `EmptyLineNode`   | (blank line)                                  | sealed class |
| `RawNode`         | anything unrecognized → preserved verbatim    | sealed class |

All node types are `sealed` to enable JIT devirtualization.

### Key Principle

> **What is not understood is preserved verbatim.**
> The parser never loses data. Unknown lines become `RawNode` and are written back as-is.
> The cost of not recognizing a line is near zero — a `RawNode` is just a `Range` into
> the original input buffer.

---

## Writer Rules

- Reconstruct file from AST nodes in order via a single `StringBuilder` pass
- Managed nodes → regenerated from model
- `CommentNode` / `EmptyLineNode` / `RawNode` → written back exactly as read via `string.AsSpan()` slice
- No string concatenation in loops
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

## Performance

Hyprlang configuration files are typically 100–500 lines. The parser is designed to handle
a full file in the **low microsecond range** on modern hardware. The file read from disk
will always dominate the total time — the parse itself is not the bottleneck.

Key performance decisions:

| Decision | Reason |
|---|---|
| `ReadOnlySpan<char>` in lexer | Zero allocation, no string copies |
| Tokens as `(TokenType, Range)` | Two ints — no heap pressure during tokenization |
| Recursive descent | Straight-line execution, no backtracking |
| `sealed` on all node types | JIT can devirtualize all virtual dispatch |
| `RawNode` as a `Range` | Unrecognized lines cost near zero |
| Single `StringBuilder` in writer | No intermediate string allocations |
| No LINQ in hot paths | Avoids iterator allocation and indirection |

The goal is not to match Rust's zero-cost abstractions, but to remain within the same
order of magnitude through disciplined use of `Span<T>` and avoiding unnecessary allocation.

---

## AOT Constraints

- No runtime reflection
- No `dynamic` types
- All node types are plain `sealed class` with no attributes requiring reflection

---

## Value Handling

The parser stores all values as **raw strings**. It does not interpret them.

```
gaps_in = 5           → AssignmentNode { Key = "gaps_in", Value = "5" }
enabled = true        → AssignmentNode { Key = "enabled",  Value = "true" }
col.active = rgba(...)→ AssignmentNode { Key = "col.active", Value = "rgba(...)" }
```

Type interpretation is the exclusive responsibility of the `OptionRegistry` in
`Hypricing.Core`. This keeps the parser free of semantic knowledge and ensures
there is a single source of truth for what each option means.

The only structural distinction the parser makes is between node kinds
(`AssignmentNode` vs `KeywordNode` vs `SectionNode` etc.) — never between value types.

---

## Out of Scope for Parser

The parser does NOT:
- Follow `source =` includes (that is the responsibility of `HyprlandService`)
- Validate semantic correctness (e.g. whether a monitor name is valid)
- Interpret value types (that is the responsibility of `OptionRegistry`)
- Understand what each keyword's params mean (that is the semantic layer)
