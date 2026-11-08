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
using Orientation = System.Windows.Controls.Orientation;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using VerticalAlignment = System.Windows.VerticalAlignment;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using MouseWheelEventArgs = System.Windows.Input.MouseWheelEventArgs;
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
    private bool IsHorizontalPreview => CurrentConfig?.DockSide == DockSide.Top;

    public DockPreviewControl()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => ApplyWidgetOrderToPreview();
    }

    private void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        ApplyWidgetOrderToPreview();
    }

    private void PreviewWidgetsScroll_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (!IsHorizontalPreview || PreviewWidgetsScroll == null || PreviewWidgetsScroll.ScrollableWidth <= 0)
        {
            return;
        }

        var nextOffset = Math.Clamp(PreviewWidgetsScroll.HorizontalOffset - (e.Delta * 0.5), 0, PreviewWidgetsScroll.ScrollableWidth);
        PreviewWidgetsScroll.ScrollToHorizontalOffset(nextOffset);
        e.Handled = true;
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
        bool isHorizontal = IsHorizontalPreview;
        var primaryDelta = isHorizontal
            ? currentPoint.X - _widgetDragStartPoint.X
            : currentPoint.Y - _widgetDragStartPoint.Y;

        if (!_isWidgetDragging && Math.Abs(primaryDelta) > 5)
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
            if (isHorizontal && PreviewWidgetsScroll != null && PreviewWidgetsScroll.ScrollableWidth > 0)
            {
                var scrollPos = e.GetPosition(PreviewWidgetsScroll);
                if (scrollPos.X < 28)
                {
                    PreviewWidgetsScroll.ScrollToHorizontalOffset(Math.Max(0, PreviewWidgetsScroll.HorizontalOffset - 10));
                }
                else if (scrollPos.X > PreviewWidgetsScroll.ActualWidth - 28)
                {
                    PreviewWidgetsScroll.ScrollToHorizontalOffset(Math.Min(PreviewWidgetsScroll.ScrollableWidth, PreviewWidgetsScroll.HorizontalOffset + 10));
                }
            }

            var visibleBorders = PreviewWidgetsPanel.Children.OfType<Border>()
                .Where(b => b.Visibility == Visibility.Visible && b.Tag is string)
                .ToList();

            var currentIndex = visibleBorders.IndexOf(_draggedWidgetBorder);
            double threshold = isHorizontal ? 18.0 : 15.0;
            if (currentIndex >= 0)
            {
                if (primaryDelta < -threshold && currentIndex > 0)
                {
                    var targetBorder = visibleBorders[currentIndex - 1];
                    if (targetBorder.Tag is string targetTag)
                    {
                        SwapWidgetsInOrder(_draggedWidgetTag, targetTag);
                        _widgetDragStartPoint = currentPoint;
                    }
                }
                else if (primaryDelta > threshold && currentIndex < visibleBorders.Count - 1)
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

        config.Experimental.WidgetOrder ??= new() { "Clock", "Media", "Volume", "Resource", "AiQuota", "Power", "Caffeine", "Pagination" };
        var order = config.Experimental.WidgetOrder;
        var idxA = order.IndexOf(tagA);
        var idxB = order.IndexOf(tagB);
        if (idxA >= 0 && idxB >= 0 && idxA != idxB)
        {
            order[idxA] = tagB;
            order[idxB] = tagA;
            if (WindowParent != null)
            {
                WindowParent.ApplyWidgetOrderToPreview();
            }
            else
            {
                ApplyWidgetOrderToPreview();
            }
            WindowParent?.OnApplyPreview?.Invoke(config.Clone());
        }
    }

    public void ApplyWidgetOrderToPreview()
    {
        if (PreviewWidgetsPanel == null) return;

        var config = CurrentConfig;
        bool isHorizontal = config?.DockSide == DockSide.Top;

        ApplyOrientationLayout(isHorizontal);

        var order = config?.Experimental?.WidgetOrder ?? new List<string> { "Clock", "Media", "Volume", "Resource", "AiQuota", "Power", "Caffeine", "Pagination" };
        var borders = new Dictionary<string, Border>(StringComparer.OrdinalIgnoreCase)
        {
            ["Clock"] = PreviewClockBorder,
            ["Media"] = PreviewMediaBorder,
            ["Volume"] = PreviewVolumeBorder,
            ["Resource"] = PreviewResourceBorder,
            ["AiQuota"] = PreviewAiQuotaBorder,
            ["Power"] = PreviewPowerBorder,
            ["Caffeine"] = PreviewCaffeineBorder,
            ["Pagination"] = PreviewPaginationBorder
        };

        PreviewWidgetsPanel.Children.Clear();
        foreach (var tag in order)
        {
            if (borders.TryGetValue(tag, out var border))
            {
                ConfigureWidgetBorderForOrientation(border, isHorizontal);
                PreviewWidgetsPanel.Children.Add(border);
            }
        }
        foreach (var kvp in borders)
        {
            if (!PreviewWidgetsPanel.Children.Contains(kvp.Value))
            {
                ConfigureWidgetBorderForOrientation(kvp.Value, isHorizontal);
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

    private static void ConfigureWidgetBorderForOrientation(Border border, bool isHorizontal)
    {
        if (isHorizontal)
        {
            border.Height = 96;
            border.Margin = new Thickness(0, 0, 5, 0);
            border.HorizontalAlignment = HorizontalAlignment.Center;
            border.VerticalAlignment = VerticalAlignment.Center;
        }
        else
        {
            border.Height = double.NaN;
            border.Margin = new Thickness(0, 0, 0, 6);
            border.HorizontalAlignment = HorizontalAlignment.Stretch;
            border.VerticalAlignment = VerticalAlignment.Center;
        }
    }

    private void ApplyOrientationLayout(bool isHorizontal)
    {
        if (PreviewDockFrame == null || PreviewDockLayoutGrid == null)
        {
            return;
        }

        PreviewDockLayoutGrid.RowDefinitions.Clear();
        PreviewDockLayoutGrid.ColumnDefinitions.Clear();

        if (isHorizontal)
        {
            PreviewDockFrame.Width = double.NaN;
            PreviewDockFrame.Height = double.NaN;
            PreviewDockFrame.MinHeight = 108;
            PreviewDockFrame.Padding = new Thickness(10, 5, 10, 5);
            PreviewDockFrame.HorizontalAlignment = HorizontalAlignment.Stretch;
            PreviewDockFrame.VerticalAlignment = VerticalAlignment.Center;

            PreviewDockLayoutGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            PreviewDockLayoutGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            PreviewDockLayoutGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            PreviewDockLayoutGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            PreviewDockLayoutGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            if (PreviewAppStack != null)
            {
                Grid.SetRow(PreviewAppStack, 0);
                Grid.SetColumn(PreviewAppStack, 0);
                PreviewAppStack.Margin = new Thickness(2, 0, 8, 0);
                PreviewAppStack.VerticalAlignment = VerticalAlignment.Center;
            }

            if (PreviewFreeSpaceBorder != null)
            {
                Grid.SetRow(PreviewFreeSpaceBorder, 0);
                Grid.SetColumn(PreviewFreeSpaceBorder, 1);
                PreviewFreeSpaceBorder.Width = 50;
                PreviewFreeSpaceBorder.MinHeight = 64;
                PreviewFreeSpaceBorder.Margin = new Thickness(0, 6, 8, 6);
                PreviewFreeSpaceBorder.VerticalAlignment = VerticalAlignment.Stretch;
            }

            if (PreviewFreeSpaceText != null)
            {
                PreviewFreeSpaceText.Text = "[ Libre ]";
            }

            if (PreviewWidgetsScroll != null)
            {
                Grid.SetRow(PreviewWidgetsScroll, 0);
                Grid.SetColumn(PreviewWidgetsScroll, 2);
                PreviewWidgetsScroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
                PreviewWidgetsScroll.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
                PreviewWidgetsScroll.HorizontalAlignment = HorizontalAlignment.Right;
                PreviewWidgetsScroll.VerticalAlignment = VerticalAlignment.Center;
            }

            if (PreviewWidgetsPanel != null)
            {
                PreviewWidgetsPanel.Orientation = Orientation.Horizontal;
                PreviewWidgetsPanel.HorizontalAlignment = HorizontalAlignment.Right;
                PreviewWidgetsPanel.VerticalAlignment = VerticalAlignment.Center;
                PreviewWidgetsPanel.Margin = new Thickness(0, 0, 2, 2);
            }

            if (PreviewActionButtonsStack != null)
            {
                Grid.SetRow(PreviewActionButtonsStack, 0);
                Grid.SetColumn(PreviewActionButtonsStack, 3);
                PreviewActionButtonsStack.Orientation = Orientation.Vertical;
                PreviewActionButtonsStack.Margin = new Thickness(6, 0, 2, 0);
            }
        }
        else
        {
            PreviewDockFrame.Width = 175;
            PreviewDockFrame.Height = double.NaN;
            PreviewDockFrame.MinHeight = 380;
            PreviewDockFrame.Padding = new Thickness(10);
            PreviewDockFrame.HorizontalAlignment = HorizontalAlignment.Center;
            PreviewDockFrame.VerticalAlignment = VerticalAlignment.Center;

            PreviewDockLayoutGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            PreviewDockLayoutGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            PreviewDockLayoutGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            PreviewDockLayoutGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            if (PreviewAppStack != null)
            {
                Grid.SetRow(PreviewAppStack, 0);
                Grid.SetColumn(PreviewAppStack, 0);
                PreviewAppStack.Margin = new Thickness(0, 4, 0, 0);
                PreviewAppStack.VerticalAlignment = VerticalAlignment.Center;
            }

            if (PreviewFreeSpaceBorder != null)
            {
                Grid.SetRow(PreviewFreeSpaceBorder, 1);
                Grid.SetColumn(PreviewFreeSpaceBorder, 0);
                PreviewFreeSpaceBorder.Width = double.NaN;
                PreviewFreeSpaceBorder.MinHeight = 32;
                PreviewFreeSpaceBorder.Margin = new Thickness(8, 8, 8, 8);
                PreviewFreeSpaceBorder.VerticalAlignment = VerticalAlignment.Stretch;
            }

            if (PreviewFreeSpaceText != null)
            {
                PreviewFreeSpaceText.Text = "[ Espacio libre ]";
            }

            if (PreviewWidgetsScroll != null)
            {
                Grid.SetRow(PreviewWidgetsScroll, 2);
                Grid.SetColumn(PreviewWidgetsScroll, 0);
                PreviewWidgetsScroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
                PreviewWidgetsScroll.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
                PreviewWidgetsScroll.HorizontalAlignment = HorizontalAlignment.Stretch;
                PreviewWidgetsScroll.VerticalAlignment = VerticalAlignment.Center;
            }

            if (PreviewWidgetsPanel != null)
            {
                PreviewWidgetsPanel.Orientation = Orientation.Vertical;
                PreviewWidgetsPanel.HorizontalAlignment = HorizontalAlignment.Stretch;
                PreviewWidgetsPanel.VerticalAlignment = VerticalAlignment.Center;
                PreviewWidgetsPanel.Margin = new Thickness(0, 0, 0, 6);
            }

            if (PreviewActionButtonsStack != null)
            {
                Grid.SetRow(PreviewActionButtonsStack, 3);
                Grid.SetColumn(PreviewActionButtonsStack, 0);
                PreviewActionButtonsStack.Orientation = Orientation.Horizontal;
                PreviewActionButtonsStack.Margin = new Thickness(0, 4, 0, 2);
            }
        }
    }
}
