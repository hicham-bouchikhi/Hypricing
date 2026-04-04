using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Hypricing.Core.Infrastructure;
using Hypricing.Core.Services;
using Hypricing.Desktop.ViewModels;
using Hypricing.Desktop.Views;

namespace Hypricing.Desktop;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var cli = new CliRunner();
            var service = new HyprlandService(cli);
            var variablesVm = new VariablesViewModel(service);
            var startupVm = new StartupViewModel(service);
            var keybindingsVm = new KeybindingsViewModel(service);
            var backupVm = new BackupViewModel(service);
            var mainVm = new MainWindowViewModel(variablesVm, startupVm, keybindingsVm, backupVm);

            desktop.MainWindow = new MainWindow
            {
                DataContext = mainVm,
            };

            // Select the first page
            mainVm.SelectedPage = mainVm.Pages[0];

            _ = variablesVm.LoadAsync().ContinueWith(_ =>
                {
                    startupVm.Refresh();
                    keybindingsVm.Refresh();
                    backupVm.Refresh();
                },
                TaskScheduler.FromCurrentSynchronizationContext());
        }

        base.OnFrameworkInitializationCompleted();
    }
}
