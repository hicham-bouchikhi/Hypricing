namespace Hypricing.HyprlangParser.Nodes;

/// <summary>
/// Blank line (whitespace-only or bare newline). Preserved for formatting fidelity.
/// </summary>
public sealed class EmptyLineNode : SyntaxNode
{
    public EmptyLineNode()
    {
        IsDirty = true;
    }
}
