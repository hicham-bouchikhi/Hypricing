using System.Globalization;
using System.Windows.Input;
using Hypricing.Core.Services;

namespace Hypricing.Desktop.ViewModels;

public sealed class InputViewModel : ViewModelBase
{
    private readonly HyprlandService _service;
    private readonly XkbService _xkb;

    // Keyboard
    private string _kbLayout = string.Empty;
    private string _kbVariant = string.Empty;
    private string _kbModel = string.Empty;
    private string _kbOptions = string.Empty;
    private string _kbRules = string.Empty;
    private bool _numlockByDefault;
    private int _repeatRate = 25;
    private int _repeatDelay = 600;

    // Mouse (input section)
    private double _sensitivity;
    private string _accelProfile = string.Empty;
    private int _followMouse = 1;
    private bool _leftHanded;

    // Cursor (cursor section)
    private bool _noHardwareCursors;
    private int _inactiveTimeout;
    private bool _noWarps;
    private bool _persistentWarps;
    private bool _enableHyprcursor = true;
    private bool _hideOnKeyPress;
    private bool _hideOnTouch;

    // Touchpad (input:touchpad section)
    private bool _naturalScroll;
    private bool _tapToClick;
    private bool _disableWhileTyping = true;
    private bool _middleButtonEmulation;
    private double _scrollFactor = 1.0;
    private bool _dragLock;
    private bool _clickfingerBehavior;

    public InputViewModel(HyprlandService service, XkbService xkb)
    {
        _service = service;
        _xkb = xkb;
        SaveCommand = new AsyncRelayCommand(SaveAsync);
    }

    public ICommand SaveCommand { get; }

    public static IReadOnlyList<string> AccelProfiles { get; } = ["", "flat", "adaptive", "custom"];
    public static IReadOnlyList<string> FollowMouseOptions { get; } = ["0", "1", "2", "3"];

    public string[] Layouts
    {
        get;
        private set
        {
            field = value;
            OnPropertyChanged();
        }
    } = [];

    public string[] Variants
    {
        get;
        private set
        {
            field = value;
            OnPropertyChanged();
        }
    } = [];

    public string? StatusMessage
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    }

    // --- Keyboard ---

    public string KbLayout
    {
        get => _kbLayout;
        set
        {
            value ??= string.Empty;
            if (_kbLayout == value) return;
            _kbLayout = value;
            OnPropertyChanged();
            _ = LoadVariantsAsync(value);
        }
    }

    public string KbVariant
    {
        get => _kbVariant;
        set
        {
            value ??= string.Empty;
            if (_kbVariant == value) return;
            _kbVariant = value;
            OnPropertyChanged();
        }
    }

    public string KbModel
    {
        get => _kbModel;
        set { if (_kbModel == value) return; _kbModel = value; OnPropertyChanged(); }
    }

    public string KbOptions
    {
        get => _kbOptions;
        set { if (_kbOptions == value) return; _kbOptions = value; OnPropertyChanged(); }
    }

    public string KbRules
    {
        get => _kbRules;
        set { if (_kbRules == value) return; _kbRules = value; OnPropertyChanged(); }
    }

    public bool NumlockByDefault
    {
        get => _numlockByDefault;
        set { if (_numlockByDefault == value) return; _numlockByDefault = value; OnPropertyChanged(); }
    }

    public int RepeatRate
    {
        get => _repeatRate;
        set { if (_repeatRate == value) return; _repeatRate = value; OnPropertyChanged(); }
    }

    public int RepeatDelay
    {
        get => _repeatDelay;
        set { if (_repeatDelay == value) return; _repeatDelay = value; OnPropertyChanged(); }
    }

    // --- Mouse ---

    public double Sensitivity
    {
        get => _sensitivity;
        set { if (_sensitivity == value) return; _sensitivity = value; OnPropertyChanged(); }
    }

    public string AccelProfile
    {
        get => _accelProfile;
        set { if (_accelProfile == value) return; _accelProfile = value; OnPropertyChanged(); }
    }

    public int FollowMouse
    {
        get => _followMouse;
        set { if (_followMouse == value) return; _followMouse = value; OnPropertyChanged(); }
    }

    public bool LeftHanded
    {
        get => _leftHanded;
        set { if (_leftHanded == value) return; _leftHanded = value; OnPropertyChanged(); }
    }

    // --- Cursor ---

    public bool NoHardwareCursors
    {
        get => _noHardwareCursors;
        set { if (_noHardwareCursors == value) return; _noHardwareCursors = value; OnPropertyChanged(); }
    }

    public int InactiveTimeout
    {
        get => _inactiveTimeout;
        set { if (_inactiveTimeout == value) return; _inactiveTimeout = value; OnPropertyChanged(); }
    }

    public bool NoWarps
    {
        get => _noWarps;
        set { if (_noWarps == value) return; _noWarps = value; OnPropertyChanged(); }
    }

    public bool PersistentWarps
    {
        get => _persistentWarps;
        set { if (_persistentWarps == value) return; _persistentWarps = value; OnPropertyChanged(); }
    }

    public bool EnableHyprcursor
    {
        get => _enableHyprcursor;
        set { if (_enableHyprcursor == value) return; _enableHyprcursor = value; OnPropertyChanged(); }
    }

    public bool HideOnKeyPress
    {
        get => _hideOnKeyPress;
        set { if (_hideOnKeyPress == value) return; _hideOnKeyPress = value; OnPropertyChanged(); }
    }

    public bool HideOnTouch
    {
        get => _hideOnTouch;
        set { if (_hideOnTouch == value) return; _hideOnTouch = value; OnPropertyChanged(); }
    }

    // --- Touchpad ---

    public bool NaturalScroll
    {
        get => _naturalScroll;
        set { if (_naturalScroll == value) return; _naturalScroll = value; OnPropertyChanged(); }
    }

    public bool TapToClick
    {
        get => _tapToClick;
        set { if (_tapToClick == value) return; _tapToClick = value; OnPropertyChanged(); }
    }

    public bool DisableWhileTyping
    {
        get => _disableWhileTyping;
        set { if (_disableWhileTyping == value) return; _disableWhileTyping = value; OnPropertyChanged(); }
    }

    public bool MiddleButtonEmulation
    {
        get => _middleButtonEmulation;
        set { if (_middleButtonEmulation == value) return; _middleButtonEmulation = value; OnPropertyChanged(); }
    }

    public double ScrollFactor
    {
        get => _scrollFactor;
        set { if (_scrollFactor == value) return; _scrollFactor = value; OnPropertyChanged(); }
    }

    public bool DragLock
    {
        get => _dragLock;
        set { if (_dragLock == value) return; _dragLock = value; OnPropertyChanged(); }
    }

    public bool ClickfingerBehavior
    {
        get => _clickfingerBehavior;
        set { if (_clickfingerBehavior == value) return; _clickfingerBehavior = value; OnPropertyChanged(); }
    }

    // --- FollowMouse as string for ComboBox binding ---

    public string FollowMouseString
    {
        get => _followMouse.ToString(CultureInfo.InvariantCulture);
        set
        {
            var v = ParseInt(value, 1);
            if (_followMouse == v) return;
            _followMouse = v;
            OnPropertyChanged();
            OnPropertyChanged(nameof(FollowMouse));
        }
    }

    public async Task InitializeAsync()
    {
        try
        {
            Layouts = await _xkb.GetLayoutsAsync();
        }
        catch
        {
            // localectl unavailable — ComboBoxes still work as free-text via IsEditable
        }
    }

    public void Refresh()
    {
        // Keyboard
        _kbLayout = _service.GetSectionValue("input", null, "kb_layout") ?? string.Empty;
        _kbVariant = _service.GetSectionValue("input", null, "kb_variant") ?? string.Empty;
        _kbModel = _service.GetSectionValue("input", null, "kb_model") ?? string.Empty;
        _kbOptions = _service.GetSectionValue("input", null, "kb_options") ?? string.Empty;
        _kbRules = _service.GetSectionValue("input", null, "kb_rules") ?? string.Empty;
        _numlockByDefault = ParseBool(_service.GetSectionValue("input", null, "numlock_by_default"), false);
        _repeatRate = ParseInt(_service.GetSectionValue("input", null, "repeat_rate"), 25);
        _repeatDelay = ParseInt(_service.GetSectionValue("input", null, "repeat_delay"), 600);

        // Mouse
        _sensitivity = ParseDouble(_service.GetSectionValue("input", null, "sensitivity"), 0.0);
        _accelProfile = _service.GetSectionValue("input", null, "accel_profile") ?? string.Empty;
        _followMouse = ParseInt(_service.GetSectionValue("input", null, "follow_mouse"), 1);
        _leftHanded = ParseBool(_service.GetSectionValue("input", null, "left_handed"), false);

        // Cursor
        _noHardwareCursors = ParseBool(_service.GetSectionValue("cursor", null, "no_hardware_cursors"), false);
        _inactiveTimeout = ParseInt(_service.GetSectionValue("cursor", null, "inactive_timeout"), 0);
        _noWarps = ParseBool(_service.GetSectionValue("cursor", null, "no_warps"), false);
        _persistentWarps = ParseBool(_service.GetSectionValue("cursor", null, "persistent_warps"), false);
        _enableHyprcursor = ParseBool(_service.GetSectionValue("cursor", null, "enable_hyprcursor"), true);
        _hideOnKeyPress = ParseBool(_service.GetSectionValue("cursor", null, "hide_on_key_press"), false);
        _hideOnTouch = ParseBool(_service.GetSectionValue("cursor", null, "hide_on_touch"), false);

        // Touchpad
        _naturalScroll = ParseBool(_service.GetSectionValue("input", "touchpad", "natural_scroll"), false);
        _tapToClick = ParseBool(_service.GetSectionValue("input", "touchpad", "tap-to-click"), false);
        _disableWhileTyping = ParseBool(_service.GetSectionValue("input", "touchpad", "disable_while_typing"), true);
        _middleButtonEmulation = ParseBool(_service.GetSectionValue("input", "touchpad", "middle_button_emulation"), false);
        _scrollFactor = ParseDouble(_service.GetSectionValue("input", "touchpad", "scroll_factor"), 1.0);
        _dragLock = ParseBool(_service.GetSectionValue("input", "touchpad", "drag_lock"), false);
        _clickfingerBehavior = ParseBool(_service.GetSectionValue("input", "touchpad", "clickfinger_behavior"), false);

        OnPropertyChanged(string.Empty);
        _ = LoadVariantsAsync(_kbLayout);
    }

    private async Task LoadVariantsAsync(string layout)
    {
        Variants = await _xkb.GetVariantsAsync(layout);
    }

    private async Task SaveAsync()
    {
        try
        {
            // Keyboard
            SetStringValue("input", null, "kb_layout", _kbLayout);
            SetStringValue("input", null, "kb_variant", _kbVariant);
            SetStringValue("input", null, "kb_model", _kbModel);
            SetStringValue("input", null, "kb_options", _kbOptions);
            SetStringValue("input", null, "kb_rules", _kbRules);
            _service.SetSectionValue("input", null, "numlock_by_default", BoolToString(_numlockByDefault));
            _service.SetSectionValue("input", null, "repeat_rate", _repeatRate.ToString(CultureInfo.InvariantCulture));
            _service.SetSectionValue("input", null, "repeat_delay", _repeatDelay.ToString(CultureInfo.InvariantCulture));

            // Mouse
            _service.SetSectionValue("input", null, "sensitivity", _sensitivity.ToString(CultureInfo.InvariantCulture));
            SetStringValue("input", null, "accel_profile", _accelProfile);
            _service.SetSectionValue("input", null, "follow_mouse", _followMouse.ToString(CultureInfo.InvariantCulture));
            _service.SetSectionValue("input", null, "left_handed", BoolToString(_leftHanded));

            // Cursor
            _service.SetSectionValue("cursor", null, "no_hardware_cursors", BoolToString(_noHardwareCursors));
            _service.SetSectionValue("cursor", null, "inactive_timeout", _inactiveTimeout.ToString(CultureInfo.InvariantCulture));
            _service.SetSectionValue("cursor", null, "no_warps", BoolToString(_noWarps));
            _service.SetSectionValue("cursor", null, "persistent_warps", BoolToString(_persistentWarps));
            _service.SetSectionValue("cursor", null, "enable_hyprcursor", BoolToString(_enableHyprcursor));
            _service.SetSectionValue("cursor", null, "hide_on_key_press", BoolToString(_hideOnKeyPress));
            _service.SetSectionValue("cursor", null, "hide_on_touch", BoolToString(_hideOnTouch));

            // Touchpad
            _service.SetSectionValue("input", "touchpad", "natural_scroll", BoolToString(_naturalScroll));
            _service.SetSectionValue("input", "touchpad", "tap-to-click", BoolToString(_tapToClick));
            _service.SetSectionValue("input", "touchpad", "disable_while_typing", BoolToString(_disableWhileTyping));
            _service.SetSectionValue("input", "touchpad", "middle_button_emulation", BoolToString(_middleButtonEmulation));
            _service.SetSectionValue("input", "touchpad", "scroll_factor", _scrollFactor.ToString(CultureInfo.InvariantCulture));
            _service.SetSectionValue("input", "touchpad", "drag_lock", BoolToString(_dragLock));
            _service.SetSectionValue("input", "touchpad", "clickfinger_behavior", BoolToString(_clickfingerBehavior));

            await _service.SaveAsync();
            Refresh();
            StatusMessage = "Saved and reloaded";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Save failed: {ex.Message}";
        }
    }

    private void SetStringValue(string section, string? device, string key, string value)
    {
        _service.SetSectionValue(section, device, key,
            string.IsNullOrWhiteSpace(value) ? null : value);
    }

    private static bool ParseBool(string? value, bool defaultValue)
    {
        if (value is null) return defaultValue;
        return value is "true" or "yes" or "1";
    }

    private static int ParseInt(string? value, int defaultValue)
    {
        if (value is null) return defaultValue;
        return int.TryParse(value, CultureInfo.InvariantCulture, out var result) ? result : defaultValue;
    }

    private static double ParseDouble(string? value, double defaultValue)
    {
        if (value is null) return defaultValue;
        return double.TryParse(value, CultureInfo.InvariantCulture, out var result) ? result : defaultValue;
    }

    private static string BoolToString(bool value) => value ? "true" : "false";
}
