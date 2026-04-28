using System.Text;

namespace Hypricing.HyprlangParser;

/// <summary>
/// Character-level scanner over Hyprlang source text.
/// Provides low-level scanning utilities that the parser drives.
/// Operates on the original string to enable Range-based references.
/// </summary>
internal sealed class Lexer(string source)
{
    public string Source => source;
    public int Position { get; set; } = 0;
    public bool IsAtEnd => Position >= source.Length;

    public char Peek() => Position < source.Length ? source[Position] : '\0';

    public void Advance() => Position++;

    public bool TryConsume(char c)
    {
        if (Position < source.Length && source[Position] == c)
        {
            Position++;
            return true;
        }
        return false;
    }

    /// <summary>Skip spaces and tabs (not newlines). Returns count of characters skipped.</summary>
    public int SkipWhitespace()
    {
        int start = Position;
        while (Position < source.Length && source[Position] is ' ' or '\t')
            Position++;
        return Position - start;
    }

    public bool IsAtNewLine()
    {
        if (Position >= source.Length) return false;
        return source[Position] == '\n' || (source[Position] == '\r' && Position + 1 < source.Length && source[Position + 1] == '\n');
    }

    /// <summary>Consume a newline (\n or \r\n) if present. Returns characters consumed (0, 1, or 2).</summary>
    public int ConsumeNewLine()
    {
        if (Position >= source.Length) return 0;
        if (source[Position] == '\r' && Position + 1 < source.Length && source[Position + 1] == '\n')
        {
            Position += 2;
            return 2;
        }
        if (source[Position] == '\n')
        {
            Position++;
            return 1;
        }
        return 0;
    }

    /// <summary>Read a run of identifier characters. Returns the range.</summary>
    public Range ReadIdentifier()
    {
        int start = Position;
        while (Position < source.Length && IsIdentChar(source[Position]))
            Position++;
        return start..Position;
    }

    /// <summary>Advance to end of line (position at newline or EOF).</summary>
    public void SkipToEndOfLine()
    {
        while (Position < source.Length && !IsNewLineChar(Position))
            Position++;
    }

    /// <summary>
    /// Reads value content after '=' (whitespace already skipped).
    /// Handles ## escape sequences and # inline comments.
    /// Position advances to the newline or EOF after the value and optional comment.
    /// </summary>
    /// <param name="stopAtBrace">If true, also treats '}' as a line terminator (for single-line sections).</param>
    public (string Value, string? InlineComment) ReadValue(bool stopAtBrace = false)
    {
        int start = Position;
        int lineEnd = FindLineEnd(start, stopAtBrace);

        int? commentStart = null;
        bool hasEscape = false;

        int i = start;
        while (i < lineEnd)
        {
            if (source[i] == '#')
            {
                if (i + 1 < lineEnd && source[i + 1] == '#')
                {
                    // ## escape → literal #
                    hasEscape = true;
                    i += 2;
                }
                else if (i == start || source[i - 1] is ' ' or '\t')
                {
                    // # at value start (preceded by consumed whitespace) or after whitespace → inline comment
                    commentStart = i;
                    break;
                }
                else
                {
                    // # not preceded by whitespace → literal
                    i++;
                }
            }
            else
            {
                i++;
            }
        }

        int valueTextEnd = commentStart ?? lineEnd;
        ReadOnlySpan<char> rawSpan = source.AsSpan(start, valueTextEnd - start).TrimEnd();
        string value = hasEscape ? Unescape(rawSpan) : rawSpan.ToString();

        string? comment = null;
        if (commentStart.HasValue)
        {
            // Comment extends to actual end of line (not stopped by brace)
            int commentEnd = FindLineEnd(commentStart.Value, stopAtBrace: false);
            comment = source.AsSpan(commentStart.Value, commentEnd - commentStart.Value).TrimEnd().ToString();
        }

        Position = lineEnd;
        return (value, comment);
    }

    /// <summary>
    /// Checks whether a value string contains top-level commas (not inside parentheses).
    /// Used to distinguish AssignmentNode from KeywordNode.
    /// </summary>
    public static bool HasTopLevelCommas(string value)
    {
        int parenDepth = 0;
        for (int i = 0; i < value.Length; i++)
        {
            switch (value[i])
            {
                case '(': parenDepth++; break;
                case ')' when parenDepth > 0: parenDepth--; break;
                case ',' when parenDepth == 0: return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Escapes '#' characters in a value for writing.
    /// Every '#' becomes '##' to prevent misinterpretation as inline comments.
    /// </summary>
    public static string EscapeValue(string value)
    {
        if (!value.Contains('#'))
            return value;

        return value.Replace("#", "##");
    }

    public static bool IsIdentChar(char c) =>
        c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9')
            or '_' or '-' or '.';

    private int FindLineEnd(int from, bool stopAtBrace = false)
    {
        int i = from;
        while (i < source.Length)
        {
            if (IsNewLineChar(i)) break;
            if (stopAtBrace && source[i] == '}') break;
            i++;
        }
        return i;
    }

    private bool IsNewLineChar(int index)
    {
        if (index >= source.Length) return false;
        if (source[index] == '\n') return true;
        if (source[index] == '\r' && index + 1 < source.Length && source[index + 1] == '\n') return true;
        return false;
    }

    private static string Unescape(ReadOnlySpan<char> raw)
    {
        var sb = new StringBuilder(raw.Length);
        for (int i = 0; i < raw.Length; i++)
        {
            if (raw[i] == '#' && i + 1 < raw.Length && raw[i + 1] == '#')
            {
                sb.Append('#');
                i++; // skip second #
            }
            else
            {
                sb.Append(raw[i]);
            }
        }
        return sb.ToString();
    }
}
