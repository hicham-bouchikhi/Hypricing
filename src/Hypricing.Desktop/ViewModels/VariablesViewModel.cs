using System.Collections.ObjectModel;
using System.Windows.Input;
using Hypricing.Core.Services;

namespace Hypricing.Desktop.ViewModels;

public sealed class VariablesViewModel : ViewModelBase
{
    private readonly HyprlandService _service;

    public VariablesViewModel(HyprlandService service)
    {
        _service = service;
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        ReloadCommand = new AsyncRelayCommand(LoadAsync);
        AddDeclarationCommand = new RelayCommand(AddDeclaration);
        AddEnvCommand = new RelayCommand(AddEnv);
    }

    public ObservableCollection<DeclarationItemViewModel> Declarations { get; } = [];
    public ObservableCollection<EnvItemViewModel> EnvironmentVariables { get; } = [];

    public ICommand SaveCommand { get; }
    public ICommand ReloadCommand { get; }
    public ICommand AddDeclarationCommand { get; }
    public ICommand AddEnvCommand { get; }

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

    public string NewDeclName
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

    public string NewDeclValue
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

    public string NewEnvKey
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

    public string NewEnvValue
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

    public async Task LoadAsync()
    {
        try
        {
            await _service.LoadAsync();
            Refresh();
            StatusMessage = $"Loaded {Declarations.Count} declaration(s), {EnvironmentVariables.Count} env var(s)";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Load failed: {ex.Message}";
        }
    }

    private void RemoveDeclaration(DeclarationItemViewModel item)
    {
        _service.RemoveDeclaration(item.Node);
        Declarations.Remove(item);
    }

    private void RemoveEnv(EnvItemViewModel item)
    {
        _service.RemoveEnvironmentVariable(item.Node);
        EnvironmentVariables.Remove(item);
    }

    private void AddDeclaration()
    {
        if (string.IsNullOrWhiteSpace(NewDeclName)) return;
        _service.AddDeclaration(NewDeclName.Trim(), NewDeclValue.Trim());
        NewDeclName = string.Empty;
        NewDeclValue = string.Empty;
        Refresh();
    }

    private void AddEnv()
    {
        if (string.IsNullOrWhiteSpace(NewEnvKey)) return;
        _service.AddEnvironmentVariable(NewEnvKey.Trim(), NewEnvValue.Trim());
        NewEnvKey = string.Empty;
        NewEnvValue = string.Empty;
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

    private void Refresh()
    {
        Declarations.Clear();
        foreach (var decl in _service.GetDeclarations())
            Declarations.Add(new DeclarationItemViewModel(decl, RemoveDeclaration));

        EnvironmentVariables.Clear();
        foreach (var env in _service.GetEnvironmentVariables())
            EnvironmentVariables.Add(new EnvItemViewModel(env, RemoveEnv));
    }
}
