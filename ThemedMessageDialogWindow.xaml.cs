using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using DockBar.Services;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace DockBar;

public partial class ThemedMessageDialogWindow : Window, INotifyPropertyChanged
{
    private readonly System.Windows.Threading.DispatcherTimer? _countdownTimer;
    private int _remainingSeconds;
    private readonly string? _countdownFormat;

    public string DialogTitle { get; }
    public string DialogMessage { get; }
    public string DialogGlyph { get; }
    public Brush DialogGlyphBrush { get; }

    public bool ShowOkButton { get; }
    public bool ShowYesButton { get; }
    public bool ShowNoButton { get; }
    public bool ShowCancelButton { get; }

    public bool HasCountdown => _remainingSeconds > 0;
    public int RemainingSeconds => _remainingSeconds;
    public string CountdownText => string.Format(
        _countdownFormat ?? LocalizationService.Get("Settings_ApplyCountdownText"),
        _remainingSeconds);

    public MessageBoxResult Result { get; private set; } = MessageBoxResult.None;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ThemedMessageDialogWindow(
        string message,
        string title,
        MessageBoxButton button = MessageBoxButton.OK,
        MessageBoxImage image = MessageBoxImage.None,
        int countdownSeconds = 0,
        string? countdownFormat = null)
    {
        DialogTitle = !string.IsNullOrWhiteSpace(title) ? title : "DockBar";
        DialogMessage = message ?? string.Empty;
        _remainingSeconds = countdownSeconds;
        _countdownFormat = countdownFormat;

        if (countdownSeconds > 0)
        {
            _countdownTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _countdownTimer.Tick += CountdownTimer_Tick;
            _countdownTimer.Start();
            Closed += (_, _) => _countdownTimer.Stop();
        }

        // Glyph & Color based on Image type
        switch (image)
        {
            case MessageBoxImage.Error: // or Hand, Stop
                DialogGlyph = "\uE783"; // Exclamation in octagon / Stop
                DialogGlyphBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(248, 81, 73));
                break;
            case MessageBoxImage.Warning: // or Exclamation
                DialogGlyph = "\uE7BA"; // Warning triangle
                DialogGlyphBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(240, 180, 41));
                break;
            case MessageBoxImage.Question:
                DialogGlyph = "\uE897"; // Help / Question
                DialogGlyphBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(88, 166, 255));
                break;
            case MessageBoxImage.Information: // or Asterisk
            default:
                DialogGlyph = "\uE946"; // Info bubble
                DialogGlyphBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(88, 166, 255));
                break;
        }
        DialogGlyphBrush.Freeze();

        // Configure Buttons
        switch (button)
        {
            case MessageBoxButton.OKCancel:
                ShowOkButton = true;
                ShowCancelButton = true;
                break;
            case MessageBoxButton.YesNo:
                ShowYesButton = true;
                ShowNoButton = true;
                break;
            case MessageBoxButton.YesNoCancel:
                ShowYesButton = true;
                ShowNoButton = true;
                ShowCancelButton = true;
                break;
            case MessageBoxButton.OK:
            default:
                ShowOkButton = true;
                break;
        }

        DataContext = this;
        InitializeComponent();

        SourceInitialized += (_, _) =>
        {
            WindowSwitcherHelper.HideFromWindowSwitchers(this);
            ThemeService.ApplyWindowBackdrop(this);
        };
    }

    private void CountdownTimer_Tick(object? sender, EventArgs e)
    {
        _remainingSeconds--;
        OnPropertyChanged(nameof(RemainingSeconds));
        OnPropertyChanged(nameof(CountdownText));

        if (_remainingSeconds <= 0)
        {
            _countdownTimer?.Stop();
            if (Result == MessageBoxResult.None)
            {
                Result = MessageBoxResult.No;
            }
            Close();
        }
    }

    private void Header_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
        {
            DragMove();
        }
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            _countdownTimer?.Stop();
            if (ShowCancelButton)
            {
                Result = MessageBoxResult.Cancel;
                Close();
            }
            else if (ShowNoButton)
            {
                Result = MessageBoxResult.No;
                Close();
            }
            else if (ShowOkButton)
            {
                Result = MessageBoxResult.OK;
                Close();
            }
        }
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        _countdownTimer?.Stop();
        Result = MessageBoxResult.OK;
        Close();
    }

    private void BtnYes_Click(object sender, RoutedEventArgs e)
    {
        _countdownTimer?.Stop();
        Result = MessageBoxResult.Yes;
        Close();
    }

    private void BtnNo_Click(object sender, RoutedEventArgs e)
    {
        _countdownTimer?.Stop();
        Result = MessageBoxResult.No;
        Close();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        _countdownTimer?.Stop();
        Result = MessageBoxResult.Cancel;
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        _countdownTimer?.Stop();
        if (Result == MessageBoxResult.None && HasCountdown)
        {
            Result = MessageBoxResult.No;
        }
        base.OnClosed(e);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public static class ThemedMessageBox
{
    public static MessageBoxResult Show(
        string message,
        string title = "DockBar",
        MessageBoxButton button = MessageBoxButton.OK,
        MessageBoxImage icon = MessageBoxImage.None)
    {
        return Show(null, message, title, button, icon);
    }

    public static MessageBoxResult Show(
        Window? owner,
        string message,
        string title = "DockBar",
        MessageBoxButton button = MessageBoxButton.OK,
        MessageBoxImage icon = MessageBoxImage.None)
    {
        if (System.Windows.Application.Current?.Dispatcher?.CheckAccess() == false)
        {
            return System.Windows.Application.Current.Dispatcher.Invoke(() => Show(owner, message, title, button, icon));
        }

        var dlg = new ThemedMessageDialogWindow(message, title, button, icon);
        if (owner != null && owner.IsVisible && owner.WindowState != WindowState.Minimized)
        {
            dlg.Owner = owner;
            dlg.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }
        else
        {
            dlg.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        dlg.ShowDialog();
        return dlg.Result;
    }

    public static MessageBoxResult ShowCountdown(
        Window? owner,
        string message,
        string title,
        int countdownSeconds = 5,
        string? countdownFormat = null,
        MessageBoxImage icon = MessageBoxImage.Question)
    {
        if (System.Windows.Application.Current?.Dispatcher?.CheckAccess() == false)
        {
            return System.Windows.Application.Current.Dispatcher.Invoke(() =>
                ShowCountdown(owner, message, title, countdownSeconds, countdownFormat, icon));
        }

        var dlg = new ThemedMessageDialogWindow(
            message: message,
            title: title,
            button: MessageBoxButton.YesNo,
            image: icon,
            countdownSeconds: countdownSeconds,
            countdownFormat: countdownFormat);

        if (owner != null && owner.IsVisible && owner.WindowState != WindowState.Minimized)
        {
            dlg.Owner = owner;
            dlg.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }
        else
        {
            dlg.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        dlg.ShowDialog();
        return dlg.Result;
    }
}

