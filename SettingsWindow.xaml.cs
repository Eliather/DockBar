using System;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DockBar.Models;
using DockBar.Services;

namespace DockBar;

public partial class SettingsWindow : Window, INotifyPropertyChanged
{
    private const double GlassOpacity = 0.45;

    public DockConfig Config { get; }

    private SolidColorBrush _previewBrush = new(System.Windows.Media.Color.FromRgb(16, 16, 16));
    private SolidColorBrush _textBrush = new(System.Windows.Media.Color.FromRgb(242, 242, 242));
    private SolidColorBrush _settingsBackgroundBrush = new(System.Windows.Media.Color.FromRgb(0, 0, 0));
    private SolidColorBrush _hueBrush = new(System.Windows.Media.Color.FromRgb(255, 0, 0));
    private string _hexInput = "#101010";
    private byte _pendingR;
    private byte _pendingG;
    private byte _pendingB;
    private double _hue;
    private double _sat = 1.0;
    private double _val = 1.0;
    private bool _isEditingBackground = true;

    public bool IsEditingBackground
    {
        get => _isEditingBackground;
        set
        {
            if (_isEditingBackground != value)
            {
                _isEditingBackground = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsEditingAccent));
                OnPropertyChanged(nameof(ColorPickerTitle));
                OnPropertyChanged(nameof(HexLabelText));
                OnPropertyChanged(nameof(ActiveColorBrush));
                SyncHsvToActiveTarget();
            }
        }
    }

    public bool IsEditingAccent
    {
        get => !_isEditingBackground;
        set => IsEditingBackground = !value;
    }

    public string ColorPickerTitle => LocalizationService.Get(_isEditingBackground ? "Settings_ColorPickerBg" : "Settings_ColorPickerAccent");
    public string HexLabelText => LocalizationService.Get(_isEditingBackground ? "Settings_HexLabelBg" : "Settings_HexLabelAccent");

    public SolidColorBrush ActiveColorBrush => _isEditingBackground ? PreviewBrush : AccentPreviewBrush;

    public SolidColorBrush AccentPreviewBrush
    {
        get
        {
            var brush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(Config.AccentR, Config.AccentG, Config.AccentB));
            brush.Freeze();
            return brush;
        }
    }

    public SolidColorBrush PreviewBrush
    {
        get => _previewBrush;
        private set
        {
            if (_previewBrush != value)
            {
                _previewBrush = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ActiveColorBrush));
            }
        }
    }

    public SolidColorBrush TextBrush
    {
        get => _textBrush;
        private set
        {
            if (_textBrush != value)
            {
                _textBrush = value;
                OnPropertyChanged();
            }
        }
    }

    public SolidColorBrush SettingsBackgroundBrush
    {
        get => _settingsBackgroundBrush;
        private set
        {
            if (_settingsBackgroundBrush != value)
            {
                _settingsBackgroundBrush = value;
                OnPropertyChanged();
            }
        }
    }

    public SolidColorBrush HueBrush
    {
        get => _hueBrush;
        private set
        {
            if (_hueBrush != value)
            {
                _hueBrush = value;
                OnPropertyChanged();
            }
        }
    }

    public string HexColor => $"#{_pendingR:X2}{_pendingG:X2}{_pendingB:X2}";

    public string OpacityPercentText
    {
        get
        {
            var op = Config.UseTransparency
                ? Math.Clamp(Config.BackgroundOpacity, 0.0, 1.0)
                : 1.0;
            return $"{Math.Round(op * 100):F0}%";
        }
    }

    public string GlassStatusText =>
        LocalizationService.Get(Config.UseTransparency ? "Settings_GlassOn" : "Settings_GlassOff") +
        (Config.UseTransparency ? $" ({OpacityPercentText})" : "");

    public string HexInput
    {
        get => _hexInput;
        set
        {
            if (_hexInput != value)
            {
                _hexInput = value;
                OnPropertyChanged();
            }
        }
    }

    public double Hue
    {
        get => _hue;
        set
        {
            if (Math.Abs(_hue - value) > double.Epsilon)
            {
                _hue = value;
                OnPropertyChanged();
                UpdateHueBrush();
                ApplyHsvToPending();
                UpdateSatValThumb();
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public SettingsWindow(DockConfig config)
    {
        Config = config;
        InitializeComponent();
        DataContext = this;
        SourceInitialized += (_, _) =>
        {
            WindowSwitcherHelper.HideFromWindowSwitchers(this);
            ThemeService.ApplyWindowBackdrop(this, Config);
        };
        if (Config.BackgroundOpacity < 0 || Config.BackgroundOpacity > 1.0)
        {
            Config.BackgroundOpacity = GlassOpacity;
        }
        _pendingR = Config.BackgroundR;
        _pendingG = Config.BackgroundG;
        _pendingB = Config.BackgroundB;
        SyncHsvToActiveTarget();
        UpdatePreviewBrush();
        UpdateTextBrush();
    }

    private void ColorTargetChanged(object sender, RoutedEventArgs e)
    {
        SyncHsvToActiveTarget();
    }

    private void SyncHsvToActiveTarget()
    {
        if (_isEditingBackground)
        {
            RgbToHsv(_pendingR, _pendingG, _pendingB, out _hue, out _sat, out _val);
            var hex = $"#{_pendingR:X2}{_pendingG:X2}{_pendingB:X2}";
            if (!string.Equals(HexInput, hex, StringComparison.OrdinalIgnoreCase))
            {
                HexInput = hex;
            }
        }
        else
        {
            var ar = Config.AccentR != 0 || Config.AccentG != 0 || Config.AccentB != 0 ? Config.AccentR : (byte)55;
            var ag = Config.AccentR != 0 || Config.AccentG != 0 || Config.AccentB != 0 ? Config.AccentG : (byte)115;
            var ab = Config.AccentR != 0 || Config.AccentG != 0 || Config.AccentB != 0 ? Config.AccentB : (byte)245;
            RgbToHsv(ar, ag, ab, out _hue, out _sat, out _val);
            var hex = $"#{ar:X2}{ag:X2}{ab:X2}";
            if (!string.Equals(HexInput, hex, StringComparison.OrdinalIgnoreCase))
            {
                HexInput = hex;
            }
        }
        OnPropertyChanged(nameof(Hue));
        OnPropertyChanged(nameof(HexLabelText));
        OnPropertyChanged(nameof(ColorPickerTitle));
        OnPropertyChanged(nameof(ActiveColorBrush));
        OnPropertyChanged(nameof(AccentPreviewBrush));
        UpdateHueBrush();
        UpdateSatValThumb();
    }

    private void UpdatePreviewBrush()
    {
        var baseColor = System.Windows.Media.Color.FromRgb(_pendingR, _pendingG, _pendingB);
        var opacity = Config.UseTransparency
            ? Math.Clamp(Config.BackgroundOpacity, 0.0, 1.0)
            : 1.0;
        var brush = new SolidColorBrush(baseColor)
        {
            Opacity = opacity
        };
        brush.Freeze();
        PreviewBrush = brush;
        OnPropertyChanged(nameof(HexColor));
        OnPropertyChanged(nameof(OpacityPercentText));
        OnPropertyChanged(nameof(GlassStatusText));
        if (_isEditingBackground)
        {
            var hex = $"#{_pendingR:X2}{_pendingG:X2}{_pendingB:X2}";
            if (!string.Equals(HexInput, hex, StringComparison.OrdinalIgnoreCase))
            {
                HexInput = hex;
            }
        }
        UpdateHueBrush();
        UpdateSatValThumb();
        UpdateTextBrush();
    }

    private void ColorComponentChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        UpdatePreviewBrush();
    }

    private void TransparencyToggled(object sender, RoutedEventArgs e)
    {
        if (Config.UseTransparency && (Config.BackgroundOpacity < 0 || Config.BackgroundOpacity > 1.0))
        {
            Config.BackgroundOpacity = GlassOpacity;
        }
        OnPropertyChanged(nameof(OpacityPercentText));
        OnPropertyChanged(nameof(GlassStatusText));
        UpdatePreviewBrush();
    }

    private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        OnPropertyChanged(nameof(OpacityPercentText));
        OnPropertyChanged(nameof(GlassStatusText));
        UpdatePreviewBrush();
    }

    private void UpdateTextBrush()
    {
        var color = Config.UseLightText
            ? System.Windows.Media.Color.FromRgb(242, 242, 242)
            : System.Windows.Media.Color.FromRgb(10, 10, 10);
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        TextBrush = brush;

        var backgroundColor = Config.UseLightText
            ? System.Windows.Media.Color.FromRgb(0, 0, 0)
            : System.Windows.Media.Color.FromRgb(255, 255, 255);
        var backgroundBrush = new SolidColorBrush(backgroundColor);
        backgroundBrush.Freeze();
        SettingsBackgroundBrush = backgroundBrush;
    }

    private void NumericSettingChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        // Placeholder to keep symmetry; settings are already bound TwoWay.
    }

    private void PresetWin8_Click(object sender, RoutedEventArgs e)
    {
        Config.AutoHideDelaySeconds = 0;
        Config.HideAnimationMs = 200;
        Config.BackgroundR = 0;
        Config.BackgroundG = 0;
        Config.BackgroundB = 0;
        Config.UseTransparency = false;
        Config.BackgroundOpacity = GlassOpacity;
        Config.DockWidth = 175;
        Config.IconSize = 40;
        Config.UseLightText = true;
        Config.AccentR = 55;
        Config.AccentG = 115;
        Config.AccentB = 245;
        _pendingR = Config.BackgroundR;
        _pendingG = Config.BackgroundG;
        _pendingB = Config.BackgroundB;
        SyncHsvToActiveTarget();
        UpdatePreviewBrush();
        ThemeService.Apply(Config);
        OnPropertyChanged(nameof(Config));
        OnPropertyChanged(nameof(AccentPreviewBrush));
    }

    private bool TryApplyHex(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var raw = text.Trim();
        if (raw.StartsWith("#")) raw = raw[1..];
        if (raw.Length != 6) return false;

        if (!int.TryParse(raw, System.Globalization.NumberStyles.HexNumber, null, out var value))
        {
            return false;
        }

        byte r = (byte)((value >> 16) & 0xFF);
        byte g = (byte)((value >> 8) & 0xFF);
        byte b = (byte)(value & 0xFF);

        if (_isEditingBackground)
        {
            _pendingR = r;
            _pendingG = g;
            _pendingB = b;
            SyncHsvToActiveTarget();
            UpdatePreviewBrush();
        }
        else
        {
            Config.AccentR = r;
            Config.AccentG = g;
            Config.AccentB = b;
            SyncHsvToActiveTarget();
            ThemeService.Apply(Config);
            OnPropertyChanged(nameof(Config));
            OnPropertyChanged(nameof(AccentPreviewBrush));
        }
        return true;
    }

    private void HexBox_LostFocus(object sender, RoutedEventArgs e)
    {
        TryApplyHex(HexInput);
    }

    private void HexBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter)
        {
            if (TryApplyHex(HexInput))
            {
                e.Handled = true;
            }
        }
    }

    private void Header_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
        {
            DragMove();
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        // Commit pending color
        Config.BackgroundR = _pendingR;
        Config.BackgroundG = _pendingG;
        Config.BackgroundB = _pendingB;
        ThemeService.Apply(Config);
        DialogResult = true;
        Close();
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        var version = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
            ?? LocalizationService.Get("About_UnknownVersion");
        var aboutWindow = new AboutWindow(version)
        {
            Owner = this
        };
        aboutWindow.ShowDialog();
    }

    private void HueSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        Hue = e.NewValue;
    }

    private void SatValCanvas_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        var pos = e.GetPosition(SatValCanvas);
        UpdateSatValFromPoint(pos);
    }

    private void SatValCanvas_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
        {
            var pos = e.GetPosition(SatValCanvas);
            UpdateSatValFromPoint(pos);
        }
    }

    private void SatValBorder_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateSatValThumb();
    }

    private void SatValBorder_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateSatValThumb();
    }

    private void Swatch_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button btn || btn.Tag is not string tag)
        {
            return;
        }

        if (TryParseHex(tag, out var r, out var g, out var b))
        {
            _pendingR = r;
            _pendingG = g;
            _pendingB = b;
            if (_isEditingBackground)
            {
                SyncHsvToActiveTarget();
            }
            UpdatePreviewBrush();
        }
    }

    private void AccentSwatch_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button btn || btn.Tag is not string tag)
        {
            return;
        }

        if (TryParseHex(tag, out var r, out var g, out var b))
        {
            Config.AccentR = r;
            Config.AccentG = g;
            Config.AccentB = b;
            if (!_isEditingBackground)
            {
                SyncHsvToActiveTarget();
            }
            ThemeService.Apply(Config);
            OnPropertyChanged(nameof(Config));
            OnPropertyChanged(nameof(AccentPreviewBrush));
        }
    }

    private static bool TryParseHex(string? text, out byte r, out byte g, out byte b)
    {
        r = 55; g = 115; b = 245;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var raw = text.Trim();
        if (raw.StartsWith("#")) raw = raw[1..];
        if (raw.Length != 6) return false;
        if (!int.TryParse(raw, System.Globalization.NumberStyles.HexNumber, null, out var value)) return false;
        r = (byte)((value >> 16) & 0xFF);
        g = (byte)((value >> 8) & 0xFF);
        b = (byte)(value & 0xFF);
        return true;
    }

    private void ApplyHsvToPending()
    {
        var (r, g, b) = HsvToRgb(_hue, _sat, _val);
        if (_isEditingBackground)
        {
            _pendingR = r;
            _pendingG = g;
            _pendingB = b;
            UpdatePreviewBrush();
        }
        else
        {
            Config.AccentR = r;
            Config.AccentG = g;
            Config.AccentB = b;
            var hex = $"#{r:X2}{g:X2}{b:X2}";
            if (!string.Equals(HexInput, hex, StringComparison.OrdinalIgnoreCase))
            {
                HexInput = hex;
            }
            ThemeService.Apply(Config);
            OnPropertyChanged(nameof(Config));
            OnPropertyChanged(nameof(AccentPreviewBrush));
            OnPropertyChanged(nameof(ActiveColorBrush));
        }
    }

    private void UpdateSatValFromPoint(System.Windows.Point pos)
    {
        var w = SatValBorder.ActualWidth;
        var h = SatValBorder.ActualHeight;
        if (w <= 0 || h <= 0) return;

        _sat = Math.Clamp(pos.X / w, 0, 1);
        _val = 1 - Math.Clamp(pos.Y / h, 0, 1);
        ApplyHsvToPending();
    }

    private void UpdateSatValThumb()
    {
        if (SatValCanvas == null || SatValThumb == null) return;
        var w = SatValBorder.ActualWidth;
        var h = SatValBorder.ActualHeight;
        if (w <= 0 || h <= 0) return;

        var x = _sat * w;
        var y = (1 - _val) * h;
        Canvas.SetLeft(SatValThumb, x - SatValThumb.Width / 2);
        Canvas.SetTop(SatValThumb, y - SatValThumb.Height / 2);
    }

    private void UpdateHueBrush()
    {
        var (r, g, b) = HsvToRgb(_hue, 1, 1);
        var brush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(r, g, b));
        brush.Freeze();
        HueBrush = brush;
    }

    private static void RgbToHsv(byte r, byte g, byte b, out double h, out double s, out double v)
    {
        var rf = r / 255.0;
        var gf = g / 255.0;
        var bf = b / 255.0;

        var max = Math.Max(rf, Math.Max(gf, bf));
        var min = Math.Min(rf, Math.Min(gf, bf));
        var delta = max - min;

        h = 0;
        if (delta > 0)
        {
            if (max == rf)
            {
                h = 60 * (((gf - bf) / delta) % 6);
            }
            else if (max == gf)
            {
                h = 60 * (((bf - rf) / delta) + 2);
            }
            else
            {
                h = 60 * (((rf - gf) / delta) + 4);
            }
        }
        if (h < 0) h += 360;

        s = max == 0 ? 0 : delta / max;
        v = max;
    }

    private static (byte r, byte g, byte b) HsvToRgb(double h, double s, double v)
    {
        h = Math.Clamp(h, 0, 360);
        s = Math.Clamp(s, 0, 1);
        v = Math.Clamp(v, 0, 1);

        var c = v * s;
        var x = c * (1 - Math.Abs((h / 60 % 2) - 1));
        var m = v - c;

        double rf = 0, gf = 0, bf = 0;
        if (h < 60) { rf = c; gf = x; bf = 0; }
        else if (h < 120) { rf = x; gf = c; bf = 0; }
        else if (h < 180) { rf = 0; gf = c; bf = x; }
        else if (h < 240) { rf = 0; gf = x; bf = c; }
        else if (h < 300) { rf = x; gf = 0; bf = c; }
        else { rf = c; gf = 0; bf = x; }

        var r = (byte)Math.Round((rf + m) * 255);
        var g = (byte)Math.Round((gf + m) * 255);
        var b = (byte)Math.Round((bf + m) * 255);
        return (r, g, b);
    }

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    private void NumericTextBox_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
    {
        foreach (var ch in e.Text)
        {
            if (!char.IsDigit(ch) && ch != '.')
            {
                e.Handled = true;
                return;
            }
        }
    }

    private void NumericTextBox_Pasting(object sender, DataObjectPastingEventArgs e)
    {
        if (e.DataObject.GetDataPresent(typeof(string)))
        {
            var text = (string)e.DataObject.GetData(typeof(string));
            if (!IsNumericText(text))
            {
                e.CancelCommand();
            }
        }
        else
        {
            e.CancelCommand();
        }
    }

    private void NumericTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        ClampTextBox(sender as System.Windows.Controls.TextBox);
    }

    private void ClampTextBox(System.Windows.Controls.TextBox? textBox)
    {
        if (textBox == null)
        {
            return;
        }

        if (!double.TryParse(textBox.Text, out var value))
        {
            value = 0;
        }

        var tag = textBox.Tag as string;
        double min = double.MinValue, max = double.MaxValue;
        if (!string.IsNullOrWhiteSpace(tag))
        {
            var parts = tag.Split('|');
            if (parts.Length == 2)
            {
                double.TryParse(parts[0], out min);
                double.TryParse(parts[1], out max);
            }
        }

        value = Math.Max(min, Math.Min(max, value));
        textBox.Text = value.ToString("0.##");
    }

    private bool IsNumericText(string text)
    {
        foreach (var ch in text)
        {
            if (!char.IsDigit(ch) && ch != '.')
            {
                return false;
            }
        }
        return true;
    }

    private void TextColorChanged(object sender, RoutedEventArgs e)
    {
        UpdateTextBrush();
    }
}
