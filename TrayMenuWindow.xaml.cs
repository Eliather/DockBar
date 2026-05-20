using System;
using System.Windows;
using System.Windows.Input;
using DockBar.Services;
using WinForms = System.Windows.Forms;

namespace DockBar;

public partial class TrayMenuWindow : Window
{
    private const double ScreenMargin = 8;
    private const double PopupGap = 6;
    private System.Drawing.Point _anchorPoint;
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
        Loaded += TrayMenuWindow_Loaded;
    }

    public void ShowAt(System.Drawing.Point anchorPoint)
    {
        _anchorPoint = anchorPoint;
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

        var screen = WinForms.Screen.FromPoint(_anchorPoint);
        var area = screen.WorkingArea;
        var anchor = PointFromScreen(new System.Windows.Point(_anchorPoint.X, _anchorPoint.Y));
        var areaTopLeft = PointFromScreen(new System.Windows.Point(area.Left, area.Top));
        var areaBottomRight = PointFromScreen(new System.Windows.Point(area.Right, area.Bottom));

        var workLeft = Math.Min(areaTopLeft.X, areaBottomRight.X);
        var workTop = Math.Min(areaTopLeft.Y, areaBottomRight.Y);
        var workRight = Math.Max(areaTopLeft.X, areaBottomRight.X);
        var workBottom = Math.Max(areaTopLeft.Y, areaBottomRight.Y);

        var minLeft = workLeft + ScreenMargin;
        var maxLeft = workRight - ActualWidth - ScreenMargin;
        var minTop = workTop + ScreenMargin;
        var maxTop = workBottom - ActualHeight - ScreenMargin;

        var preferLeft = (workRight - anchor.X) < (anchor.X - workLeft);
        var preferUp = (workBottom - anchor.Y) < (anchor.Y - workTop);

        var leftCandidate = preferLeft
            ? anchor.X - ActualWidth - PopupGap
            : anchor.X + PopupGap;
        var rightCandidate = preferLeft
            ? anchor.X + PopupGap
            : anchor.X - ActualWidth - PopupGap;

        var topCandidate = preferUp
            ? anchor.Y - ActualHeight - PopupGap
            : anchor.Y + PopupGap;
        var bottomCandidate = preferUp
            ? anchor.Y + PopupGap
            : anchor.Y - ActualHeight - PopupGap;

        Left = SelectAxisPosition(leftCandidate, rightCandidate, minLeft, maxLeft);
        Top = SelectAxisPosition(topCandidate, bottomCandidate, minTop, maxTop);
    }

    private static double SelectAxisPosition(double primary, double secondary, double min, double max)
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
}
