using System.Collections.ObjectModel;
using System.Windows.Input;
using Hypricing.Core.Contracts;
using Hypricing.Core.Services;

namespace Hypricing.Desktop.ViewModels;

public sealed class IdleViewModel : ViewModelBase
{
    private readonly IdleService _service;

    public IdleViewModel(IdleService service)
    {
        _service = service;
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        StartCommand = new AsyncRelayCommand(StartAsync);
        StopCommand = new AsyncRelayCommand(StopAsync);
        RestartCommand = new AsyncRelayCommand(RestartAsync);
        AddListenerCommand = new RelayCommand(AddListener);
    }

    public ObservableCollection<IdleListenerViewModel> Listeners { get; } = [];

    public ICommand SaveCommand { get; }
    public ICommand StartCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand RestartCommand { get; }
    public ICommand AddListenerCommand { get; }

    public bool IsAvailable
    {
        get;
        private set
        {
            if (field == value) return;
            field = value;
            OnPropertyChanged();
        }
    }

    public bool IsDaemonRunning
    {
        get;
        private set
        {
            if (field == value) return;
            field = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DaemonStatusText));
        }
    }

    public string DaemonStatusText => IsDaemonRunning ? "Running" : "Stopped";

    public string ActivePresetLabel => _service.ActivePresetName ?? "idle daemon";

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

    public string? LockCmd
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            OnPropertyChanged();
        }
    }

    public string? UnlockCmd
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            OnPropertyChanged();
        }
    }

    public string? BeforeSleepCmd
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            OnPropertyChanged();
        }
    }

    public string? AfterSleepCmd
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            OnPropertyChanged();
        }
    }

    public async Task InitializeAsync()
    {
        await _service.InitializeAsync();

        if (_service.Error is not null)
        {
            StatusMessage = _service.Error;
            IsAvailable = false;
            return;
        }

        IsAvailable = true;
        StatusMessage = $"Using {_service.ActivePresetName}";
        await LoadAsync();
        await RefreshDaemonStatusAsync();
    }

    private async Task LoadAsync()
    {
        var backend = _service.Backend;
        if (backend is null) return;

        try
        {
            var config = await backend.GetConfigAsync();
            LockCmd = config.General.LockCmd;
            UnlockCmd = config.General.UnlockCmd;
            BeforeSleepCmd = config.General.BeforeSleepCmd;
            AfterSleepCmd = config.General.AfterSleepCmd;

            Listeners.Clear();
            foreach (var l in config.Listeners)
                Listeners.Add(new IdleListenerViewModel(l, this));
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load config: {ex.Message}";
        }
    }

    private async Task SaveAsync()
    {
        var backend = _service.Backend;
        if (backend is null) return;

        try
        {
            var config = new IdleConfig(
                new IdleGeneral(LockCmd, UnlockCmd, BeforeSleepCmd, AfterSleepCmd),
                [.. Listeners.Select(l => new IdleListener(l.TimeoutSeconds, l.OnTimeout ?? string.Empty, l.OnResume))]);

            await backend.SaveAsync(config);
            IsDaemonRunning = await backend.IsDaemonRunningAsync();
            StatusMessage = "Saved and restarted";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Save failed: {ex.Message}";
        }
    }

    private async Task StartAsync()
    {
        var backend = _service.Backend;
        if (backend is null) return;
        try { await backend.StartDaemonAsync(); } catch { }
        await RefreshDaemonStatusAsync();
    }

    private async Task StopAsync()
    {
        var backend = _service.Backend;
        if (backend is null) return;
        try { await backend.StopDaemonAsync(); } catch { }
        await RefreshDaemonStatusAsync();
    }

    private async Task RestartAsync()
    {
        var backend = _service.Backend;
        if (backend is null) return;
        try { await backend.RestartDaemonAsync(); } catch { }
        await RefreshDaemonStatusAsync();
    }

    private async Task RefreshDaemonStatusAsync()
    {
        var backend = _service.Backend;
        IsDaemonRunning = backend is not null && await backend.IsDaemonRunningAsync();
    }

    private void AddListener()
    {
        Listeners.Add(new IdleListenerViewModel(new IdleListener(300, string.Empty, null), this));
    }

    internal void RemoveListener(IdleListenerViewModel listener) => Listeners.Remove(listener);
}

public sealed class IdleListenerViewModel : ViewModelBase
{
    private readonly IdleViewModel _parent;

    public IdleListenerViewModel(IdleListener listener, IdleViewModel parent)
    {
        _parent = parent;
        TimeoutSeconds = listener.Timeout;
        OnTimeout = listener.OnTimeout;
        OnResume = listener.OnResume;
        RemoveCommand = new RelayCommand(() => _parent.RemoveListener(this));
    }

    public ICommand RemoveCommand { get; }

    public int TimeoutSeconds
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            OnPropertyChanged();
        }
    }

    public string? OnTimeout
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            OnPropertyChanged();
        }
    }

    public string? OnResume
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            OnPropertyChanged();
        }
    }
}
