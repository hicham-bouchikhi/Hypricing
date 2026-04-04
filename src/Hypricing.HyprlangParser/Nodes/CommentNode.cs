namespace Hypricing.HyprlangParser.Nodes;

/// <summary>
/// Full-line comment: <c># comment text</c>.
/// The <see cref="Text"/> includes the leading <c>#</c>.
/// </summary>
public sealed class CommentNode : SyntaxNode
{
    public CommentNode(string text)
    {
        Text = text;
        IsDirty = true;
    }

    /// <summary>
    /// The comment text including the <c>#</c> prefix (e.g. <c>"# this is a comment"</c>).
    /// </summary>
    public string Text { get; }
}
