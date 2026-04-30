using System.Windows.Input;

namespace Hypricing.Desktop.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    private ViewModelBase _currentPage;

    public MainWindowViewModel(
        VariablesViewModel variablesPage,
        StartupViewModel startupPage,
        KeybindingsViewModel keybindingsPage,
        MonitorsViewModel monitorsPage,
        InputViewModel inputPage,
        AudioViewModel audioPage,
        PowerViewModel powerPage,
        BackupViewModel backupPage)
    {
        BackupPage = backupPage;

        Pages =
        [
            new PageItem("Variables",   variablesPage),
            new PageItem("Keybindings", keybindingsPage),
            new PageItem("Display",     monitorsPage),
            new PageItem("Input",       inputPage),
            new PageItem("Startup",     startupPage),
            new PageItem("Audio",       audioPage),
            new PageItem("Power",       powerPage),
            new PageItem("Bluetooth",   new PlaceholderViewModel("Bluetooth")),
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

    public PageItem? SelectedPage
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            OnPropertyChanged();
            if (value?.ViewModel is not null)
                CurrentPage = value.ViewModel;
        }
    }
}

public sealed class PageItem(string name, ViewModelBase viewModel)
{
    public string Name { get; } = name;
    public ViewModelBase ViewModel { get; } = viewModel;
}
