using System.Collections.ObjectModel;
using System.Windows.Input;
using Hypricing.Core.Services;
using Hypricing.HyprlangParser.Nodes;

namespace Hypricing.Desktop.ViewModels;

public sealed class StartupViewModel : ViewModelBase
{
    private readonly HyprlandService _service;

    public StartupViewModel(HyprlandService service)
    {
        _service = service;
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        AddCommand = new RelayCommand(AddEntry);
    }

    public ObservableCollection<ExecItemViewModel> ExecEntries { get; } = [];

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

    public string NewCommand
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            OnPropertyChanged();
        }
    } = string.Empty;

    public ExecVariant NewVariant
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            OnPropertyChanged();
        }
    } = ExecVariant.Once;

    public void Refresh()
    {
        ExecEntries.Clear();
        foreach (var exec in _service.GetExecEntries())
            ExecEntries.Add(new ExecItemViewModel(exec, RemoveEntry));
    }

    private void RemoveEntry(ExecItemViewModel item)
    {
        _service.RemoveExecEntry(item.Node);
        ExecEntries.Remove(item);
    }

    private void AddEntry()
    {
        if (string.IsNullOrWhiteSpace(NewCommand))
            return;

        _service.AddExecEntry(NewVariant, NewCommand.Trim());
        NewCommand = string.Empty;
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
