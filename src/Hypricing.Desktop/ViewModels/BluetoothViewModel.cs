using System.Collections.ObjectModel;
using System.Windows.Input;
using Hypricing.Core.Services;

namespace Hypricing.Desktop.ViewModels;

public sealed class BluetoothViewModel : ViewModelBase
{
    private readonly BluetoothService _service;

    public BluetoothViewModel(BluetoothService service)
    {
        _service = service;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        ScanCommand = new AsyncRelayCommand(ScanAsync);
    }

    public ObservableCollection<BluetoothDeviceViewModel> Devices { get; } = [];

    public ICommand RefreshCommand { get; }
    public ICommand ScanCommand { get; }

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

    public async Task InitializeAsync()
    {
        StatusMessage = "Loading…";
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        try
        {
            var devices = await _service.GetDevicesAsync();
            Devices.Clear();
            foreach (var d in devices)
                Devices.Add(new BluetoothDeviceViewModel(d, _service, this));

            StatusMessage = Devices.Count == 0 ? "No paired devices found" : $"{Devices.Count} device(s)";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Refresh failed: {ex.Message}";
        }
    }

    private async Task ScanAsync()
    {
        IsScanning = true;
        StatusMessage = "Scanning for devices…";
        try
        {
            await _service.ScanAsync();
        }
        finally
        {
            IsScanning = false;
        }
        await RefreshAsync();
    }

    internal Task OnChanged() => RefreshAsync();
}

public sealed class BluetoothDeviceViewModel : ViewModelBase
{
    private readonly BluetoothDevice _device;
    private readonly BluetoothService _service;
    private readonly BluetoothViewModel _parent;

    public BluetoothDeviceViewModel(BluetoothDevice device, BluetoothService service, BluetoothViewModel parent)
    {
        _device = device;
        _service = service;
        _parent = parent;
        ConnectCommand = new AsyncRelayCommand(ToggleConnectAsync);
        RemoveCommand = new AsyncRelayCommand(RemoveAsync);
    }

    public string Address => _device.Address;
    public string Name => _device.Name;
    public string Icon => _device.Icon;
    public bool Connected => _device.Connected;
    public bool Trusted => _device.Trusted;
    public bool HasBattery => _device.BatteryPercent.HasValue;
    public string BatteryText => _device.BatteryPercent.HasValue ? $"{_device.BatteryPercent}%" : string.Empty;
    public string ConnectLabel => _device.Connected ? "Disconnect" : "Connect";

    public ICommand ConnectCommand { get; }
    public ICommand RemoveCommand { get; }

    private async Task ToggleConnectAsync()
    {
        try
        {
            if (_device.Connected)
                await _service.DisconnectAsync(_device.Address);
            else
                await _service.ConnectAsync(_device.Address);
        }
        catch { /* ignore transient errors */ }
        await _parent.OnChanged();
    }

    private async Task RemoveAsync()
    {
        try
        {
            await _service.RemoveAsync(_device.Address);
        }
        catch { /* ignore transient errors */ }
        await _parent.OnChanged();
    }
}
