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
            var monitorsVm = new MonitorsViewModel(service);
            var xkbService = new XkbService(cli);
            var inputVm = new InputViewModel(service, xkbService);
            var audioService = new AudioService(cli);
            var audioVm = new AudioViewModel(audioService);
            var powerService = new PowerService(cli);
            var idleService = new IdleService(cli);
            var idleVm = new IdleViewModel(idleService);
            var powerVm = new PowerViewModel(powerService, idleVm);
            var bluetoothService = new BluetoothService(cli);
            var bluetoothVm = new BluetoothViewModel(bluetoothService);
            var networkService = new NetworkService(cli);
            var networkVm = new NetworkViewModel(networkService);
            var wallpaperService = new WallpaperService(cli);
            var wallpaperVm = new WallpaperViewModel(wallpaperService);
            var backupVm = new BackupViewModel(service);
            var mainVm = new MainWindowViewModel(variablesVm, startupVm, keybindingsVm, monitorsVm, inputVm, audioVm, powerVm, bluetoothVm, networkVm, wallpaperVm, backupVm);

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
                    monitorsVm.Refresh();
                    _ = inputVm.InitializeAsync();
                    inputVm.Refresh();
                    _ = audioVm.InitializeAsync();
                    _ = powerVm.InitializeAsync();
                    _ = idleVm.InitializeAsync();
                    _ = bluetoothVm.InitializeAsync();
                    _ = networkVm.InitializeAsync();
                    _ = wallpaperVm.InitializeAsync();
                    backupVm.Refresh();
                },
                TaskScheduler.FromCurrentSynchronizationContext());
        }

        base.OnFrameworkInitializationCompleted();
    }
}
