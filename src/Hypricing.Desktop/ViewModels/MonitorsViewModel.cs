using System.Collections.ObjectModel;
using System.Windows.Input;
using Hypricing.Core.Services;

namespace Hypricing.Desktop.ViewModels;

public sealed class MonitorsViewModel : ViewModelBase
{
    private readonly HyprlandService _service;
    private double _canvasWidth = 600;
    private double _canvasHeight = 300;

    public MonitorsViewModel(HyprlandService service)
    {
        _service = service;
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        AddCommand = new RelayCommand(AddMonitor);
    }

    public ObservableCollection<MonitorItemViewModel> Monitors { get; } = [];

    public ICommand SaveCommand { get; }
    public ICommand AddCommand { get; }

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

    public MonitorItemViewModel? SelectedMonitor
    {
        get;
        set
        {
            if (field == value) return;
            if (field is not null)
                field.IsSelected = false;
            field = value;
            if (field is not null)
                field.IsSelected = true;
            OnPropertyChanged();
        }
    }

    public double CanvasWidth
    {
        get => _canvasWidth;
        set
        {
            if (Math.Abs(_canvasWidth - value) < 1) return;
            _canvasWidth = value;
            OnPropertyChanged();
            RecalculateLayout();
        }
    }

    public double CanvasHeight
    {
        get => _canvasHeight;
        set
        {
            if (Math.Abs(_canvasHeight - value) < 1) return;
            _canvasHeight = value;
            OnPropertyChanged();
            RecalculateLayout();
        }
    }

    public void Refresh()
    {
        Monitors.Clear();
        foreach (var node in _service.GetMonitors())
        {
            var vm = new MonitorItemViewModel(node, RemoveMonitor);
            Monitors.Add(vm);
        }

        if (Monitors.Count > 0)
            SelectedMonitor = Monitors[0];

        RecalculateLayout();

        // Fire-and-forget: populate available modes from live hyprctl data.
        // Failures are swallowed gracefully (e.g. no Hyprland session in tests).
        _ = LoadModesAsync();
    }

    /// <summary>
    /// Queries <c>hyprctl monitors -j</c> and populates <see cref="MonitorItemViewModel.AvailableModes"/>
    /// for each monitor. Runs asynchronously after <see cref="Refresh"/> so the UI is never blocked.
    /// </summary>
    private async Task LoadModesAsync()
    {
        try
        {
            var infos = await _service.GetMonitorInfoAsync();
            foreach (var vm in Monitors)
            {
                var info = infos.FirstOrDefault(i => i.Name == vm.Name);
                if (info is not null && info.AvailableModes.Count > 0)
                    vm.AvailableModes = info.AvailableModes;
            }
        }
        catch
        {
            // hyprctl unavailable (tests, no running Wayland session) — keep fallback values.
        }
    }

    /// <summary>
    /// Recalculates canvas positions/sizes for all monitors to fit within the canvas area.
    /// </summary>
    public void RecalculateLayout()
    {
        if (Monitors.Count == 0) return;

        // Find bounding box in real coordinates
        int minX = int.MaxValue, minY = int.MaxValue;
        int maxX = int.MinValue, maxY = int.MinValue;

        foreach (var m in Monitors)
        {
            if (m.X < minX) minX = m.X;
            if (m.Y < minY) minY = m.Y;
            if (m.X + m.Width > maxX) maxX = m.X + m.Width;
            if (m.Y + m.Height > maxY) maxY = m.Y + m.Height;
        }

        int totalWidth = maxX - minX;
        int totalHeight = maxY - minY;

        if (totalWidth <= 0 || totalHeight <= 0) return;

        // Scale to fit canvas with padding
        double padding = 40;
        double availW = _canvasWidth - padding * 2;
        double availH = _canvasHeight - padding * 2;

        if (availW <= 0 || availH <= 0) return;

        double scale = Math.Min(availW / totalWidth, availH / totalHeight);

        // Center offset
        double scaledTotalW = totalWidth * scale;
        double scaledTotalH = totalHeight * scale;
        double offsetX = (_canvasWidth - scaledTotalW) / 2;
        double offsetY = (_canvasHeight - scaledTotalH) / 2;

        foreach (var m in Monitors)
        {
            m.CanvasX = (m.X - minX) * scale + offsetX;
            m.CanvasY = (m.Y - minY) * scale + offsetY;
            m.CanvasWidth = m.Width * scale;
            m.CanvasHeight = m.Height * scale;
        }
    }

    /// <summary>
    /// Called from the view after drag ends. Converts canvas coordinates back to real
    /// coordinates and snaps flush to the nearest edge of another monitor.
    /// </summary>
    public void ApplyDragPosition(MonitorItemViewModel dragged, double canvasX, double canvasY)
    {
        if (Monitors.Count < 2)
        {
            // Single monitor — just reset to 0,0
            dragged.X = 0;
            dragged.Y = 0;
            RecalculateLayout();
            return;
        }

        // Reverse the scaling to get approximate real coordinates
        int minX = int.MaxValue, minY = int.MaxValue;
        int maxX = int.MinValue, maxY = int.MinValue;

        foreach (var m in Monitors)
        {
            if (m.X < minX) minX = m.X;
            if (m.Y < minY) minY = m.Y;
            if (m.X + m.Width > maxX) maxX = m.X + m.Width;
            if (m.Y + m.Height > maxY) maxY = m.Y + m.Height;
        }

        int totalWidth = Math.Max(maxX - minX, 1);
        int totalHeight = Math.Max(maxY - minY, 1);

        double padding = 40;
        double availW = _canvasWidth - padding * 2;
        double availH = _canvasHeight - padding * 2;
        if (availW <= 0 || availH <= 0) return;

        double scale = Math.Min(availW / totalWidth, availH / totalHeight);
        double offsetX = (_canvasWidth - totalWidth * scale) / 2;
        double offsetY = (_canvasHeight - totalHeight * scale) / 2;

        // Convert canvas position → approximate real coords
        int approxX = (int)Math.Round((canvasX - offsetX) / scale) + minX;
        int approxY = (int)Math.Round((canvasY - offsetY) / scale) + minY;

        // Find the best snap position: always stick to the closest edge of another monitor.
        // We test all possible snap placements (left-of, right-of, above, below each monitor)
        // and pick the one closest to where the user dropped it.
        int bestX = approxX, bestY = approxY;
        double bestDist = double.MaxValue;

        foreach (var other in Monitors)
        {
            if (ReferenceEquals(other, dragged)) continue;

            // Candidate positions: place dragged flush against each side of other
            // For each side, also try aligning tops/bottoms or lefts/rights
            ReadOnlySpan<(int cx, int cy)> candidates =
            [
                // Right of other, top-aligned
                (other.X + other.Width, other.Y),
                // Right of other, bottom-aligned
                (other.X + other.Width, other.Y + other.Height - dragged.Height),
                // Left of other, top-aligned
                (other.X - dragged.Width, other.Y),
                // Left of other, bottom-aligned
                (other.X - dragged.Width, other.Y + other.Height - dragged.Height),
                // Below other, left-aligned
                (other.X, other.Y + other.Height),
                // Below other, right-aligned
                (other.X + other.Width - dragged.Width, other.Y + other.Height),
                // Above other, left-aligned
                (other.X, other.Y - dragged.Height),
                // Above other, right-aligned
                (other.X + other.Width - dragged.Width, other.Y - dragged.Height),
            ];

            foreach (var (cx, cy) in candidates)
            {
                double dist = Math.Sqrt((double)(cx - approxX) * (cx - approxX)
                                      + (double)(cy - approxY) * (cy - approxY));
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestX = cx;
                    bestY = cy;
                }
            }
        }

        dragged.X = bestX;
        dragged.Y = bestY;
        RecalculateLayout();
    }

    private void RemoveMonitor(MonitorItemViewModel item)
    {
        _service.RemoveMonitor(item.Node);
        Monitors.Remove(item);
        if (SelectedMonitor == item)
            SelectedMonitor = Monitors.Count > 0 ? Monitors[0] : null;
        RecalculateLayout();
    }

    private void AddMonitor()
    {
        // Find a position to the right of existing monitors
        int maxRight = 0;
        foreach (var m in Monitors)
        {
            if (m.X + m.Width > maxRight)
                maxRight = m.X + m.Width;
        }

        _service.AddMonitor($",1920x1080@60,{maxRight}x0,1");
        Refresh();
    }

    private async Task SaveAsync()
    {
        try
        {
            await _service.SaveAsync();
            Refresh();
            StatusMessage = "Saved and reloaded";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Save failed: {ex.Message}";
        }
    }
}
