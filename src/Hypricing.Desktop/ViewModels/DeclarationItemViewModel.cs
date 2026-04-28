using System.Windows.Input;
using Hypricing.HyprlangParser.Nodes;

namespace Hypricing.Desktop.ViewModels;

/// <summary>
/// Wraps a single <see cref="DeclarationNode"/> for data binding.
/// Writes back to the underlying AST node on change.
/// </summary>
public sealed class DeclarationItemViewModel : ViewModelBase
{
    public DeclarationItemViewModel(DeclarationNode node, Action<DeclarationItemViewModel>? onRemove = null)
    {
        Node = node;
        RemoveCommand = new RelayCommand(() => onRemove?.Invoke(this));
    }

    internal DeclarationNode Node { get; }

    public ICommand RemoveCommand { get; }

    public string Name
    {
        get => Node.Name;
        set
        {
            if (Node.Name == value) return;
            Node.Name = value;
            OnPropertyChanged();
        }
    }

    public string Value
    {
        get => Node.Value;
        set
        {
            if (Node.Value == value) return;
            Node.Value = value;
            OnPropertyChanged();
        }
    }
}
