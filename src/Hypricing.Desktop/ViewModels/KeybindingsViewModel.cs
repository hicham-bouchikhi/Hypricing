using System.Collections.ObjectModel;
using System.Windows.Input;
using Hypricing.Core.Services;

namespace Hypricing.Desktop.ViewModels;

public sealed class KeybindingsViewModel : ViewModelBase
{
    private readonly HyprlandService _service;
    private string? _statusMessage;
    private string _newVariant = "bind";
    private string _newModifiers = string.Empty;
    private string _newKey = string.Empty;
    private string _newDispatcher = string.Empty;
    private string _newArgs = string.Empty;

    public KeybindingsViewModel(HyprlandService service)
    {
        _service = service;
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        AddCommand = new RelayCommand(AddKeybinding);
    }

    public ObservableCollection<BindItemViewModel> Keybindings { get; } = [];

    public ICommand SaveCommand { get; }
    public ICommand AddCommand { get; }

    public string? StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (_statusMessage == value) return;
            _statusMessage = value;
            OnPropertyChanged();
        }
    }

    public string NewVariant
    {
        get => _newVariant;
        set { if (_newVariant != value) { _newVariant = value; OnPropertyChanged(); } }
    }

    public string NewModifiers
    {
        get => _newModifiers;
        set { if (_newModifiers != value) { _newModifiers = value; OnPropertyChanged(); } }
    }

    public string NewKey
    {
        get => _newKey;
        set { if (_newKey != value) { _newKey = value; OnPropertyChanged(); } }
    }

    public string NewDispatcher
    {
        get => _newDispatcher;
        set { if (_newDispatcher != value) { _newDispatcher = value; OnPropertyChanged(); } }
    }

    public string NewArgs
    {
        get => _newArgs;
        set { if (_newArgs != value) { _newArgs = value; OnPropertyChanged(); } }
    }

    public void Refresh()
    {
        Keybindings.Clear();
        foreach (var node in _service.GetKeybindings())
            Keybindings.Add(new BindItemViewModel(node, RemoveKeybinding));
    }

    private void RemoveKeybinding(BindItemViewModel item)
    {
        _service.RemoveKeybinding(item.Node);
        Keybindings.Remove(item);
    }

    private void AddKeybinding()
    {
        if (string.IsNullOrWhiteSpace(NewDispatcher)) return;

        var @params = string.IsNullOrEmpty(NewArgs)
            ? $"{NewModifiers.Trim()},{NewKey.Trim()},{NewDispatcher.Trim()}"
            : $"{NewModifiers.Trim()},{NewKey.Trim()},{NewDispatcher.Trim()},{NewArgs.Trim()}";

        _service.AddKeybinding(NewVariant, @params);
        NewModifiers = string.Empty;
        NewKey = string.Empty;
        NewDispatcher = string.Empty;
        NewArgs = string.Empty;
        Refresh();
    }

    private async Task SaveAsync()
    {
        try
        {
            await _service.SaveAsync();
            Refresh();
            StatusMessage = "Saved and reloaded";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Save failed: {ex.Message}";
        }
    }
}
