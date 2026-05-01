using System.Windows.Input;
using Hypricing.Core.Services;

namespace Hypricing.Desktop.ViewModels;

public sealed class PowerViewModel : ViewModelBase
{
    private readonly PowerService _service;
    private string[] _profiles = [];
    private string? _activeProfile;
    private BatteryInfo? _battery;

    public PowerViewModel(PowerService service, IdleViewModel idle)
    {
        _service = service;
        Idle = idle;
        RefreshCommand = new AsyncRelayCommand(() => RefreshAsync());
    }

    public ICommand RefreshCommand { get; }
    public IdleViewModel Idle { get; }

    public string[] Profiles
    {
        get => _profiles;
        private set { _profiles = value; OnPropertyChanged(); }
    }

    public string? ActiveProfile
    {
        get => _activeProfile;
        set
        {
            if (_activeProfile == value) return;
            _activeProfile = value;
            OnPropertyChanged();
            if (value is not null)
                _ = ApplyProfileAsync(value);
        }
    }

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

    public bool HasBattery => _battery?.Present == true;

    public double BatteryPercentage => _battery?.Percentage ?? 0;

    public string BatteryState => _battery?.State ?? string.Empty;

    public string? BatteryTimeEstimate => _battery?.TimeEstimate;

    public string BatteryLabel => _battery is null
        ? "No battery detected"
        : $"{(int)_battery.Percentage}% — {_battery.State}";

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await RefreshAsync(ct);
    }

    private async Task RefreshAsync(CancellationToken ct = default)
    {
        try
        {
            var (profiles, active) = await _service.GetProfilesAsync(ct);
            Profiles = profiles;

            // Set backing field to avoid triggering ApplyProfileAsync on load
            _activeProfile = active;
            OnPropertyChanged(nameof(ActiveProfile));

            _battery = await _service.GetBatteryInfoAsync(ct);
            OnPropertyChanged(nameof(HasBattery));
            OnPropertyChanged(nameof(BatteryPercentage));
            OnPropertyChanged(nameof(BatteryState));
            OnPropertyChanged(nameof(BatteryTimeEstimate));
            OnPropertyChanged(nameof(BatteryLabel));

            StatusMessage = string.Empty;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Refresh failed: {ex.Message}";
        }
    }

    private async Task ApplyProfileAsync(string profile)
    {
        try
        {
            await _service.SetProfileAsync(profile);
            StatusMessage = $"Profile set to {profile}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed: {ex.Message}";
        }
    }
}
