namespace Hypricing.HyprlangParser.Nodes;

/// <summary>
/// Unrecognized content preserved verbatim. Guarantees no data loss —
/// anything the parser does not understand becomes a <see cref="RawNode"/>.
/// </summary>
public sealed class RawNode : SyntaxNode
{
    public RawNode(string text)
    {
        Text = text;
        IsDirty = true;
    }

    /// <summary>
    /// The raw line content (without trailing newline).
    /// </summary>
    public string Text { get; }
}
