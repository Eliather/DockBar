using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using DockBar.Services;
using WinForms = System.Windows.Forms;

namespace DockBar;

public partial class TrayMenuWindow : Window
{
    private const double ScreenMargin = 8;
    private const double PopupGap = 6;
    private System.Drawing.Rectangle _anchorBounds;
    private bool _closeRequested;
    private Action? _afterCloseAction;

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
        SourceInitialized += (_, _) => WindowSwitcherHelper.HideFromWindowSwitchers(this);
        Loaded += TrayMenuWindow_Loaded;
    }

    public void ShowAt(System.Drawing.Rectangle anchorBounds)
    {
        _anchorBounds = anchorBounds;
        Show();
        Activate();
    }

    public void RequestClose(Action? afterClose = null)
    {
        if (_closeRequested)
        {
            return;
        }

        _closeRequested = true;
        _afterCloseAction = afterClose;
        Close();
    }

    private void TrayMenuWindow_Loaded(object sender, RoutedEventArgs e)
    {
        PositionWindow();
        Opacity = 1;
    }

    private void PositionWindow()
    {
        UpdateLayout();

        var screen = WinForms.Screen.FromRectangle(_anchorBounds);
        var area = screen.WorkingArea;
        var dpi = VisualTreeHelper.GetDpi(this);
        var scaleX = dpi.DpiScaleX <= 0 ? 1.0 : dpi.DpiScaleX;
        var scaleY = dpi.DpiScaleY <= 0 ? 1.0 : dpi.DpiScaleY;
        var widthPx = Math.Max(1, (int)Math.Ceiling(ActualWidth * scaleX));
        var heightPx = Math.Max(1, (int)Math.Ceiling(ActualHeight * scaleY));

        var anchorLeft = _anchorBounds.Left;
        var anchorTop = _anchorBounds.Top;
        var anchorRight = _anchorBounds.Right;
        var anchorBottom = _anchorBounds.Bottom;
        var anchorCenterX = anchorLeft + (_anchorBounds.Width / 2.0);
        var anchorCenterY = anchorTop + (_anchorBounds.Height / 2.0);

        var workLeft = area.Left;
        var workTop = area.Top;
        var workRight = area.Right;
        var workBottom = area.Bottom;

        var minLeft = workLeft + (int)Math.Round(ScreenMargin * scaleX);
        var maxLeft = workRight - widthPx - (int)Math.Round(ScreenMargin * scaleX);
        var minTop = workTop + (int)Math.Round(ScreenMargin * scaleY);
        var maxTop = workBottom - heightPx - (int)Math.Round(ScreenMargin * scaleY);
        var gapX = (int)Math.Round(PopupGap * scaleX);
        var gapY = (int)Math.Round(PopupGap * scaleY);

        var preferLeft = (workRight - anchorCenterX) < (anchorCenterX - workLeft);
        var preferUp = (workBottom - anchorCenterY) < (anchorCenterY - workTop);

        var leftCandidate = preferLeft
            ? anchorRight - widthPx
            : anchorLeft;
        var rightCandidate = preferLeft
            ? anchorLeft
            : anchorRight - widthPx;

        var topCandidate = preferUp
            ? anchorTop - heightPx - gapY
            : anchorBottom + gapY;
        var bottomCandidate = preferUp
            ? anchorBottom + gapY
            : anchorTop - heightPx - gapY;

        var leftPx = SelectAxisPosition(leftCandidate, rightCandidate, minLeft, maxLeft);
        var topPx = SelectAxisPosition(topCandidate, bottomCandidate, minTop, maxTop);
        ApplyPixelPosition(leftPx, topPx);
    }

    private void ApplyPixelPosition(int leftPx, int topPx)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        SetWindowPos(hwnd, IntPtr.Zero, leftPx, topPx, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
    }

    private static int SelectAxisPosition(int primary, int secondary, int min, int max)
    {
        if (max < min)
        {
            return min;
        }

        if (primary >= min && primary <= max)
        {
            return primary;
        }

        if (secondary >= min && secondary <= max)
        {
            return secondary;
        }

        return Math.Max(min, Math.Min(primary, max));
    }

    private void Window_Deactivated(object sender, EventArgs e)
    {
        RequestClose();
    }

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            RequestClose();
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
        RequestClose(handler == null ? null : () => handler.Invoke(this, EventArgs.Empty));
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);

        var action = _afterCloseAction;
        _afterCloseAction = null;

        if (action != null)
        {
            Dispatcher.BeginInvoke(action);
        }
    }

    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        uint uFlags);
}
