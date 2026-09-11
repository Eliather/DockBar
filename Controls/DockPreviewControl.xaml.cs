using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using DockBar.Models;
using UserControl = System.Windows.Controls.UserControl;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Point = System.Windows.Point;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using MouseButtonState = System.Windows.Input.MouseButtonState;

namespace DockBar.Controls;

public partial class DockPreviewControl : UserControl
{
    private Border? _draggedWidgetBorder;
    private string? _draggedWidgetTag;
    private Point _widgetDragStartPoint;
    private bool _isWidgetDragging;

    private SettingsWindow? WindowParent => Window.GetWindow(this) as SettingsWindow;
    private DockConfig? CurrentConfig => WindowParent?.Config ?? (DataContext as SettingsWindow)?.Config;

    public DockPreviewControl()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => ApplyWidgetOrderToPreview();
    }

    private void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        ApplyWidgetOrderToPreview();
    }

    private void PreviewWidget_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border border && border.Tag is string tag)
        {
            _draggedWidgetBorder = border;
            _draggedWidgetTag = tag;
            _widgetDragStartPoint = e.GetPosition(PreviewWidgetsPanel);
            _isWidgetDragging = false;
        }
    }

    private void PreviewWidgetsPanel_MouseMove(object sender, MouseEventArgs e)
    {
        if (_draggedWidgetBorder == null || _draggedWidgetTag == null || PreviewWidgetsPanel == null)
        {
            return;
        }

        if (e.LeftButton != MouseButtonState.Pressed)
        {
            EndWidgetDrag();
            return;
        }

        var currentPoint = e.GetPosition(PreviewWidgetsPanel);
        var diffY = currentPoint.Y - _widgetDragStartPoint.Y;

        if (!_isWidgetDragging && Math.Abs(diffY) > 5)
        {
            _isWidgetDragging = true;
            PreviewWidgetsPanel.CaptureMouse();
            _draggedWidgetBorder.Opacity = 0.72;
            if (TryFindResource("AppAccentBrush") is Brush accent)
            {
                _draggedWidgetBorder.BorderBrush = accent;
            }
        }

        if (_isWidgetDragging)
        {
            var visibleBorders = PreviewWidgetsPanel.Children.OfType<Border>()
                .Where(b => b.Visibility == Visibility.Visible && b.Tag is string)
                .ToList();

            var currentIndex = visibleBorders.IndexOf(_draggedWidgetBorder);
            if (currentIndex >= 0)
            {
                if (diffY < -15 && currentIndex > 0)
                {
                    var targetBorder = visibleBorders[currentIndex - 1];
                    if (targetBorder.Tag is string targetTag)
                    {
                        SwapWidgetsInOrder(_draggedWidgetTag, targetTag);
                        _widgetDragStartPoint = currentPoint;
                    }
                }
                else if (diffY > 15 && currentIndex < visibleBorders.Count - 1)
                {
                    var targetBorder = visibleBorders[currentIndex + 1];
                    if (targetBorder.Tag is string targetTag)
                    {
                        SwapWidgetsInOrder(_draggedWidgetTag, targetTag);
                        _widgetDragStartPoint = currentPoint;
                    }
                }
            }
        }
    }

    private void PreviewWidgetsPanel_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        EndWidgetDrag();
    }

    private void EndWidgetDrag()
    {
        if (PreviewWidgetsPanel != null && PreviewWidgetsPanel.IsMouseCaptured)
        {
            PreviewWidgetsPanel.ReleaseMouseCapture();
        }

        if (_draggedWidgetBorder != null)
        {
            _draggedWidgetBorder.Opacity = 1.0;
            _draggedWidgetBorder.BorderBrush = Brushes.Transparent;
        }

        var config = CurrentConfig;
        if (_isWidgetDragging && config != null)
        {
            WindowParent?.OnApplyPreview?.Invoke(config.Clone());
        }

        _draggedWidgetBorder = null;
        _draggedWidgetTag = null;
        _isWidgetDragging = false;
    }

    private void SwapWidgetsInOrder(string tagA, string tagB)
    {
        var config = CurrentConfig;
        if (config == null) return;

        config.Experimental.WidgetOrder ??= new() { "Clock", "Media", "Volume", "Resource", "Caffeine" };
        var order = config.Experimental.WidgetOrder;
        var idxA = order.IndexOf(tagA);
        var idxB = order.IndexOf(tagB);
        if (idxA >= 0 && idxB >= 0 && idxA != idxB)
        {
            order[idxA] = tagB;
            order[idxB] = tagA;
            ApplyWidgetOrderToPreview();
            WindowParent?.OnApplyPreview?.Invoke(config.Clone());
        }
    }

    public void ApplyWidgetOrderToPreview()
    {
        if (PreviewWidgetsPanel == null) return;

        var config = CurrentConfig;
        var order = config?.Experimental?.WidgetOrder ?? new List<string> { "Clock", "Media", "Volume", "Resource", "Caffeine" };
        var borders = new Dictionary<string, Border>(StringComparer.OrdinalIgnoreCase)
        {
            ["Clock"] = PreviewClockBorder,
            ["Media"] = PreviewMediaBorder,
            ["Volume"] = PreviewVolumeBorder,
            ["Resource"] = PreviewResourceBorder,
            ["Caffeine"] = PreviewCaffeineBorder
        };

        PreviewWidgetsPanel.Children.Clear();
        foreach (var tag in order)
        {
            if (borders.TryGetValue(tag, out var border))
            {
                PreviewWidgetsPanel.Children.Add(border);
            }
        }
        foreach (var kvp in borders)
        {
            if (!PreviewWidgetsPanel.Children.Contains(kvp.Value))
            {
                PreviewWidgetsPanel.Children.Add(kvp.Value);
            }
        }

        if (_isWidgetDragging && _draggedWidgetBorder != null)
        {
            _draggedWidgetBorder.Opacity = 0.72;
            if (TryFindResource("AppAccentBrush") is Brush accent)
            {
                _draggedWidgetBorder.BorderBrush = accent;
            }
        }
    }
}
