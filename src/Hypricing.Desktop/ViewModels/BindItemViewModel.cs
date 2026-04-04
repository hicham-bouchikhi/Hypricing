using System.Windows.Input;
using Hypricing.HyprlangParser.Nodes;

namespace Hypricing.Desktop.ViewModels;

/// <summary>
/// Wraps a bind <see cref="KeywordNode"/> for data binding.
/// Params format: MODS,KEY,DISPATCHER[,ARGS]
/// </summary>
public sealed class BindItemViewModel : ViewModelBase
{
    private readonly KeywordNode _node;

    public BindItemViewModel(KeywordNode node, Action<BindItemViewModel>? onRemove = null)
    {
        _node = node;
        RemoveCommand = new RelayCommand(() => onRemove?.Invoke(this));
    }

    internal KeywordNode Node => _node;
    public ICommand RemoveCommand { get; }

    public string Variant
    {
        get => _node.Keyword;
        set
        {
            if (_node.Keyword == value) return;
            _node.Keyword = value;
            OnPropertyChanged();
        }
    }

    public string Modifiers
    {
        get => GetPart(0);
        set => SetPart(0, value);
    }

    public string Key
    {
        get => GetPart(1);
        set => SetPart(1, value);
    }

    public string Dispatcher
    {
        get => GetPart(2);
        set => SetPart(2, value);
    }

    public string Args
    {
        get
        {
            var parts = _node.Params.Split(',', 4);
            return parts.Length > 3 ? parts[3] : string.Empty;
        }
        set
        {
            var parts = _node.Params.Split(',', 4);
            while (parts.Length < 4)
                parts = [.. parts, ""];
            parts[3] = value;
            SyncParams(parts);
            OnPropertyChanged();
        }
    }

    public static IReadOnlyList<string> AllVariants { get; } =
    [
        "bind",
        "binde",
        "bindm",
        "bindl",
        "bindr",
        "bindn",
    ];

    private string GetPart(int index)
    {
        var parts = _node.Params.Split(',', 4);
        return index < parts.Length ? parts[index] : string.Empty;
    }

    private void SetPart(int index, string value)
    {
        var parts = _node.Params.Split(',', 4);
        while (parts.Length <= index)
            parts = [.. parts, ""];
        parts[index] = value;
        SyncParams(parts);
        OnPropertyChanged();
    }

    private void SyncParams(string[] parts)
    {
        // Trim trailing empty args
        int last = parts.Length - 1;
        while (last > 2 && string.IsNullOrEmpty(parts[last]))
            last--;
        _node.Params = string.Join(',', parts[..(last + 1)]);
    }
}
