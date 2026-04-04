using System.Windows.Input;

namespace Hypricing.Desktop.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    private ViewModelBase _currentPage;

    public MainWindowViewModel(
        VariablesViewModel variablesPage,
        StartupViewModel startupPage,
        KeybindingsViewModel keybindingsPage,
        BackupViewModel backupPage)
    {
        BackupPage = backupPage;

        Pages =
        [
            new PageItem("Variables", variablesPage),
            new PageItem("Keybindings", keybindingsPage),
            new PageItem("Startup", startupPage),
            new PageItem("Display", new PlaceholderViewModel("Display")),
            new PageItem("Audio", new PlaceholderViewModel("Audio")),
            new PageItem("Power", new PlaceholderViewModel("Power")),
            new PageItem("Bluetooth", new PlaceholderViewModel("Bluetooth")),
        ];

        _currentPage = variablesPage;
        OpenBackupsCommand = new RelayCommand(() =>
        {
            SelectedPage = null;
            CurrentPage = backupPage;
            backupPage.Refresh();
        });
    }

    public IReadOnlyList<PageItem> Pages { get; }
    public BackupViewModel BackupPage { get; }
    public ICommand OpenBackupsCommand { get; }

    public ViewModelBase CurrentPage
    {
        get => _currentPage;
        set
        {
            if (_currentPage == value) return;
            _currentPage = value;
            OnPropertyChanged();
        }
    }

    private PageItem? _selectedPage;

    public PageItem? SelectedPage
    {
        get => _selectedPage;
        set
        {
            if (_selectedPage == value) return;
            _selectedPage = value;
            OnPropertyChanged();
            if (value?.ViewModel is not null)
                CurrentPage = value.ViewModel;
        }
    }
}

public sealed class PageItem
{
    public PageItem(string name, ViewModelBase viewModel)
    {
        Name = name;
        ViewModel = viewModel;
    }

    public string Name { get; }
    public ViewModelBase ViewModel { get; }
}
