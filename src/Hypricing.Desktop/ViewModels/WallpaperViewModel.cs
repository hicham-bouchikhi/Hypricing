using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Hypricing.Core.Contracts;
using Hypricing.Core.Services;

namespace Hypricing.Desktop.ViewModels;

public sealed class WallpaperViewModel : ViewModelBase
{
    private static readonly string[] ImageExtensions = [".jpg", ".jpeg", ".png", ".gif", ".webp"];
    private readonly WallpaperService _service;

    public static IReadOnlyList<string> TransitionTypes { get; } =
        ["none", "simple", "fade", "left", "right", "top", "bottom", "wipe", "wave", "grow", "center", "any", "outer", "random"];

    public WallpaperViewModel(WallpaperService service)
    {
        _service = service;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        RandomAllCommand = new AsyncRelayCommand(RandomAllAsync);
    }

    private readonly HashSet<MonitorWallpaperViewModel> _selectedMonitors = [];

    public ObservableCollection<MonitorWallpaperViewModel> Monitors { get; } = [];
    public ObservableCollection<WallpaperImageViewModel> Images { get; } = [];

    public int SelectedMonitorCount
    {
        get;
        private set
        {
            if (field == value) return;
            field = value;
            OnPropertyChanged();
        }
    }

    public string? BrowsedFolder
    {
        get;
        private set
        {
            if (field == value) return;
            field = value;
            OnPropertyChanged();
        }
    }

    public bool SupportsTransitions
    {
        get;
        private set
        {
            if (field == value) return;
            field = value;
            OnPropertyChanged();
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

    public string? Error
    {
        get;
        private set
        {
            if (field == value) return;
            field = value;
            OnPropertyChanged();
        }
    }

    public string TransitionType
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            OnPropertyChanged();
        }
    } = "none";

    public string TransitionDuration
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            OnPropertyChanged();
        }
    } = "1.0";

    public string TransitionFps
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            OnPropertyChanged();
        }
    } = "30";

    public ICommand RefreshCommand { get; }
    public ICommand RandomAllCommand { get; }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await _service.InitializeAsync(ct);
        Error = _service.Error;
        if (_service.Backend is null) return;

        SupportsTransitions = _service.SupportsTransitions;
        await RefreshAsync();

        if (_service.SavedFolder is not null)
            SetFolder(_service.SavedFolder);
    }

    public void ToggleMonitor(MonitorWallpaperViewModel monitor)
    {
        if (_selectedMonitors.Contains(monitor) && _selectedMonitors.Count == 1)
            return;
        if (!_selectedMonitors.Remove(monitor))
            _selectedMonitors.Add(monitor);
        monitor.IsSelected = _selectedMonitors.Contains(monitor);
        SelectedMonitorCount = _selectedMonitors.Count;
    }

    public void SetFolder(string path)
    {
        BrowsedFolder = path;
        Images.Clear();

        List<string> files;
        try
        {
            files = Directory.EnumerateFiles(path)
                .Where(f => ImageExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .OrderBy(f => f)
                .ToList();
        }
        catch
        {
            StatusMessage = $"Could not read folder: {path}";
            return;
        }

        foreach (var file in files)
            Images.Add(new WallpaperImageViewModel(file, ApplyImageToSelected));

        _ = LoadThumbnailsAsync(Images.ToList());
    }

    public void SaveFolder(string path) => _service.SaveFolder(path);

    internal async Task ApplyWallpaperAsync(string monitor, string imagePath)
    {
        if (_service.Backend is null) return;
        try
        {
            if (_service.SupportsTransitions && _service.Backend is IWallpaperTransitions t)
            {
                var transition = new WallpaperTransition
                {
                    Type = TransitionType,
                    Duration = double.TryParse(TransitionDuration, System.Globalization.CultureInfo.InvariantCulture, out var dur) ? dur : 1.0,
                    Fps = int.TryParse(TransitionFps, out var fps) ? fps : 30,
                };
                await t.SetWallpaperAsync(monitor, imagePath, transition);
            }
            else
            {
                await _service.Backend.SetWallpaperAsync(monitor, imagePath);
            }

            var vm = Monitors.FirstOrDefault(m => m.Monitor == monitor);
            if (vm is not null)
                vm.CurrentImagePath = imagePath.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? null : imagePath;

            StatusMessage = $"Applied on {monitor}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed: {ex.Message}";
        }
    }

    internal string? GetRandomImagePath()
    {
        if (Images.Count == 0) return null;
        return Images[Random.Shared.Next(Images.Count)].Path;
    }

    private async void ApplyImageToSelected(string path)
    {
        foreach (var img in Images)
            img.IsSelected = img.Path == path;

        if (_selectedMonitors.Count > 0)
            foreach (var m in _selectedMonitors.ToList())
                await ApplyWallpaperAsync(m.Monitor, path);
        else
            StatusMessage = "Select a monitor first";
    }

    private async Task RefreshAsync()
    {
        if (_service.Backend is null) return;
        try
        {
            var active = await _service.Backend.GetActiveWallpapersAsync();
            var previousNames = _selectedMonitors.Select(m => m.Monitor).ToHashSet();
            _selectedMonitors.Clear();
            Monitors.Clear();
            foreach (var mw in active)
                Monitors.Add(new MonitorWallpaperViewModel(mw.Monitor, mw.ImagePath, this));
            foreach (var m in Monitors.Where(m => previousNames.Contains(m.Monitor)))
                ToggleMonitor(m);
            if (_selectedMonitors.Count == 0 && Monitors.Count > 0)
                ToggleMonitor(Monitors[0]);
            SelectedMonitorCount = _selectedMonitors.Count;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not query wallpapers: {ex.Message}";
        }
    }

    private async Task RandomAllAsync()
    {
        foreach (var monitor in Monitors)
        {
            var path = GetRandomImagePath();
            if (path is not null)
                await ApplyWallpaperAsync(monitor.Monitor, path);
        }
    }

    private static async Task LoadThumbnailsAsync(List<WallpaperImageViewModel> items)
    {
        foreach (var item in items)
        {
            var bitmap = await Task.Run(() => WallpaperImageViewModel.DecodeThumbnail(item.Path));
            await Dispatcher.UIThread.InvokeAsync(() => item.Thumbnail = bitmap);
        }
    }
}

public sealed class MonitorWallpaperViewModel : ViewModelBase
{
    private readonly WallpaperViewModel _parent;

    public MonitorWallpaperViewModel(string monitor, string? currentImagePath, WallpaperViewModel parent)
    {
        Monitor = monitor;
        CurrentImagePath = currentImagePath;
        _parent = parent;
        SelectCommand = new RelayCommand(() => parent.ToggleMonitor(this));
        RandomCommand = new AsyncRelayCommand(async () =>
        {
            var path = parent.GetRandomImagePath();
            if (path is not null)
                await parent.ApplyWallpaperAsync(monitor, path);
        });
        SetColorCommand = new AsyncRelayCommand(async () =>
        {
            var hex = $"0x{SelectedColor.R:X2}{SelectedColor.G:X2}{SelectedColor.B:X2}";
            await parent.ApplyWallpaperAsync(monitor, hex);
        });
    }

    public string Monitor { get; }

    public string? CurrentImagePath
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            OnPropertyChanged();
        }
    }

    public bool IsSelected
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            OnPropertyChanged();
        }
    }

    public Color SelectedColor
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedBrush));
        }
    } = Colors.Black;

    public SolidColorBrush SelectedBrush => new(SelectedColor);

    public ICommand SelectCommand { get; }
    public ICommand RandomCommand { get; }
    public ICommand SetColorCommand { get; }
}

public sealed class WallpaperImageViewModel : ViewModelBase
{
    private static readonly ConcurrentDictionary<string, Bitmap?> Cache = new();

    public WallpaperImageViewModel(string path, Action<string> onSelect)
    {
        Path = path;
        FileName = System.IO.Path.GetFileName(path);
        SelectCommand = new RelayCommand(() => onSelect(path));
        if (Cache.TryGetValue(path, out var cached))
            Thumbnail = cached;
    }

    public string Path { get; }
    public string FileName { get; }

    public bool IsSelected
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            OnPropertyChanged();
        }
    }

    public Bitmap? Thumbnail
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            OnPropertyChanged();
        }
    }

    public ICommand SelectCommand { get; }

    public static Bitmap? DecodeThumbnail(string path)
    {
        if (Cache.TryGetValue(path, out var cached))
            return cached;
        try
        {
            using var stream = File.OpenRead(path);
            var bmp = Bitmap.DecodeToWidth(stream, 200, BitmapInterpolationMode.LowQuality);
            Cache[path] = bmp;
            return bmp;
        }
        catch
        {
            Cache[path] = null;
            return null;
        }
    }
}
