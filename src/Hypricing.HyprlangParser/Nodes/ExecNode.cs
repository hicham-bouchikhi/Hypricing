namespace Hypricing.HyprlangParser.Nodes;

/// <summary>
/// Identifies the specific exec directive variant.
/// Each value maps 1:1 to its source keyword for round-trip fidelity.
/// </summary>
public enum ExecVariant
{
    /// <summary>exec-once</summary>
    Once,

    /// <summary>execr-once</summary>
    OnceRestart,

    /// <summary>exec</summary>
    Reload,

    /// <summary>execr</summary>
    ExecrReload,

    /// <summary>exec-shutdown</summary>
    Shutdown
}

/// <summary>
/// Exec directive: <c>exec-once = [rules] command</c> and variants.
/// </summary>
public sealed class ExecNode : SyntaxNode
{
    private ExecVariant _variant;
    private string? _rules;
    private string _command;
    private string? _inlineComment;

    public ExecNode(ExecVariant variant, string command, string? rules = null, string? inlineComment = null)
    {
        _variant = variant;
        _command = command;
        _rules = rules;
        _inlineComment = inlineComment;
        IsDirty = true;
    }

    public ExecVariant Variant { get => _variant; set { _variant = value; IsDirty = true; } }
    public string? Rules { get => _rules; set { _rules = value; IsDirty = true; } }
    public string Command { get => _command; set { _command = value; IsDirty = true; } }
    public string? InlineComment { get => _inlineComment; set { _inlineComment = value; IsDirty = true; } }

    internal static bool TryParseVariant(string keyword, out ExecVariant variant)
    {
        switch (keyword)
        {
            case "exec-once": variant = ExecVariant.Once; return true;
            case "exec": variant = ExecVariant.Reload; return true;
            case "exec-shutdown": variant = ExecVariant.Shutdown; return true;
            case "execr-once": variant = ExecVariant.OnceRestart; return true;
            case "execr": variant = ExecVariant.ExecrReload; return true;
            default: variant = default; return false;
        }
    }

    internal static string VariantToKeyword(ExecVariant variant) => variant switch
    {
        ExecVariant.Once => "exec-once",
        ExecVariant.OnceRestart => "execr-once",
        ExecVariant.Reload => "exec",
        ExecVariant.ExecrReload => "execr",
        ExecVariant.Shutdown => "exec-shutdown",
        _ => throw new ArgumentOutOfRangeException(nameof(variant)),
    };
}
