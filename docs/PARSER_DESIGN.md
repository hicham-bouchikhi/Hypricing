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
    TokenType.Exec      => ParseExec(),   // exec, exec-once, exec-shutdown, execr, execr-once
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

| Node              | Example                                                  | Key fields                                              |
|-------------------|----------------------------------------------------------|---------------------------------------------------------|
| `DeclarationNode` | `$myvar = somevalue`                                     | `Name`, `Value`, `InlineComment?`                       |
| `AssignmentNode`  | `gaps_in = 5`                                            | `Key`, `Value`, `InlineComment?`                        |
| `KeywordNode`     | `bind = SUPER,Q,killactive`                              | `Keyword`, `Params`, `InlineComment?`                   |
| `SectionNode`     | `general { ... }` / `device:kb { ... }`                  | `Name`, `Device?`, `Children`, `InlineComment?`         |
| `ExecNode`        | `exec-once = [workspace 1 silent] kitty`                 | `Variant` (enum), `Rules?`, `Command`, `InlineComment?` |
| `SourceNode`      | `source = ~/.config/hypr/other.conf`                     | `Path`, `InlineComment?`                                |
| `CommentNode`     | `# this is a comment`                                    | raw line (Range)                                        |
| `EmptyLineNode`   | (blank line)                                             | —                                                       |
| `RawNode`         | anything unrecognized → preserved verbatim               | raw line (Range)                                        |

All node types are `sealed` to enable JIT devirtualization. `ExecNode.Variant` is an enum:
```csharp
enum ExecVariant { Once, OnceRestart, Reload, ExecrReload, Shutdown }
```
Each enum value maps 1:1 to its source keyword so the writer can reconstruct the exact original
keyword for round-trip fidelity (`exec` → `Reload`, `execr` → `ExecrReload`).

### AssignmentNode vs KeywordNode

The parser distinguishes assignments from keywords by the presence of **top-level commas**
in the value — commas not enclosed in parentheses:

```
gaps_in = 5                          → AssignmentNode  (no commas)
col.active_border = rgba(...) 45deg  → AssignmentNode  (no top-level commas)
monitor = DP-1,1920x1080@144,0x0,1  → KeywordNode     (top-level commas)
env = XCURSOR_SIZE,24                → KeywordNode     (top-level comma)
bind = SUPER,Q,killactive            → KeywordNode     (top-level commas)
```

The lexer must track parenthesis depth when scanning the value to correctly identify
top-level commas. Commas inside `rgba(...)` or `rgb(...)` do not trigger keyword detection.

### Memory Model

The original input string must remain alive for the lifetime of the AST. `RawNode` and
unmodified nodes store `Range` values referencing positions in the original input buffer —
they do not copy their content. The writer resolves these ranges back to text via
`originalInput.AsSpan(range)`.

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
| K-07 | `bind = SUPER,,killactive`                   | `bind`           | `SUPER,,killactive` (empty middle param is valid) |

---

### 4. Sections

`DeclarationNode` (`$var = value`) is **only valid at the top level**. The grammar does not
allow declarations inside sections — a `$var = x` line inside a section body is a parse error
and must produce a `RawNode`, not a `DeclarationNode`. `exec` and `source` directives are also
top-level only.

| ID   | Input                                         | Expected Name | Children count |
|------|-----------------------------------------------|---------------|----------------|
| S-01 | `general { gaps_in = 5 }`                     | `general`     | 1              |
| S-02 | `general { gaps_in = 5\n gaps_out = 10 }`     | `general`     | 2              |
| S-03 | `decoration { blur { enabled = true } }`      | `decoration`  | 1 (SectionNode)|
| S-04 | `input { kb_layout = us\n }`                  | `input`       | 1              |
| S-05 | `general { }`                                 | `general`     | 0              |
| S-06 | `general { $var = x\n gaps_in = 5 }`          | `general`     | 2 (RawNode + AssignmentNode — declaration not valid inside section) |

---

### 5. Exec directives

`ExecNode` has three fields: `Command` (string), `Variant` (enum), and `Rules` (optional string).

`Variant` covers all five keywords:

| Keyword          | Variant        |
|------------------|----------------|
| `exec-once`      | `Once`         |
| `exec`           | `Reload`       |
| `exec-shutdown`  | `Shutdown`     |
| `execr-once`     | `OnceRestart`  |
| `execr`          | `ExecrReload`  |

Only `exec-once` and `exec` optionally accept a **rules prefix** `[rule1; rule2]` before the
command (per the grammar). This is commonly used in Hyprland to start apps on specific workspaces:
```
exec-once = [workspace 1 silent] kitty
exec-once = [float; workspace 2] foot
```
The `Rules` field stores the raw bracket content (`workspace 1 silent` / `float; workspace 2`).
`exec-shutdown`, `execr`, and `execr-once` do **not** accept a rules prefix.

| ID   | Input                                              | Variant        | Rules                      | Command                    |
|------|----------------------------------------------------|----------------|----------------------------|----------------------------|
| E-01 | `exec-once = waybar`                               | `Once`         | null                       | `waybar`                   |
| E-02 | `exec-once = dunst`                                | `Once`         | null                       | `dunst`                    |
| E-03 | `exec = ~/.config/hypr/start.sh`                   | `Reload`       | null                       | `~/.config/hypr/start.sh`  |
| E-04 | `exec-shutdown = poweroff`                         | `Shutdown`     | null                       | `poweroff`                 |
| E-05 | `execr-once = waybar`                              | `OnceRestart`  | null                       | `waybar`                   |
| E-06 | `execr = ~/.config/hypr/start.sh`                  | `Reload`       | null                       | `~/.config/hypr/start.sh`  |
| E-07 | `exec-once = [workspace 1 silent] kitty`           | `Once`         | `workspace 1 silent`       | `kitty`                    |
| E-08 | `exec-once = [float; workspace 2] foot`            | `Once`         | `float; workspace 2`       | `foot`                     |
| E-09 | `execr = hyprpaper`                                | `ExecrReload`  | null                       | `hyprpaper`                |

---

### 6. Source directives

| ID   | Input                                        | Expected Path                    |
|------|----------------------------------------------|----------------------------------|
| SR-01 | `source = ~/.config/hypr/monitors.conf`     | `~/.config/hypr/monitors.conf`   |
| SR-02 | `source = ~/.config/hypr/keybinds.conf`     | `~/.config/hypr/keybinds.conf`   |

---

### 7. Comments

Inline comments are stripped from the parsed value but preserved in a separate `InlineComment`
field on the node (nullable string). The writer emits `{value} {inline_comment}` to guarantee
round-trip fidelity. A `#` is only treated as starting an inline comment when it is preceded by
whitespace or appears at the start of a token boundary — `##` is an escape sequence that produces
a literal `#` in the value and does **not** start a comment.

```
gaps_in = 5 # inline    → value = "5",   InlineComment = "# inline"
title = My app ## note  → value = "My app # note",  InlineComment = null
```

`AssignmentNode`, `KeywordNode`, `DeclarationNode`, `ExecNode`, and `SourceNode` all carry
an optional `InlineComment` field. `SectionNode` header lines may also carry one.

| ID   | Input                        | Expected Node   | Value / Content              | InlineComment  |
|------|------------------------------|-----------------|------------------------------|----------------|
| C-01 | `# this is a comment`        | `CommentNode`   | `# this is a comment`        | —              |
| C-02 | `# gaps_in = 5`              | `CommentNode`   | `# gaps_in = 5`              | —              |
| C-03 | `gaps_in = 5 # inline`       | `AssignmentNode`| value = `5`                  | `# inline`     |
| C-04 | `title = My app ## note`     | `AssignmentNode`| value = `My app # note`      | null           |
| C-05 | `bind = SUPER,Q,exec ## cmd` | `KeywordNode`   | params = `SUPER,Q,exec # cmd`| null           |

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
| EC-09 | `title = My app ## note`               | `AssignmentNode`, value = `My app # note`, no inline comment |
| EC-10 | `exec-once = [float; ws 2] kitty`      | `ExecNode`, Variant = `Once`, Rules = `float; ws 2`, Command = `kitty` |
| EC-11 | `execr-once = waybar`                  | `ExecNode`, Variant = `OnceRestart`    |
| EC-12 | `$var = x` inside a section body       | `RawNode` (declarations not valid in sections) |
| EC-13 | `bind = SUPER,,killactive`             | `KeywordNode`, Params = `SUPER,,killactive` (empty middle param) |

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
enabled = 0           → AssignmentNode { Key = "enabled",  Value = "0" }
col.active = rgba(...)→ AssignmentNode { Key = "col.active", Value = "rgba(...)" }
```

Type interpretation is the exclusive responsibility of the `OptionRegistry` in
`Hypricing.Core`. This keeps the parser free of semantic knowledge and ensures
there is a single source of truth for what each option means.

The only structural distinction the parser makes is between node kinds
(`AssignmentNode` vs `KeywordNode` vs `SectionNode` etc.) — never between value types.

### Bool values

Hyprland accepts all of the following as valid boolean values:
`true` `false` `yes` `no` `on` `off` `0` `1`

The parser emits all of them as raw strings. The `OptionRegistry` is responsible for
recognizing all 8 forms when interpreting a `Bool` option.

---

## Out of Scope for Parser

The parser does NOT:
- Follow `source =` includes (that is the responsibility of `HyprlandService`)
- Validate semantic correctness (e.g. whether a monitor name is valid)
- Interpret value types (that is the responsibility of `OptionRegistry`)
- Understand what each keyword's params mean (that is the semantic layer)
