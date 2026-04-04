using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;
using Hypricing.Desktop.ViewModels;

namespace Hypricing.Desktop.Views;

public partial class MonitorsView : UserControl
{
    private MonitorItemViewModel? _dragging;
    private Point _dragOffset;
    private Canvas? _canvas;

    public MonitorsView()
    {
        InitializeComponent();
    }

    protected override void OnLoaded(global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        base.OnLoaded(e);
        _canvas = this.FindControl<Canvas>("MonitorCanvas");
        if (_canvas is null) return;

        _canvas.SizeChanged += (_, _) => OnCanvasSizeChanged();

        if (DataContext is MonitorsViewModel vm)
        {
            vm.Monitors.CollectionChanged += OnMonitorsChanged;
            OnCanvasSizeChanged();
            RebuildCanvas();
        }
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_canvas is not null && DataContext is MonitorsViewModel vm)
        {
            vm.Monitors.CollectionChanged += OnMonitorsChanged;
            OnCanvasSizeChanged();
            RebuildCanvas();
        }
    }

    private void OnCanvasSizeChanged()
    {
        if (_canvas is null || DataContext is not MonitorsViewModel vm) return;
        vm.CanvasWidth = Math.Max(_canvas.Bounds.Width, 200);
        vm.CanvasHeight = Math.Max(_canvas.Bounds.Height, 150);
    }

    private void OnMonitorsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RebuildCanvas();
    }

    private void RebuildCanvas()
    {
        if (_canvas is null || DataContext is not MonitorsViewModel vm) return;

        _canvas.Children.Clear();

        foreach (var monitor in vm.Monitors)
        {
            var block = CreateMonitorBlock(monitor);
            _canvas.Children.Add(block);
            UpdateBlockPosition(block, monitor);

            // Listen for property changes to update position
            monitor.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName is nameof(MonitorItemViewModel.CanvasX)
                    or nameof(MonitorItemViewModel.CanvasY)
                    or nameof(MonitorItemViewModel.CanvasWidth)
                    or nameof(MonitorItemViewModel.CanvasHeight)
                    or nameof(MonitorItemViewModel.IsSelected))
                {
                    UpdateBlockPosition(block, monitor);
                    UpdateBlockAppearance(block, monitor);
                }
            };
        }
    }

    private Border CreateMonitorBlock(MonitorItemViewModel monitor)
    {
        var nameText = new TextBlock
        {
            Text = monitor.Name,
            FontSize = 13,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(Color.Parse("#cdd6f4")),
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
        };

        var resText = new TextBlock
        {
            Text = monitor.Resolution,
            FontSize = 10,
            Foreground = new SolidColorBrush(Color.Parse("#6c7086")),
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
        };

        // Bind text updates
        monitor.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(MonitorItemViewModel.Name))
                nameText.Text = monitor.Name;
            if (args.PropertyName is nameof(MonitorItemViewModel.Width)
                or nameof(MonitorItemViewModel.Height)
                or nameof(MonitorItemViewModel.RefreshRate))
                resText.Text = monitor.Resolution;
        };

        var stack = new StackPanel
        {
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Spacing = 2,
        };
        stack.Children.Add(nameText);
        stack.Children.Add(resText);

        var border = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#313244")),
            BorderBrush = new SolidColorBrush(Color.Parse("#45475a")),
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(8),
            Child = stack,
            Cursor = new Cursor(StandardCursorType.Hand),
            Tag = monitor,
        };

        border.PointerPressed += OnMonitorPointerPressed;
        border.PointerMoved += OnMonitorPointerMoved;
        border.PointerReleased += OnMonitorPointerReleased;

        return border;
    }

    private static void UpdateBlockPosition(Border block, MonitorItemViewModel monitor)
    {
        Canvas.SetLeft(block, monitor.CanvasX);
        Canvas.SetTop(block, monitor.CanvasY);
        block.Width = Math.Max(monitor.CanvasWidth, 40);
        block.Height = Math.Max(monitor.CanvasHeight, 30);
    }

    private static void UpdateBlockAppearance(Border block, MonitorItemViewModel monitor)
    {
        block.BorderBrush = new SolidColorBrush(
            Color.Parse(monitor.IsSelected ? "#89b4fa" : "#45475a"));
        block.Background = new SolidColorBrush(
            Color.Parse(monitor.IsSelected ? "#3b3b5c" : "#313244"));
    }

    private void OnMonitorPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Border border || border.Tag is not MonitorItemViewModel monitor) return;
        if (DataContext is not MonitorsViewModel vm) return;

        _dragging = monitor;
        _dragOffset = e.GetPosition(border);
        vm.SelectedMonitor = monitor;
        e.Pointer.Capture(border);
        e.Handled = true;
    }

    private void OnMonitorPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_dragging is null || _canvas is null) return;
        if (sender is not Border border) return;

        var pos = e.GetPosition(_canvas);
        var newX = pos.X - _dragOffset.X;
        var newY = pos.Y - _dragOffset.Y;

        // Clamp to canvas bounds
        newX = Math.Max(0, Math.Min(newX, _canvas.Bounds.Width - border.Width));
        newY = Math.Max(0, Math.Min(newY, _canvas.Bounds.Height - border.Height));

        Canvas.SetLeft(border, newX);
        Canvas.SetTop(border, newY);
        e.Handled = true;
    }

    private void OnMonitorPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_dragging is null || _canvas is null || sender is not Border border) return;
        if (DataContext is not MonitorsViewModel vm) return;

        var finalX = Canvas.GetLeft(border);
        var finalY = Canvas.GetTop(border);

        vm.ApplyDragPosition(_dragging, finalX, finalY);

        e.Pointer.Capture(null);
        _dragging = null;
        e.Handled = true;
    }
}
