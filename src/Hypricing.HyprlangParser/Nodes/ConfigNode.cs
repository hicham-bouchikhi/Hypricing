namespace Hypricing.HyprlangParser.Nodes;

/// <summary>
/// Root document node returned by <see cref="HyprlangParser.Parse"/>.
/// Contains the ordered list of top-level AST nodes.
/// </summary>
public sealed class ConfigNode
{
    /// <summary>
    /// The original input text. Must remain alive for the lifetime of the AST
    /// because unmodified nodes store Range references into this buffer.
    /// </summary>
    internal string OriginalText { get; }

    /// <summary>
    /// Top-level nodes in document order.
    /// </summary>
    public List<SyntaxNode> Children { get; } = [];

    internal ConfigNode(string originalText) => OriginalText = originalText;
}
