namespace Hypricing.HyprlangParser.Nodes;

/// <summary>
/// Keyword with comma-separated parameters: <c>keyword = param1,param2,...</c>.
/// Distinguished from <see cref="AssignmentNode"/> by the presence of top-level commas in the value.
/// </summary>
public sealed class KeywordNode : SyntaxNode
{
    private string _keyword;
    private string _params;
    private string? _inlineComment;

    public KeywordNode(string keyword, string @params, string? inlineComment = null)
    {
        _keyword = keyword;
        _params = @params;
        _inlineComment = inlineComment;
        IsDirty = true;
    }

    public string Keyword { get => _keyword; set { _keyword = value; IsDirty = true; } }
    public string Params { get => _params; set { _params = value; IsDirty = true; } }
    public string? InlineComment { get => _inlineComment; set { _inlineComment = value; IsDirty = true; } }
}
