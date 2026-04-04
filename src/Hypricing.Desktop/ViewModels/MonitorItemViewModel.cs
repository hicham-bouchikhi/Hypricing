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
    private readonly KeywordNode _node;
    private string _name = string.Empty;
    private int _width;
    private int _height;
    private double _refreshRate;
    private int _x;
    private int _y;
    private double _scale = 1;
    private bool _isSelected;

    // Canvas-relative properties (set by the layout logic)
    private double _canvasX;
    private double _canvasY;
    private double _canvasWidth;
    private double _canvasHeight;

    public MonitorItemViewModel(KeywordNode node, Action<MonitorItemViewModel>? onRemove = null)
    {
        _node = node;
        RemoveCommand = new RelayCommand(() => onRemove?.Invoke(this));
        Parse(node.Params);
    }

    internal KeywordNode Node => _node;
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
        get => _isSelected;
        set { if (_isSelected != value) { _isSelected = value; OnPropertyChanged(); } }
    }

    public double CanvasX
    {
        get => _canvasX;
        set { if (Math.Abs(_canvasX - value) > 0.1) { _canvasX = value; OnPropertyChanged(); } }
    }

    public double CanvasY
    {
        get => _canvasY;
        set { if (Math.Abs(_canvasY - value) > 0.1) { _canvasY = value; OnPropertyChanged(); } }
    }

    public double CanvasWidth
    {
        get => _canvasWidth;
        set { if (Math.Abs(_canvasWidth - value) > 0.1) { _canvasWidth = value; OnPropertyChanged(); } }
    }

    public double CanvasHeight
    {
        get => _canvasHeight;
        set { if (Math.Abs(_canvasHeight - value) > 0.1) { _canvasHeight = value; OnPropertyChanged(); } }
    }

    public string Resolution => $"{_width}x{_height}@{_refreshRate.ToString(CultureInfo.InvariantCulture)}";
    public string Position => $"{_x}x{_y}";

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
        _node.Params = $"{_name},{_width}x{_height}@{_refreshRate.ToString(CultureInfo.InvariantCulture)},{_x}x{_y},{_scale.ToString(CultureInfo.InvariantCulture)}";
    }

    [GeneratedRegex(@"^(\d+)x(\d+)(?:@(\d+(?:\.\d+)?))?$")]
    private static partial Regex ResolutionRegex();

    [GeneratedRegex(@"^(-?\d+)x(-?\d+)$")]
    private static partial Regex PositionRegex();
}
