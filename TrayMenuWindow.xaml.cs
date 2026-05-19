using System;
using System.Windows;
using System.Windows.Input;
using DockBar.Services;
using WinForms = System.Windows.Forms;

namespace DockBar;

public partial class TrayMenuWindow : Window
{
    private const double ScreenMargin = 8;
    private System.Drawing.Point _anchorPoint;

    public string VersionTag { get; }

    public event EventHandler? OpenRequested;
    public event EventHandler? ToggleSideRequested;
    public event EventHandler? SettingsRequested;
    public event EventHandler? UpdateRequested;
    public event EventHandler? ConfigFolderRequested;
    public event EventHandler? ExitRequested;

    public TrayMenuWindow()
    {
        VersionTag = $"v{UpdateService.GetCurrentVersion()}";
        InitializeComponent();
        DataContext = this;
        Loaded += TrayMenuWindow_Loaded;
    }

    public void ShowAt(System.Drawing.Point anchorPoint)
    {
        _anchorPoint = anchorPoint;
        Show();
        Activate();
    }

    private void TrayMenuWindow_Loaded(object sender, RoutedEventArgs e)
    {
        PositionWindow();
        Opacity = 1;
    }

    private void PositionWindow()
    {
        UpdateLayout();

        var screen = WinForms.Screen.FromPoint(_anchorPoint);
        var area = screen.WorkingArea;

        var desiredLeft = _anchorPoint.X - ActualWidth + 6;
        var desiredTop = _anchorPoint.Y - ActualHeight - 6;

        Left = Math.Max(area.Left + ScreenMargin, Math.Min(desiredLeft, area.Right - ActualWidth - ScreenMargin));
        Top = Math.Max(area.Top + ScreenMargin, Math.Min(desiredTop, area.Bottom - ActualHeight - ScreenMargin));
    }

    private void Window_Deactivated(object sender, EventArgs e)
    {
        Close();
    }

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
        }
    }

    private void Open_Click(object sender, RoutedEventArgs e)
    {
        CloseAndRaise(OpenRequested);
    }

    private void ToggleSide_Click(object sender, RoutedEventArgs e)
    {
        CloseAndRaise(ToggleSideRequested);
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        CloseAndRaise(SettingsRequested);
    }

    private void Update_Click(object sender, RoutedEventArgs e)
    {
        CloseAndRaise(UpdateRequested);
    }

    private void ConfigFolder_Click(object sender, RoutedEventArgs e)
    {
        CloseAndRaise(ConfigFolderRequested);
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        CloseAndRaise(ExitRequested);
    }

    private void CloseAndRaise(EventHandler? handler)
    {
        Close();
        handler?.Invoke(this, EventArgs.Empty);
    }
}
