using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace DockBar;

public sealed class EdgeHotspotWindow : Window
{
    private bool _triggered;

    public event EventHandler? HotspotTriggered;

    public EdgeHotspotWindow()
    {
        Width = 6;
        Height = 100;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        AllowsTransparency = true;
        var brush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(15, 0, 0, 0));
        brush.Freeze();
        Background = brush;
        Opacity = 1.0;
        Focusable = false;
        WindowStartupLocation = WindowStartupLocation.Manual;
        SourceInitialized += (_, _) => WindowSwitcherHelper.HideFromWindowSwitchers(this);

        MouseEnter += HotspotWindow_MouseEnter;
        MouseMove += HotspotWindow_MouseMove;
        MouseLeave += HotspotWindow_MouseLeave;
    }

    public void ShowOnEdge(Rect bounds, DockSide side, double thickness)
    {
        var hotspotWidth = Math.Max(8, thickness);
        Width = hotspotWidth;
        Height = Math.Max(1, bounds.Height);
        Left = side == DockSide.Left
            ? bounds.Left
            : bounds.Right - hotspotWidth;
        Top = bounds.Top;

        _triggered = false;

        if (!IsVisible)
        {
            Show();
        }
    }

    public void HideHotspot()
    {
        _triggered = false;
        if (IsVisible)
        {
            Hide();
        }
    }

    private void HotspotWindow_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        RaiseHotspotTriggered();
    }

    private void HotspotWindow_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        RaiseHotspotTriggered();
    }

    private void HotspotWindow_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        _triggered = false;
    }

    private void RaiseHotspotTriggered()
    {
        if (_triggered)
        {
            return;
        }

        _triggered = true;
        HotspotTriggered?.Invoke(this, EventArgs.Empty);
    }
}
