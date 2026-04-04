using System.Text;

namespace Hypricing.HyprlangParser;

/// <summary>
/// Character-level scanner over Hyprlang source text.
/// Provides low-level scanning utilities that the parser drives.
/// Operates on the original string to enable Range-based references.
/// </summary>
internal sealed class Lexer
{
    private readonly string _source;
    private int _pos;

    public Lexer(string source)
    {
        _source = source;
        _pos = 0;
    }

    public string Source => _source;
    public int Position { get => _pos; set => _pos = value; }
    public bool IsAtEnd => _pos >= _source.Length;

    public char Peek() => _pos < _source.Length ? _source[_pos] : '\0';

    public void Advance() => _pos++;

    public bool TryConsume(char c)
    {
        if (_pos < _source.Length && _source[_pos] == c)
        {
            _pos++;
            return true;
        }
        return false;
    }

    /// <summary>Skip spaces and tabs (not newlines). Returns count of characters skipped.</summary>
    public int SkipWhitespace()
    {
        int start = _pos;
        while (_pos < _source.Length && _source[_pos] is ' ' or '\t')
            _pos++;
        return _pos - start;
    }

    public bool IsAtNewLine()
    {
        if (_pos >= _source.Length) return false;
        return _source[_pos] == '\n' || (_source[_pos] == '\r' && _pos + 1 < _source.Length && _source[_pos + 1] == '\n');
    }

    /// <summary>Consume a newline (\n or \r\n) if present. Returns characters consumed (0, 1, or 2).</summary>
    public int ConsumeNewLine()
    {
        if (_pos >= _source.Length) return 0;
        if (_source[_pos] == '\r' && _pos + 1 < _source.Length && _source[_pos + 1] == '\n')
        {
            _pos += 2;
            return 2;
        }
        if (_source[_pos] == '\n')
        {
            _pos++;
            return 1;
        }
        return 0;
    }

    /// <summary>Read a run of identifier characters. Returns the range.</summary>
    public Range ReadIdentifier()
    {
        int start = _pos;
        while (_pos < _source.Length && IsIdentChar(_source[_pos]))
            _pos++;
        return start.._pos;
    }

    /// <summary>Advance to end of line (position at newline or EOF).</summary>
    public void SkipToEndOfLine()
    {
        while (_pos < _source.Length && !IsNewLineChar(_pos))
            _pos++;
    }

    /// <summary>
    /// Reads value content after '=' (whitespace already skipped).
    /// Handles ## escape sequences and # inline comments.
    /// Position advances to the newline or EOF after the value and optional comment.
    /// </summary>
    /// <param name="stopAtBrace">If true, also treats '}' as a line terminator (for single-line sections).</param>
    public (string Value, string? InlineComment) ReadValue(bool stopAtBrace = false)
    {
        int start = _pos;
        int lineEnd = FindLineEnd(start, stopAtBrace);

        int? commentStart = null;
        bool hasEscape = false;

        int i = start;
        while (i < lineEnd)
        {
            if (_source[i] == '#')
            {
                if (i + 1 < lineEnd && _source[i + 1] == '#')
                {
                    // ## escape → literal #
                    hasEscape = true;
                    i += 2;
                }
                else if (i == start || _source[i - 1] is ' ' or '\t')
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
        ReadOnlySpan<char> rawSpan = _source.AsSpan(start, valueTextEnd - start).TrimEnd();
        string value = hasEscape ? Unescape(rawSpan) : rawSpan.ToString();

        string? comment = null;
        if (commentStart.HasValue)
        {
            // Comment extends to actual end of line (not stopped by brace)
            int commentEnd = FindLineEnd(commentStart.Value, stopAtBrace: false);
            comment = _source.AsSpan(commentStart.Value, commentEnd - commentStart.Value).TrimEnd().ToString();
        }

        _pos = lineEnd;
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
        while (i < _source.Length)
        {
            if (IsNewLineChar(i)) break;
            if (stopAtBrace && _source[i] == '}') break;
            i++;
        }
        return i;
    }

    private bool IsNewLineChar(int index)
    {
        if (index >= _source.Length) return false;
        if (_source[index] == '\n') return true;
        if (_source[index] == '\r' && index + 1 < _source.Length && _source[index + 1] == '\n') return true;
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
