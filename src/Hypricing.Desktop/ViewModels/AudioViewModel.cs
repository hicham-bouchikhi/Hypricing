using System.Collections.ObjectModel;
using System.Windows.Input;
using Hypricing.Core.Contracts;
using Hypricing.Core.Services;

namespace Hypricing.Desktop.ViewModels;

public sealed class AudioViewModel : ViewModelBase
{
    private readonly AudioService _audioService;
    private string? _statusMessage;
    private SinkViewModel? _selectedSink;

    public AudioViewModel(AudioService audioService)
    {
        _audioService = audioService;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
    }

    public ObservableCollection<SinkViewModel> Sinks { get; } = [];
    public ObservableCollection<SinkViewModel> Sources { get; } = [];
    public ObservableCollection<StreamViewModel> Streams { get; } = [];

    public ICommand RefreshCommand { get; }

    public string? StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (_statusMessage == value) return;
            _statusMessage = value;
            OnPropertyChanged();
        }
    }

    public SinkViewModel? SelectedSink
    {
        get => _selectedSink;
        set
        {
            if (_selectedSink == value) return;
            _selectedSink = value;
            OnPropertyChanged();
        }
    }

    public async Task InitializeAsync()
    {
        await _audioService.InitializeAsync();

        if (_audioService.Error is not null)
        {
            StatusMessage = _audioService.Error;
            return;
        }

        StatusMessage = $"Using {_audioService.ActivePresetName}";
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        var backend = _audioService.Backend;
        if (backend is null)
        {
            StatusMessage = "No audio backend available";
            return;
        }

        try
        {
            var sinks = await backend.ListSinksAsync();
            var sources = await backend.ListSourcesAsync();
            var streams = await backend.ListStreamsAsync();

            Sinks.Clear();
            foreach (var s in sinks)
                Sinks.Add(new SinkViewModel(s, backend, this));

            Sources.Clear();
            foreach (var s in sources)
                Sources.Add(new SinkViewModel(s, backend, this));

            Streams.Clear();
            foreach (var s in streams)
                Streams.Add(new StreamViewModel(s, backend, Sinks, this));
        }
        catch (Exception ex)
        {
            StatusMessage = $"Refresh failed: {ex.Message}";
        }
    }

    internal Task OnChanged() => RefreshAsync();
}

public sealed class SinkViewModel : ViewModelBase
{
    private readonly AudioDevice _device;
    private readonly IAudioBackend _backend;
    private readonly AudioViewModel _parent;
    private double _volume;

    public SinkViewModel(AudioDevice device, IAudioBackend backend, AudioViewModel parent)
    {
        _device = device;
        _backend = backend;
        _parent = parent;
        _volume = device.Volume;
        ToggleMuteCommand = new AsyncRelayCommand(ToggleMuteAsync);
        SetDefaultCommand = new AsyncRelayCommand(SetDefaultAsync);
    }

    public int Id => _device.Id;
    public string Name => _device.Name;
    public string Description => _device.Description;
    public bool Muted => _device.Muted;
    public string MuteLabel => _device.Muted ? "Unmute" : "Mute";
    public bool IsDefault => _device.IsDefault;
    public int VolumePercent => (int)Math.Round(_volume * 100);

    public double Volume
    {
        get => _volume;
        set
        {
            var snapped = value is > 0.98 and < 1.03 ? 1.0 : value;

            if (Math.Abs(_volume - snapped) < 0.005) return;
            _volume = snapped;
            OnPropertyChanged();
            OnPropertyChanged(nameof(VolumePercent));
            _ = SetVolumeAsync(snapped);

            // Force slider to jump to snapped position on next frame
            if (Math.Abs(snapped - value) > 0.005)
                Avalonia.Threading.Dispatcher.UIThread.Post(() => OnPropertyChanged(nameof(Volume)));
        }
    }

    public ICommand ToggleMuteCommand { get; }
    public ICommand SetDefaultCommand { get; }

    private async Task SetVolumeAsync(double vol)
    {
        await _backend.SetVolumeAsync(_device.Id, vol);
    }

    private async Task ToggleMuteAsync()
    {
        await _backend.ToggleMuteAsync(_device.Id);
        await _parent.OnChanged();
    }

    private async Task SetDefaultAsync()
    {
        await _backend.SetDefaultSinkAsync(_device.Id, _device.Name);
        await _parent.OnChanged();
    }
}

public sealed class StreamViewModel : ViewModelBase
{
    private readonly AudioStream _stream;
    private readonly IAudioBackend _backend;
    private readonly AudioViewModel _parent;
    private double _volume;
    private SinkViewModel? _targetSink;

    public StreamViewModel(
        AudioStream stream,
        IAudioBackend backend,
        ObservableCollection<SinkViewModel> sinks,
        AudioViewModel parent)
    {
        _stream = stream;
        _backend = backend;
        _parent = parent;
        _volume = stream.Volume;
        AvailableSinks = sinks;
        _targetSink = sinks.FirstOrDefault(s => s.Id == stream.SinkId);
    }

    public int Id => _stream.Id;
    public string AppName => _stream.AppName;
    public int SinkId => _stream.SinkId;
    public bool Muted => _stream.Muted;
    public int VolumePercent => (int)Math.Round(_volume * 100);
    public ObservableCollection<SinkViewModel> AvailableSinks { get; }

    public double Volume
    {
        get => _volume;
        set
        {
            var snapped = value is > 0.98 and < 1.03 ? 1.0 : value;

            if (Math.Abs(_volume - snapped) < 0.005) return;
            _volume = snapped;
            OnPropertyChanged();
            OnPropertyChanged(nameof(VolumePercent));
            _ = _backend.SetStreamVolumeAsync(_stream.Id, snapped);

            if (Math.Abs(snapped - value) > 0.005)
                Avalonia.Threading.Dispatcher.UIThread.Post(() => OnPropertyChanged(nameof(Volume)));
        }
    }

    public SinkViewModel? TargetSink
    {
        get => _targetSink;
        set
        {
            if (_targetSink == value || value is null) return;
            _targetSink = value;
            OnPropertyChanged();
            _ = MoveToSinkAsync(value.Id);
        }
    }

    private async Task MoveToSinkAsync(int sinkId)
    {
        await _backend.MoveStreamAsync(_stream.Id, sinkId);
        await _parent.OnChanged();
    }
}
