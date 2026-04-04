namespace Hypricing.HyprlangParser.Nodes;

/// <summary>
/// Configuration section: <c>name { children }</c> or <c>name:device { children }</c>.
/// </summary>
public sealed class SectionNode : SyntaxNode
{
    private string _name;
    private string? _device;
    private string? _inlineComment;

    public SectionNode(string name, string? device = null, string? inlineComment = null)
    {
        _name = name;
        _device = device;
        _inlineComment = inlineComment;
        IsDirty = true;
    }

    public string Name { get => _name; set { _name = value; IsDirty = true; } }
    public string? Device { get => _device; set { _device = value; IsDirty = true; } }
    public List<SyntaxNode> Children { get; } = [];
    public string? InlineComment { get => _inlineComment; set { _inlineComment = value; IsDirty = true; } }

    /// <summary>Range covering the section header line (e.g. "general {\n").</summary>
    internal Range HeaderSpan { get; set; }

    /// <summary>Range covering the closing brace line (e.g. "}\n").</summary>
    internal Range ClosingSpan { get; set; }
}
