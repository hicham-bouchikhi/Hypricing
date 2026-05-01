using System.Collections.ObjectModel;
using System.Windows.Input;
using Hypricing.Core.Services;

namespace Hypricing.Desktop.ViewModels;

public sealed class NetworkViewModel : ViewModelBase
{
    private readonly NetworkService _service;

    public NetworkViewModel(NetworkService service)
    {
        _service = service;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        ScanCommand = new AsyncRelayCommand(ScanAsync);
        ToggleWifiCommand = new AsyncRelayCommand(ToggleWifiAsync);
    }

    public ObservableCollection<NetworkDeviceViewModel> Devices { get; } = [];
    public ObservableCollection<WifiNetworkViewModel> Networks { get; } = [];

    public ICommand RefreshCommand { get; }
    public ICommand ScanCommand { get; }
    public ICommand ToggleWifiCommand { get; }

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

    public bool WifiEnabled
    {
        get;
        private set
        {
            if (field == value) return;
            field = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(WifiToggleLabel));
        }
    }

    public bool IsScanning
    {
        get;
        private set
        {
            if (field == value) return;
            field = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ScanLabel));
        }
    }

    public string ScanLabel => IsScanning ? "Scanning…" : "Scan";
    public string WifiToggleLabel => WifiEnabled ? "WiFi Off" : "WiFi On";

    public async Task InitializeAsync()
    {
        StatusMessage = "Loading…";
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        try
        {
            WifiEnabled = await _service.GetWifiEnabledAsync();

            var devices = await _service.GetDevicesAsync();
            Devices.Clear();
            foreach (var d in devices)
                Devices.Add(new NetworkDeviceViewModel(d, _service, this));

            var networks = await _service.GetWifiNetworksAsync();
            Networks.Clear();
            foreach (var n in networks)
                Networks.Add(new WifiNetworkViewModel(n, _service, this));

            var connected = devices.Count(d => d.State == "connected");
            StatusMessage = connected > 0 ? $"{connected} interface(s) connected" : "No active connections";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Refresh failed: {ex.Message}";
        }
    }

    private async Task ScanAsync()
    {
        IsScanning = true;
        StatusMessage = "Scanning for networks…";
        try
        {
            await _service.ScanAsync();
            await Task.Delay(1500); // give NetworkManager a moment to collect results
        }
        finally
        {
            IsScanning = false;
        }
        await RefreshAsync();
    }

    private async Task ToggleWifiAsync()
    {
        try
        {
            await _service.SetWifiEnabledAsync(!WifiEnabled);
        }
        catch { }
        await RefreshAsync();
    }

    internal Task OnChanged() => RefreshAsync();
}

public sealed class NetworkDeviceViewModel : ViewModelBase
{
    private readonly NetworkDevice _device;
    private readonly NetworkService _service;
    private readonly NetworkViewModel _parent;

    public NetworkDeviceViewModel(NetworkDevice device, NetworkService service, NetworkViewModel parent)
    {
        _device = device;
        _service = service;
        _parent = parent;
        DisconnectCommand = new AsyncRelayCommand(DisconnectAsync);
    }

    public string Name => _device.Name;
    public string TypeLabel => _device.Type switch
    {
        "wifi"     => "WiFi",
        "ethernet" => "Ethernet",
        _          => _device.Type,
    };
    public string State => _device.State;
    public string? Connection => _device.Connection;
    public string? IpAddress => _device.IpAddress;
    public bool IsConnected => _device.State == "connected";
    public bool CanDisconnect => IsConnected;

    public ICommand DisconnectCommand { get; }

    private async Task DisconnectAsync()
    {
        try
        {
            await _service.DisconnectAsync(_device.Name);
        }
        catch { }
        await _parent.OnChanged();
    }
}

public sealed class WifiNetworkViewModel : ViewModelBase
{
    private readonly WifiNetwork _network;
    private readonly NetworkService _service;
    private readonly NetworkViewModel _parent;

    public WifiNetworkViewModel(WifiNetwork network, NetworkService service, NetworkViewModel parent)
    {
        _network = network;
        _service = service;
        _parent = parent;
        ConnectCommand = new AsyncRelayCommand(ConnectAsync);
    }

    public string Ssid => _network.Ssid;
    public int Signal => _network.Signal;
    public string Security => _network.Security == "--" ? "Open" : _network.Security;
    public bool Active => _network.Active;

    // 1-4 bars for signal strength display
    public int SignalBars => _network.Signal switch
    {
        >= 75 => 4,
        >= 50 => 3,
        >= 25 => 2,
        _     => 1,
    };

    // Per-bar opacity: bar N is full if SignalBars >= N, dimmed otherwise
    public double Bar1Opacity => SignalBars >= 1 ? 1.0 : 0.2;
    public double Bar2Opacity => SignalBars >= 2 ? 1.0 : 0.2;
    public double Bar3Opacity => SignalBars >= 3 ? 1.0 : 0.2;
    public double Bar4Opacity => SignalBars >= 4 ? 1.0 : 0.2;

    public ICommand ConnectCommand { get; }

    private async Task ConnectAsync()
    {
        try
        {
            await _service.ConnectAsync(_network.Ssid);
        }
        catch { }
        await _parent.OnChanged();
    }
}
