namespace Hypricing.HyprlangParser.Exceptions;

/// <summary>
/// Thrown when the parser encounters a structural error (e.g. missing closing brace).
/// </summary>
public sealed class ParseException : Exception
{
    public int Position { get; }

    public ParseException(string message, int position)
        : base(message)
    {
        Position = position;
    }

    public ParseException(string message, int position, Exception innerException)
        : base(message, innerException)
    {
        Position = position;
    }
}
