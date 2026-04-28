using System.Windows.Input;
using Hypricing.HyprlangParser.Nodes;

namespace Hypricing.Desktop.ViewModels;

/// <summary>
/// Wraps an <see cref="ExecNode"/> for data binding.
/// </summary>
public sealed class ExecItemViewModel : ViewModelBase
{
    private readonly Action<ExecItemViewModel>? _onRemove;

    public ExecItemViewModel(ExecNode node, Action<ExecItemViewModel>? onRemove = null)
    {
        Node = node;
        _onRemove = onRemove;
        RemoveCommand = new RelayCommand(() => _onRemove?.Invoke(this));
    }

    internal ExecNode Node { get; }

    public ICommand RemoveCommand { get; }

    public ExecVariant Variant
    {
        get => Node.Variant;
        set
        {
            if (Node.Variant == value) return;
            Node.Variant = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(VariantDisplay));
        }
    }

    public string Command
    {
        get => Node.Command;
        set
        {
            if (Node.Command == value) return;
            Node.Command = value;
            OnPropertyChanged();
        }
    }

    public string? Rules
    {
        get => Node.Rules;
        set
        {
            if (Node.Rules == value) return;
            Node.Rules = value;
            OnPropertyChanged();
        }
    }

    public string VariantDisplay => ExecNode.VariantToKeyword(Node.Variant);

    public static IReadOnlyList<ExecVariant> AllVariants { get; } =
    [
        ExecVariant.Once,
        ExecVariant.Reload,
        ExecVariant.OnceRestart,
        ExecVariant.ExecrReload,
        ExecVariant.Shutdown,
    ];
}
