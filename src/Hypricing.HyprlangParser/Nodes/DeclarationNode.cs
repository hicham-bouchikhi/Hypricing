namespace Hypricing.HyprlangParser.Nodes;

/// <summary>
/// Variable declaration: <c>$name = value</c>.
/// Only valid at the top level.
/// </summary>
public sealed class DeclarationNode : SyntaxNode
{
    private string _name;
    private string _value;
    private string? _inlineComment;

    public DeclarationNode(string name, string value, string? inlineComment = null)
    {
        _name = name;
        _value = value;
        _inlineComment = inlineComment;
        IsDirty = true;
    }

    public string Name { get => _name; set { _name = value; IsDirty = true; } }
    public string Value { get => _value; set { _value = value; IsDirty = true; } }
    public string? InlineComment { get => _inlineComment; set { _inlineComment = value; IsDirty = true; } }
}
