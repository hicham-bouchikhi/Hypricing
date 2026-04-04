using System.Text;
using Hypricing.HyprlangParser.Nodes;

namespace Hypricing.HyprlangParser;

/// <summary>
/// Serializes a <see cref="ConfigNode"/> AST back to Hyprlang text.
/// </summary>
public static class HyprlangWriter
{
    /// <summary>
    /// Writes the AST back to Hyprlang text.
    /// For unmodified nodes, the original text is copied verbatim (round-trip guarantee).
    /// For modified nodes, text is regenerated from the node's fields.
    /// </summary>
    public static string Write(ConfigNode config)
    {
        ArgumentNullException.ThrowIfNull(config);

        var sb = new StringBuilder(config.OriginalText.Length);
        foreach (var node in config.Children)
        {
            WriteNode(sb, node, config.OriginalText, indentLevel: 0);
        }
        return sb.ToString();
    }

    private static void WriteNode(StringBuilder sb, SyntaxNode node, string original, int indentLevel)
    {
        // Sections are always processed piece by piece (children may be independently dirty)
        if (node is SectionNode section)
        {
            WriteSection(sb, section, original, indentLevel);
            return;
        }

        // Non-section clean nodes: copy original text verbatim
        if (!node.IsDirty)
        {
            AppendSpan(sb, original, node.OriginalSpan);
            return;
        }

        // Dirty nodes: regenerate from fields
        switch (node)
        {
            case DeclarationNode decl:
                WriteIndent(sb, indentLevel);
                sb.Append('$');
                sb.Append(decl.Name);
                sb.Append(" = ");
                sb.Append(Lexer.EscapeValue(decl.Value));
                WriteInlineComment(sb, decl.InlineComment);
                sb.Append('\n');
                break;

            case AssignmentNode assign:
                WriteIndent(sb, indentLevel);
                sb.Append(assign.Key);
                sb.Append(" = ");
                sb.Append(Lexer.EscapeValue(assign.Value));
                WriteInlineComment(sb, assign.InlineComment);
                sb.Append('\n');
                break;

            case KeywordNode kw:
                WriteIndent(sb, indentLevel);
                sb.Append(kw.Keyword);
                sb.Append(" = ");
                sb.Append(Lexer.EscapeValue(kw.Params));
                WriteInlineComment(sb, kw.InlineComment);
                sb.Append('\n');
                break;

            case ExecNode exec:
                WriteIndent(sb, indentLevel);
                sb.Append(VariantToKeyword(exec.Variant));
                sb.Append(" = ");
                if (exec.Rules != null)
                {
                    sb.Append('[');
                    sb.Append(exec.Rules);
                    sb.Append("] ");
                }
                sb.Append(Lexer.EscapeValue(exec.Command));
                WriteInlineComment(sb, exec.InlineComment);
                sb.Append('\n');
                break;

            case SourceNode source:
                WriteIndent(sb, indentLevel);
                sb.Append("source = ");
                sb.Append(Lexer.EscapeValue(source.Path));
                WriteInlineComment(sb, source.InlineComment);
                sb.Append('\n');
                break;

            case CommentNode comment:
                WriteIndent(sb, indentLevel);
                sb.Append(comment.Text);
                sb.Append('\n');
                break;

            case EmptyLineNode:
                sb.Append('\n');
                break;

            case RawNode raw:
                sb.Append(raw.Text);
                sb.Append('\n');
                break;
        }
    }

    private static void WriteSection(StringBuilder sb, SectionNode section, string original, int indentLevel)
    {
        // Header
        if (!section.IsDirty)
        {
            AppendSpan(sb, original, section.HeaderSpan);
        }
        else
        {
            WriteIndent(sb, indentLevel);
            sb.Append(section.Name);
            if (section.Device != null)
            {
                sb.Append(':');
                sb.Append(section.Device);
            }
            sb.Append(" {");
            WriteInlineComment(sb, section.InlineComment);
            sb.Append('\n');
        }

        // Children
        foreach (var child in section.Children)
        {
            WriteNode(sb, child, original, indentLevel + 1);
        }

        // Closing brace
        if (!section.IsDirty)
        {
            AppendSpan(sb, original, section.ClosingSpan);
        }
        else
        {
            WriteIndent(sb, indentLevel);
            sb.Append("}\n");
        }
    }

    private static void WriteIndent(StringBuilder sb, int level)
    {
        for (int i = 0; i < level; i++)
            sb.Append("    ");
    }

    private static void WriteInlineComment(StringBuilder sb, string? comment)
    {
        if (comment != null)
        {
            sb.Append(' ');
            sb.Append(comment);
        }
    }

    private static void AppendSpan(StringBuilder sb, string source, Range range)
    {
        var (offset, length) = range.GetOffsetAndLength(source.Length);
        sb.Append(source.AsSpan(offset, length));
    }

    private static string VariantToKeyword(ExecVariant variant) =>
        ExecNode.VariantToKeyword(variant);
}
