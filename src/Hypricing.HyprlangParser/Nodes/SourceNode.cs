namespace Hypricing.HyprlangParser.Nodes;

/// <summary>
/// Source directive: <c>source = path</c>.
/// Only valid at the top level.
/// </summary>
public sealed class SourceNode : SyntaxNode
{
    private string _path;
    private string? _inlineComment;

    public SourceNode(string path, string? inlineComment = null)
    {
        _path = path;
        _inlineComment = inlineComment;
        IsDirty = true;
    }

    public string Path { get => _path; set { _path = value; IsDirty = true; } }
    public string? InlineComment { get => _inlineComment; set { _inlineComment = value; IsDirty = true; } }
}
