using System.Windows.Input;
using Hypricing.HyprlangParser.Nodes;

namespace Hypricing.Desktop.ViewModels;

/// <summary>
/// Wraps an <c>env = KEY,VALUE</c> <see cref="KeywordNode"/> for data binding.
/// Splits params on the first comma into Name/Value and reconstructs on change.
/// </summary>
public sealed class EnvItemViewModel : ViewModelBase
{
    private string _name;
    private string _value;

    public EnvItemViewModel(KeywordNode node, Action<EnvItemViewModel>? onRemove = null)
    {
        Node = node;
        RemoveCommand = new RelayCommand(() => onRemove?.Invoke(this));
        var commaIndex = node.Params.IndexOf(',');
        if (commaIndex >= 0)
        {
            _name = node.Params[..commaIndex];
            _value = node.Params[(commaIndex + 1)..];
        }
        else
        {
            _name = node.Params;
            _value = string.Empty;
        }
    }

    internal KeywordNode Node { get; }

    public ICommand RemoveCommand { get; }

    public string Name
    {
        get => _name;
        set
        {
            if (_name == value) return;
            _name = value;
            SyncToNode();
            OnPropertyChanged();
        }
    }

    public string Value
    {
        get => _value;
        set
        {
            if (_value == value) return;
            _value = value;
            SyncToNode();
            OnPropertyChanged();
        }
    }

    private void SyncToNode() => Node.Params = $"{_name},{_value}";
}
