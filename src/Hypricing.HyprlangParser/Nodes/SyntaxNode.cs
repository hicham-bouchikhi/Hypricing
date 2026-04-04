namespace Hypricing.HyprlangParser.Nodes;

/// <summary>
/// Abstract base for all AST node types.
/// </summary>
public abstract class SyntaxNode
{
    /// <summary>
    /// Range in the original input text covering this node's complete content
    /// (including leading whitespace and trailing newline).
    /// Used by the writer to copy original text verbatim for unmodified nodes.
    /// </summary>
    internal Range OriginalSpan { get; set; }

    /// <summary>
    /// Whether this node has been modified since parsing, or was created programmatically.
    /// The writer regenerates text from fields for dirty nodes and copies original text for clean nodes.
    /// </summary>
    public bool IsDirty { get; internal set; }
}
