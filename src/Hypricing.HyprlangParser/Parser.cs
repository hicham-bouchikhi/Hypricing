using Hypricing.HyprlangParser.Exceptions;
using Hypricing.HyprlangParser.Nodes;

namespace Hypricing.HyprlangParser;

/// <summary>
/// Public entry point for parsing Hyprlang configuration text.
/// </summary>
public static class HyprlangParser
{
    /// <summary>
    /// Parses Hyprlang configuration text into an AST.
    /// </summary>
    /// <param name="text">The complete configuration file content.</param>
    /// <returns>The root <see cref="ConfigNode"/> containing all parsed nodes.</returns>
    public static ConfigNode Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var parser = new Parser(text);
        return parser.Parse();
    }
}

/// <summary>
/// Recursive descent parser for Hyprlang configuration files.
/// </summary>
internal sealed class Parser(string source)
{
    private readonly Lexer _lexer = new(source);
    private int _sectionDepth;

    private string Source => _lexer.Source;

    public ConfigNode Parse()
    {
        var config = new ConfigNode(Source);
        while (!_lexer.IsAtEnd)
        {
            config.Children.Add(ParseTopLevelNode());
        }
        return config;
    }

    private SyntaxNode ParseTopLevelNode()
    {
        int lineStart = _lexer.Position;
        _lexer.SkipWhitespace();

        if (_lexer.IsAtEnd)
        {
            // Trailing whitespace at end of file
            return new RawNode(Source[lineStart.._lexer.Position])
            {
                OriginalSpan = lineStart.._lexer.Position,
                IsDirty = false,
            };
        }

        char c = _lexer.Peek();

        if (c == '\n' || c == '\r')
        {
            _lexer.ConsumeNewLine();
            return new EmptyLineNode
            {
                OriginalSpan = lineStart.._lexer.Position,
                IsDirty = false,
            };
        }

        if (c == '#')
            return ParseComment(lineStart);

        if (c == '$')
            return ParseDeclaration(lineStart);

        if (c == '}')
        {
            // Unexpected } at top level → RawNode
            return ParseRawLine(lineStart);
        }

        if (Lexer.IsIdentChar(c))
            return ParseIdentifierLine(lineStart, topLevel: true);

        return ParseRawLine(lineStart);
    }

    private SyntaxNode? ParseSectionBodyNode()
    {
        var lineStart = _lexer.Position;
        _lexer.SkipWhitespace();

        if (_lexer.IsAtEnd)
            return null;

        var c = _lexer.Peek();

        if (c == '}')
            return null;

        if (c == '\n' || c == '\r')
        {
            _lexer.ConsumeNewLine();
            return new EmptyLineNode
            {
                OriginalSpan = lineStart.._lexer.Position,
                IsDirty = false,
            };
        }

        if (c == '#')
            return ParseComment(lineStart);

        // $var inside section → RawNode (declarations are top-level only)
        if (c == '$')
            return ParseRawLine(lineStart);

        if (Lexer.IsIdentChar(c))
            return ParseIdentifierLine(lineStart, topLevel: false);

        return ParseRawLine(lineStart);
    }

    private SyntaxNode ParseIdentifierLine(int lineStart, bool topLevel)
    {
        Range identRange = _lexer.ReadIdentifier();
        var ident = Source[identRange];

        _lexer.SkipWhitespace();

        if (_lexer.IsAtEnd)
        {
            // Bare identifier at end of file → RawNode
            return ParseRawLine(lineStart);
        }

        char next = _lexer.Peek();

        // Section: name {
        if (next == '{')
        {
            _lexer.Advance(); // consume {
            return ParseSection(lineStart, ident, device: null);
        }

        // Device section: name:device {
        if (next == ':')
        {
            int savedPos = _lexer.Position;
            _lexer.Advance(); // consume :
            if (!_lexer.IsAtEnd && Lexer.IsIdentChar(_lexer.Peek()))
            {
                Range deviceRange = _lexer.ReadIdentifier();
                var device = Source[deviceRange];
                _lexer.SkipWhitespace();
                if (!_lexer.IsAtEnd && _lexer.Peek() == '{')
                {
                    _lexer.Advance(); // consume {
                    return ParseSection(lineStart, ident, device);
                }
            }
            // Not a device section — rewind and fall through
            _lexer.Position = savedPos;
        }

        // Assignment, keyword, or directive: name = ...
        if (next == '=')
        {
            _lexer.Advance(); // consume =
            _lexer.SkipWhitespace();

            if (topLevel && TryGetExecVariant(ident, out var variant))
                return ParseExecValue(lineStart, variant);

            if (topLevel && ident == "source")
                return ParseSourceValue(lineStart);

            return ParseAssignmentOrKeywordValue(lineStart, ident);
        }

        // Unknown pattern → RawNode
        return ParseRawLine(lineStart);
    }

    private CommentNode ParseComment(int lineStart)
    {
        // Position is at #
        int textStart = _lexer.Position;
        _lexer.SkipToEndOfLine();
        var text = Source[textStart.._lexer.Position];
        _lexer.ConsumeNewLine();

        return new CommentNode(text)
        {
            OriginalSpan = lineStart.._lexer.Position,
            IsDirty = false,
        };
    }

    private SyntaxNode ParseDeclaration(int lineStart)
    {
        _lexer.Advance(); // consume $
        Range nameRange = _lexer.ReadIdentifier();
        if (nameRange.Start.Value == nameRange.End.Value)
        {
            // $ not followed by identifier → RawNode
            return ParseRawLine(lineStart);
        }

        var name = Source[nameRange];
        _lexer.SkipWhitespace();

        if (!_lexer.TryConsume('='))
        {
            // $name without = → treat as raw line
            return ParseRawLine(lineStart);
        }

        _lexer.SkipWhitespace();
        var (value, comment) = _lexer.ReadValue();
        _lexer.ConsumeNewLine();

        return new DeclarationNode(name, value, comment)
        {
            OriginalSpan = lineStart.._lexer.Position,
            IsDirty = false,
        };
    }

    private SourceNode ParseSourceValue(int lineStart)
    {
        // Position is after "source = " (= and whitespace consumed)
        var (path, comment) = _lexer.ReadValue();
        _lexer.ConsumeNewLine();

        return new SourceNode(path, comment)
        {
            OriginalSpan = lineStart.._lexer.Position,
            IsDirty = false,
        };
    }

    private ExecNode ParseExecValue(int lineStart, ExecVariant variant)
    {
        // Position is after "exec-variant = " (= and whitespace consumed)
        string? rules = null;

        if (!_lexer.IsAtEnd && _lexer.Peek() == '[')
        {
            _lexer.Advance(); // consume [
            int rulesStart = _lexer.Position;
            while (!_lexer.IsAtEnd && _lexer.Peek() != ']' && !_lexer.IsAtNewLine())
                _lexer.Advance();

            int rulesEnd = _lexer.Position;
            if (!_lexer.IsAtEnd && _lexer.Peek() == ']')
                _lexer.Advance(); // consume ]

            rules = Source.AsSpan(rulesStart, rulesEnd - rulesStart).Trim().ToString();
            _lexer.SkipWhitespace();
        }

        var (command, comment) = _lexer.ReadValue();
        _lexer.ConsumeNewLine();

        return new ExecNode(variant, command, rules, comment)
        {
            OriginalSpan = lineStart.._lexer.Position,
            IsDirty = false,
        };
    }

    private SyntaxNode ParseAssignmentOrKeywordValue(int lineStart, string key)
    {
        // Position is after "key = " (= and whitespace consumed)
        bool stopAtBrace = _sectionDepth > 0;
        var (value, comment) = _lexer.ReadValue(stopAtBrace);
        _lexer.ConsumeNewLine();
        int nodeEnd = _lexer.Position;

        if (Lexer.HasTopLevelCommas(value))
        {
            return new KeywordNode(key, value, comment)
            {
                OriginalSpan = lineStart..nodeEnd,
                IsDirty = false,
            };
        }

        return new AssignmentNode(key, value, comment)
        {
            OriginalSpan = lineStart..nodeEnd,
            IsDirty = false,
        };
    }

    private SectionNode ParseSection(int lineStart, string name, string? device)
    {
        // { was already consumed. Position is right after {.
        _lexer.SkipWhitespace();

        // Check for inline comment on header line
        string? headerComment = null;
        if (!_lexer.IsAtEnd && _lexer.Peek() == '#')
        {
            int commentStart = _lexer.Position;
            _lexer.SkipToEndOfLine();
            headerComment = Source[commentStart.._lexer.Position].TrimEnd();
        }

        // Consume newline after header (if present — might be single-line section)
        if (!_lexer.IsAtEnd && _lexer.IsAtNewLine())
            _lexer.ConsumeNewLine();

        int headerEnd = _lexer.Position;

        var section = new SectionNode(name, device, headerComment)
        {
            HeaderSpan = lineStart..headerEnd,
            IsDirty = false,
        };

        _sectionDepth++;

        // Parse body
        while (true)
        {
            if (_lexer.IsAtEnd)
            {
                _sectionDepth--;
                throw new ParseException($"Missing closing '}}' for section '{name}'", lineStart);
            }

            // Record position before potential whitespace
            int pos = _lexer.Position;
            _lexer.SkipWhitespace();

            if (_lexer.IsAtEnd)
            {
                _sectionDepth--;
                throw new ParseException($"Missing closing '}}' for section '{name}'", lineStart);
            }

            if (_lexer.Peek() == '}')
            {
                // Found closing brace — include leading whitespace in closing span
                _lexer.Advance(); // consume }
                _lexer.SkipWhitespace();
                if (!_lexer.IsAtEnd && _lexer.IsAtNewLine())
                    _lexer.ConsumeNewLine();
                section.ClosingSpan = pos.._lexer.Position;
                break;
            }

            if (_lexer.IsAtNewLine())
            {
                // Empty line (possibly with leading whitespace)
                _lexer.ConsumeNewLine();
                section.Children.Add(new EmptyLineNode
                {
                    OriginalSpan = pos.._lexer.Position,
                    IsDirty = false,
                });
                continue;
            }

            // Content line — rewind to include leading whitespace in node span
            _lexer.Position = pos;
            var node = ParseSectionBodyNode();
            if (node is null)
                break;
            section.Children.Add(node);
        }

        _sectionDepth--;
        section.OriginalSpan = lineStart.._lexer.Position;
        return section;
    }

    private RawNode ParseRawLine(int lineStart)
    {
        _lexer.SkipToEndOfLine();
        var text = Source[lineStart.._lexer.Position];
        _lexer.ConsumeNewLine();

        return new RawNode(text)
        {
            OriginalSpan = lineStart.._lexer.Position,
            IsDirty = false,
        };
    }

    private static bool TryGetExecVariant(string identifier, out ExecVariant variant) =>
        ExecNode.TryParseVariant(identifier, out variant);
}
