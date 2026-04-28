using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Input;
using Hypricing.HyprlangParser.Nodes;

namespace Hypricing.Desktop.ViewModels;

/// <summary>
/// Wraps a monitor <see cref="KeywordNode"/> for data binding.
/// Params format: NAME,WIDTHxHEIGHT@HZ,POSXxPOSY,SCALE
/// Example: DP-1,1920x1080@144,0x0,1
/// </summary>
public sealed partial class MonitorItemViewModel : ViewModelBase
{
    private string _name = string.Empty;
    private int _width;
    private int _height;
    private double _refreshRate;
    private int _x;
    private int _y;
    private double _scale = 1;
    private IReadOnlyList<string> _availableModes = [];

    // Canvas-relative properties (set by the layout logic)

    public MonitorItemViewModel(KeywordNode node, Action<MonitorItemViewModel>? onRemove = null)
    {
        Node = node;
        RemoveCommand = new RelayCommand(() => onRemove?.Invoke(this));
        Parse(node.Params);
        // Provide a single-item fallback so the ComboBox is never empty before
        // live modes are loaded from hyprctl.
        _availableModes = [Resolution];
    }

    internal KeywordNode Node { get; }

    public ICommand RemoveCommand { get; }

    public string Name
    {
        get => _name;
        set { if (_name != value) { _name = value; OnPropertyChanged(); SyncToNode(); } }
    }

    public int Width
    {
        get => _width;
        set { if (_width != value) { _width = value; OnPropertyChanged(); SyncToNode(); } }
    }

    public int Height
    {
        get => _height;
        set { if (_height != value) { _height = value; OnPropertyChanged(); SyncToNode(); } }
    }

    public double RefreshRate
    {
        get => _refreshRate;
        set { if (Math.Abs(_refreshRate - value) > 0.001) { _refreshRate = value; OnPropertyChanged(); SyncToNode(); } }
    }

    public int X
    {
        get => _x;
        set { if (_x != value) { _x = value; OnPropertyChanged(); SyncToNode(); } }
    }

    public int Y
    {
        get => _y;
        set { if (_y != value) { _y = value; OnPropertyChanged(); SyncToNode(); } }
    }

    public double Scale
    {
        get => _scale;
        set { if (Math.Abs(_scale - value) > 0.001) { _scale = value; OnPropertyChanged(); SyncToNode(); } }
    }

    public bool IsSelected
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged();
            }
        }
    }

    public double CanvasX
    {
        get;
        set
        {
            if (Math.Abs(field - value) > 0.1)
            {
                field = value;
                OnPropertyChanged();
            }
        }
    }

    public double CanvasY
    {
        get;
        set
        {
            if (Math.Abs(field - value) > 0.1)
            {
                field = value;
                OnPropertyChanged();
            }
        }
    }

    public double CanvasWidth
    {
        get;
        set
        {
            if (Math.Abs(field - value) > 0.1)
            {
                field = value;
                OnPropertyChanged();
            }
        }
    }

    public double CanvasHeight
    {
        get;
        set
        {
            if (Math.Abs(field - value) > 0.1)
            {
                field = value;
                OnPropertyChanged();
            }
        }
    }

    public string Resolution => $"{_width}x{_height}@{_refreshRate.ToString(CultureInfo.InvariantCulture)}";
    public string Position => $"{_x}x{_y}";

    /// <summary>
    /// Available resolution modes from <c>hyprctl monitors -j</c>.
    /// Populated after a live hyprctl query; falls back to [Resolution] when unavailable.
    /// </summary>
    public IReadOnlyList<string> AvailableModes
    {
        get => _availableModes;
        set
        {
            _availableModes = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedMode));
        }
    }

    /// <summary>
    /// The currently selected mode string (matches an entry in <see cref="AvailableModes"/>).
    /// Setting this parses the mode string and updates Width, Height and RefreshRate.
    /// </summary>
    public string? SelectedMode
    {
        get
        {
            // Find the mode in AvailableModes that matches the current resolution numerically.
            foreach (var mode in _availableModes)
            {
                var stripped = mode.EndsWith("Hz", StringComparison.OrdinalIgnoreCase)
                    ? mode[..^2] : mode;
                var m = ResolutionRegex().Match(stripped.Trim());
                if (!m.Success) continue;
                var w = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                var h = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
                var hz = m.Groups[3].Success
                    ? double.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture) : 60;
                if (w == _width && h == _height && Math.Abs(hz - _refreshRate) < 0.5)
                    return mode;
            }
            // Fallback: return the raw resolution string so the ComboBox isn't blank.
            return _availableModes.Count > 0 ? null : Resolution;
        }
        set
        {
            if (value is null) return;
            // Strip trailing "Hz" if present: "1920x1080@144.00Hz" → "1920x1080@144.00"
            var stripped = value.EndsWith("Hz", StringComparison.OrdinalIgnoreCase)
                ? value[..^2] : value;
            var m = ResolutionRegex().Match(stripped.Trim());
            if (!m.Success) return;
            _width = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
            _height = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
            _refreshRate = m.Groups[3].Success
                ? double.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture) : 60;
            OnPropertyChanged(nameof(Width));
            OnPropertyChanged(nameof(Height));
            OnPropertyChanged(nameof(RefreshRate));
            OnPropertyChanged(nameof(Resolution));
            OnPropertyChanged();
            SyncToNode();
        }
    }

    private void Parse(string @params)
    {
        // Format: NAME,WIDTHxHEIGHT@HZ,POSXxPOSY,SCALE
        // Also handles: NAME,preferred,auto,1 or NAME,disable
        var parts = @params.Split(',', 4);
        if (parts.Length < 1) return;

        _name = parts[0].Trim();

        if (parts.Length >= 2)
        {
            var resMatch = ResolutionRegex().Match(parts[1].Trim());
            if (resMatch.Success)
            {
                _width = int.Parse(resMatch.Groups[1].Value);
                _height = int.Parse(resMatch.Groups[2].Value);
                _refreshRate = resMatch.Groups[3].Success
                    ? double.Parse(resMatch.Groups[3].Value, CultureInfo.InvariantCulture)
                    : 60;
            }
        }

        if (parts.Length >= 3)
        {
            var posMatch = PositionRegex().Match(parts[2].Trim());
            if (posMatch.Success)
            {
                _x = int.Parse(posMatch.Groups[1].Value);
                _y = int.Parse(posMatch.Groups[2].Value);
            }
        }

        if (parts.Length >= 4)
        {
            if (double.TryParse(parts[3].Trim(), CultureInfo.InvariantCulture, out var s))
                _scale = s;
        }
    }

    private void SyncToNode()
    {
        Node.Params = $"{_name},{_width}x{_height}@{_refreshRate.ToString(CultureInfo.InvariantCulture)},{_x}x{_y},{_scale.ToString(CultureInfo.InvariantCulture)}";
    }

    [GeneratedRegex(@"^(\d+)x(\d+)(?:@(\d+(?:\.\d+)?))?$")]
    private static partial Regex ResolutionRegex();

    [GeneratedRegex(@"^(-?\d+)x(-?\d+)$")]
    private static partial Regex PositionRegex();
}
