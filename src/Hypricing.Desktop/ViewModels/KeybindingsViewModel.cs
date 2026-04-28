using System.Collections.ObjectModel;
using System.Windows.Input;
using Hypricing.Core.Services;

namespace Hypricing.Desktop.ViewModels;

public sealed class KeybindingsViewModel : ViewModelBase
{
    private readonly HyprlandService _service;

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
        get;
        private set
        {
            if (field == value) return;
            field = value;
            OnPropertyChanged();
        }
    }

    public string NewVariant
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged();
            }
        }
    } = "bind";

    public string NewModifiers
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged();
            }
        }
    } = string.Empty;

    public string NewKey
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged();
            }
        }
    } = string.Empty;

    public string NewDispatcher
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged();
            }
        }
    } = string.Empty;

    public string NewArgs
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged();
            }
        }
    } = string.Empty;

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
