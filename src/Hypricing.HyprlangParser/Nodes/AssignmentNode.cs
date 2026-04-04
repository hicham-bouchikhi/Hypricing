namespace Hypricing.HyprlangParser.Nodes;

/// <summary>
/// Key-value assignment: <c>key = value</c>.
/// Distinguished from <see cref="KeywordNode"/> by the absence of top-level commas in the value.
/// </summary>
public sealed class AssignmentNode : SyntaxNode
{
    private string _key;
    private string _value;
    private string? _inlineComment;

    public AssignmentNode(string key, string value, string? inlineComment = null)
    {
        _key = key;
        _value = value;
        _inlineComment = inlineComment;
        IsDirty = true;
    }

    public string Key { get => _key; set { _key = value; IsDirty = true; } }
    public string Value { get => _value; set { _value = value; IsDirty = true; } }
    public string? InlineComment { get => _inlineComment; set { _inlineComment = value; IsDirty = true; } }
}
