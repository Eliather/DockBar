using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using DockBar.Models;
using DockBar.Services;
using Win32 = Microsoft.Win32;

namespace DockBar;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private readonly DispatcherTimer _hideTimer;
    private DockConfig _config = new();
    private bool _isHidden;
    private DockSide _dockSide = DockSide.Left;
    private const double EdgeRevealPx = 2;
    private bool _isAnimating;
    private double _iconSize = 28;
    private SolidColorBrush _dockBackgroundBrush = new(System.Windows.Media.Color.FromRgb(16, 16, 16));
    private SolidColorBrush _dockTextBrush = new(System.Windows.Media.Color.FromRgb(242, 242, 242));
    private bool _isEditMode;
    private System.Windows.Point _dragStartPoint;
    private ShortcutItem? _draggingItem;
    private string? _dragHoverPath;
    private int _dropInsertIndex = -1;
    private double _preEditWidth;
    private bool _fullscreenActive;
    private IntPtr _winEventHookForeground = IntPtr.Zero;
    private IntPtr _winEventHookLocation = IntPtr.Zero;
    private WinEventDelegate? _winEventDelegate;
    private bool _fullscreenCheckPending;

    public ObservableCollection<ShortcutItem> Shortcuts { get; } = new();
    public ObservableCollection<ShortcutItem> VisibleShortcuts { get; } = new();
    private int _itemsPerPage = 6;
    private int _currentPage;

    public double IconSize
    {
        get => _iconSize;
        set
        {
            if (Math.Abs(_iconSize - value) > double.Epsilon)
            {
                _iconSize = value;
                OnPropertyChanged();
            }
        }
    }

    public bool HasMultiplePages => Shortcuts.Count > _itemsPerPage;

    public Visibility PaginationVisibility => !IsEditMode && HasMultiplePages ? Visibility.Visible : Visibility.Collapsed;

    public SolidColorBrush DockBackgroundBrush
    {
        get => _dockBackgroundBrush;
        private set
        {
            if (_dockBackgroundBrush != value)
            {
                _dockBackgroundBrush = value;
                OnPropertyChanged();
            }
        }
    }

    public SolidColorBrush DockTextBrush
    {
        get => _dockTextBrush;
        private set
        {
            if (_dockTextBrush != value)
            {
                _dockTextBrush = value;
                OnPropertyChanged();
            }
        }
    }

    public string? DragHoverPath
    {
        get => _dragHoverPath;
        set
        {
            if (_dragHoverPath != value)
            {
                _dragHoverPath = value;
                OnPropertyChanged();
            }
        }
    }

    public int DropInsertIndex
    {
        get => _dropInsertIndex;
        set
        {
            if (_dropInsertIndex != value)
            {
                _dropInsertIndex = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsEditMode
    {
        get => _isEditMode;
        set
        {
            if (_isEditMode != value)
            {
                _isEditMode = value;
                if (_isEditMode)
                {
                    StopHideTimer();
                    _preEditWidth = _config.DockWidth <= 0 ? Width : _config.DockWidth;
                    Width = Math.Max(350, _preEditWidth);
                    AlignDock(true);
                    ShowDockAnimated();
                }
                else
                {
                    Width = Math.Max(_config.DockWidth, 175);
                    AlignDock(!_isHidden);
                    StartHideTimer();
                }
                OnPropertyChanged();
                RefreshModeUI();
                OnPropertyChanged(nameof(PaginationVisibility));
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
        SourceInitialized += MainWindow_SourceInitialized;

        _hideTimer = new DispatcherTimer();
        _hideTimer.Tick += HideTimer_Tick;

        LoadConfigAndShortcuts();
        Shortcuts.CollectionChanged += Shortcuts_CollectionChanged;
        UpdateVisibleItems();
    }

    private void HideTimer_Tick(object? sender, EventArgs e)
    {
        _hideTimer.Stop();
        HideDockAnimated();
    }

    private void LoadConfigAndShortcuts()
    {
        var loaded = ConfigService.LoadConfig(out var createdDefault, out var hadError);
        _config = loaded;
        if (createdDefault)
        {
            var message = hadError
                ? LocalizationService.Get("Config_ReadError")
                : LocalizationService.Get("Config_NotFound");
            System.Windows.MessageBox.Show(message, "DockBar", MessageBoxButton.OK, MessageBoxImage.Warning);
            ConfigService.SaveConfig(_config);
        }
        _dockSide = _config.DockSide;

        ApplyVisualConfig();

        Shortcuts.Clear();
        _config.Shortcuts ??= new();
        foreach (var item in _config.Shortcuts)
        {
            item.Icon = ResolveIcon(item.Path, item.Icon);
            Shortcuts.Add(item);
        }
        UpdateVisibleItems();

        HandleAutoStartPrompt();
        AutoStartService.Apply(_config.AutoStartEnabled);
    }

    private void HandleAutoStartPrompt()
    {
        if (_config.AutoStartPrompted)
        {
            return;
        }

        var result = System.Windows.MessageBox.Show(
            LocalizationService.Get("AutoStart_Prompt"),
            "DockBar",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        _config.AutoStartEnabled = result == MessageBoxResult.Yes;
        _config.AutoStartPrompted = true;
        ConfigService.SaveConfig(_config);
    }

    private void ApplyVisualConfig()
    {
        IconSize = _config.IconSize;
        Width = Math.Max(_config.DockWidth, 175);
        UpdateBackgroundBrush();
        UpdateTextBrush();
        UpdateHideTimerInterval();
        UpdateItemsPerPage();
        AlignDock(!_isHidden);
        UpdateVisibleItems();
        ApplyGlassEffect();
    }

    private void UpdateHideTimerInterval()
    {
        var seconds = _config.AutoHideDelaySeconds;
        if (seconds < 0) seconds = 0;
        _hideTimer.Interval = TimeSpan.FromSeconds(seconds);
    }

    private void UpdateBackgroundBrush()
    {
        var color = System.Windows.Media.Color.FromRgb(_config.BackgroundR, _config.BackgroundG, _config.BackgroundB);
        var opacity = _config.UseTransparency
            ? Math.Clamp(_config.BackgroundOpacity, 0.2, 1.0)
            : 1.0;
        var alpha = (byte)Math.Clamp((int)Math.Round(opacity * 255), 30, 255);
        var brush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(alpha, color.R, color.G, color.B));
        brush.Freeze();
        DockBackgroundBrush = brush;
    }
    private void UpdateTextBrush()
    {
        var color = _config.UseLightText
            ? System.Windows.Media.Color.FromRgb(242, 242, 242)
            : System.Windows.Media.Color.FromRgb(10, 10, 10);
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        DockTextBrush = brush;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        AlignDock(true);
        HookForegroundWatcher();
        ApplyGlassEffect();
    }

    private void Window_Activated(object? sender, EventArgs e)
    {
        if (!_fullscreenActive)
        {
            EnsureTopmost();
        }
    }

    private void MainWindow_SourceInitialized(object? sender, EventArgs e)
    {
        HideFromWindowSwitchers();
    }

    private void AlignDock(bool showState)
    {
        var area = GetMonitorBounds();
        Top = area.Top;
        Height = area.Height;
        Left = showState ? GetShownLeft(area) : GetHiddenLeft(area);
    }

    private double GetShownLeft(Rect area)
    {
        return _dockSide == DockSide.Left
            ? area.Left
            : area.Right - Width;
    }

    private double GetHiddenLeft(Rect area)
    {
        return _dockSide == DockSide.Left
            ? area.Left - (Width - EdgeRevealPx)
            : area.Right - EdgeRevealPx;
    }

    private Rect GetMonitorBounds()
    {
        try
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            var monitor = NativeMethods.MonitorFromWindow(hwnd, NativeMethods.MONITOR_DEFAULTTONEAREST);
            var mi = new NativeMethods.MONITORINFO { cbSize = Marshal.SizeOf(typeof(NativeMethods.MONITORINFO)) };
            if (NativeMethods.GetMonitorInfo(monitor, ref mi))
            {
                var width = mi.rcMonitor.Right - mi.rcMonitor.Left;
                var height = mi.rcMonitor.Bottom - mi.rcMonitor.Top;
                return new Rect(mi.rcMonitor.Left, mi.rcMonitor.Top, width, height);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }

        return new Rect(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop, SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
    }

    private void Window_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        StopHideTimer();
        ShowDockAnimated();
    }

    private void Window_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_isHidden)
        {
            StopHideTimer();
            ShowDockAnimated();
        }
    }

    private void Window_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (IsEditMode)
        {
            return;
        }
        StartHideTimer();
    }

    private void StartHideTimer()
    {
        if (IsEditMode)
        {
            return;
        }

        _hideTimer.Stop();
        if (_config.AutoHideDelaySeconds <= 0)
        {
            HideDockAnimated();
            return;
        }

        UpdateHideTimerInterval();
        _hideTimer.Start();
    }

    private void StopHideTimer()
    {
        _hideTimer.Stop();
    }

    private void ShowDockAnimated()
    {
        if (!_isHidden && !_isAnimating)
        {
            return;
        }

        _isHidden = false;
        AnimateLeft(Left, GetShownLeft(SystemParameters.WorkArea));
    }

    private void HideDockAnimated()
    {
        if (IsEditMode)
        {
            return;
        }

        if (_isHidden && !_isAnimating)
        {
            return;
        }

        _isHidden = true;
        AnimateLeft(Left, GetHiddenLeft(SystemParameters.WorkArea));
    }

    private void AnimateLeft(double from, double to)
    {
        _isAnimating = true;
        var durationMs = _config.HideAnimationMs <= 0 ? 200 : _config.HideAnimationMs;
        var animation = new DoubleAnimation
        {
            From = from,
            To = to,
            Duration = TimeSpan.FromMilliseconds(durationMs),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        animation.Completed += (_, _) =>
        {
            _isAnimating = false;
            AlignDock(!_isHidden);
        };

        BeginAnimation(Window.LeftProperty, animation, HandoffBehavior.SnapshotAndReplace);
    }

    private void Window_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
        {
            return;
        }

        var files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);
        foreach (var file in files)
        {
            AddShortcut(file);
        }
        SaveConfig();
    }

    private void ShortcutItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(null);
    }

    private void ShortcutList_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!IsEditMode)
        {
            return;
        }

        if (e.LeftButton == MouseButtonState.Pressed)
        {
            var pos = e.GetPosition(null);
            var diff = _dragStartPoint - pos;

            if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
            {
                var element = e.OriginalSource as DependencyObject;
                if (element == null)
                {
                    return;
                }

                var data = (element as FrameworkElement)?.DataContext as ShortcutItem
                           ?? FindAncestor<FrameworkElement>(element)?.DataContext as ShortcutItem;
                if (data != null)
                {
                    _draggingItem = data;
                    System.Windows.DragDrop.DoDragDrop((DependencyObject)sender, data, System.Windows.DragDropEffects.Move);
                    _draggingItem = null;
                    DragHoverPath = null;
                    DropInsertIndex = -1;
                }
            }
        }
    }

    private void ShortcutList_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (!IsEditMode)
        {
            return;
        }

        if (!e.Data.GetDataPresent(typeof(ShortcutItem)))
        {
            return;
        }

        var droppedData = (ShortcutItem)e.Data.GetData(typeof(ShortcutItem));
        var sourceElement = e.OriginalSource as DependencyObject;
        var targetElement = sourceElement != null ? FindAncestor<FrameworkElement>(sourceElement) : null;
        var targetData = targetElement?.DataContext as ShortcutItem;
        var oldIndex = Shortcuts.IndexOf(droppedData);
        var newIndex = -1;

        if (targetElement != null && targetData != null)
        {
            var targetIndex = Shortcuts.IndexOf(targetData);
            var relativePos = e.GetPosition(targetElement);
            newIndex = relativePos.Y < (targetElement.ActualHeight / 2) ? targetIndex : targetIndex + 1;
        }
        else
        {
            newIndex = Shortcuts.Count;
        }

        if (oldIndex < 0 || newIndex < 0)
        {
            return;
        }

        if (oldIndex < newIndex)
        {
            newIndex--;
        }

        if (newIndex < 0) newIndex = 0;
        if (newIndex > Shortcuts.Count - 1) newIndex = Shortcuts.Count - 1;

        if (oldIndex != newIndex)
        {
            Shortcuts.Move(oldIndex, newIndex);
        }
        SaveConfig();
        DragHoverPath = null;
        DropInsertIndex = -1;
    }

    private void ShortcutList_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        if (!IsEditMode || !e.Data.GetDataPresent(typeof(ShortcutItem)))
        {
            return;
        }

        e.Effects = System.Windows.DragDropEffects.Move;
        var targetElement = FindAncestor<FrameworkElement>((DependencyObject)e.OriginalSource);
        if (targetElement?.DataContext is ShortcutItem targetItem)
        {
            var targetIndex = Shortcuts.IndexOf(targetItem);
            var pos = e.GetPosition(targetElement);
            var insertIndex = pos.Y < targetElement.ActualHeight / 2 ? targetIndex : targetIndex + 1;
            DropInsertIndex = insertIndex;
            DragHoverPath = targetItem.Path;
        }
        else
        {
            DropInsertIndex = Shortcuts.Count;
            DragHoverPath = null;
        }
    }

    private static T? FindAncestor<T>(DependencyObject current) where T : DependencyObject
    {
        while (current != null)
        {
            if (current is T target)
            {
                return target;
            }
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }

    private void AddShortcut(string path, string? displayName = null, ImageSource? iconOverride = null)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var isFileOrDir = File.Exists(path) || Directory.Exists(path);
        if (!isFileOrDir)
        {
            if (!(Uri.TryCreate(path, UriKind.Absolute, out var uri) && !uri.IsFile && !string.IsNullOrWhiteSpace(uri.Scheme)))
            {
                return;
            }
        }

        if (Shortcuts.Any(s => string.Equals(s.Path, path, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        string? friendly = null;
        ImageSource? shellIcon = null;

        if (!isFileOrDir && path.StartsWith("shell:AppsFolder", StringComparison.OrdinalIgnoreCase))
        {
            var info = ShellItemService.GetShellItemInfo(path, 256);
            friendly = info.displayName;
            shellIcon = info.icon;
        }

        var name = !string.IsNullOrWhiteSpace(displayName)
            ? displayName.Trim()
            : !string.IsNullOrWhiteSpace(friendly)
                ? friendly
                : isFileOrDir
                    ? (Directory.Exists(path) ? new DirectoryInfo(path).Name : Path.GetFileNameWithoutExtension(path))
                    : (path.StartsWith("shell:", StringComparison.OrdinalIgnoreCase) ? path : new Uri(path).Host);

        var item = new ShortcutItem
        {
            Name = string.IsNullOrEmpty(name) ? path : name,
            Path = path,
            Icon = iconOverride
                   ?? shellIcon
                   ?? (isFileOrDir ? IconService.GetIcon(path, (int)Math.Max(_config.IconSize * 4, 256)) : null)
        };

        Shortcuts.Add(item);
        SaveConfig();
    }

    private void Shortcut_Click(object sender, RoutedEventArgs e)
    {
        if (IsEditMode)
        {
            return;
        }

        if (sender is not System.Windows.Controls.Button button || button.Tag is not string path)
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button button || button.Tag is not string path)
        {
            return;
        }

        var item = Shortcuts.FirstOrDefault(s => string.Equals(s.Path, path, StringComparison.OrdinalIgnoreCase));
        if (item != null)
        {
            Shortcuts.Remove(item);
            SaveConfig();
        }
    }

    private void Rename_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button button || button.Tag is not string path)
        {
            return;
        }

        var item = Shortcuts.FirstOrDefault(s => string.Equals(s.Path, path, StringComparison.OrdinalIgnoreCase));
        if (item == null)
        {
            return;
        }

        var renameWindow = new RenameWindow(item.Name, DockBackgroundBrush, DockTextBrush)
        {
            Owner = this
        };

        if (renameWindow.ShowDialog() == true)
        {
            var input = renameWindow.NewName;
            if (!string.IsNullOrWhiteSpace(input))
            {
                item.Name = input.Trim();
                SaveConfig();
                OnPropertyChanged(nameof(Shortcuts));
            }
        }
    }

    private void ChangeIcon_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button button || button.Tag is not string path)
        {
            return;
        }

        var dialog = new Win32.OpenFileDialog
        {
            Title = LocalizationService.Get("Dialog_SelectIconTitle"),
            Filter = LocalizationService.Get("Dialog_SelectIconFilter"),
            Multiselect = false
        };

        if (dialog.ShowDialog() == true)
        {
            var target = dialog.FileName;
            var item = Shortcuts.FirstOrDefault(s => string.Equals(s.Path, path, StringComparison.OrdinalIgnoreCase));
            if (item != null)
            {
                item.Icon = IconService.GetIcon(target, (int)Math.Max(_config.IconSize * 4, 256));
                SaveConfig();
            }
        }
    }

    private void AddShortcutMenu_Click(object sender, RoutedEventArgs e)
    {
        var menu = new System.Windows.Controls.ContextMenu
        {
            PlacementTarget = sender as System.Windows.Controls.Button,
            Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom
        };

        var fileItem = new System.Windows.Controls.MenuItem { Header = LocalizationService.Get("AddMenu_File") };
        fileItem.Click += (_, _) => AddFileShortcut();
        var storeItem = new System.Windows.Controls.MenuItem { Header = LocalizationService.Get("AddMenu_Store") };
        storeItem.Click += (_, _) => AddStoreAppFlow();
        var uriItem = new System.Windows.Controls.MenuItem { Header = LocalizationService.Get("AddMenu_Uri") };
        uriItem.Click += (_, _) => AddUriShortcut();

        menu.Items.Add(fileItem);
        menu.Items.Add(storeItem);
        menu.Items.Add(uriItem);
        menu.IsOpen = true;
    }

    private void AddFileShortcut()
    {
        var dialog = new Win32.OpenFileDialog
        {
            Title = LocalizationService.Get("Dialog_SelectShortcutTitle"),
            Filter = LocalizationService.Get("Dialog_SelectShortcutFilter"),
            Multiselect = true
        };

        if (dialog.ShowDialog() == true)
        {
            foreach (var file in dialog.FileNames)
            {
                AddShortcut(file);
            }
        }
    }

    private void AddUriShortcut()
    {
        var addLink = new AddLinkWindow(DockBackgroundBrush, DockTextBrush)
        {
            Owner = this
        };
        if (addLink.ShowDialog() == true)
        {
            AddShortcut(addLink.Target, addLink.DisplayName);
        }
    }

    private void AddStoreAppFlow()
    {
        var picker = new StoreAppPickerWindow(DockBackgroundBrush, DockTextBrush)
        {
            Owner = this
        };

        if (picker.ShowDialog() == true && picker.SelectedApp != null)
        {
            var app = picker.SelectedApp;
            var appId = !string.IsNullOrWhiteSpace(app.AppId) ? app.AppId : $"{app.PackageFamilyName}!App";
            var path = $"shell:AppsFolder\\{appId}";
            var name = !string.IsNullOrWhiteSpace(app.FriendlyName)
                ? app.FriendlyName
                : (!string.IsNullOrWhiteSpace(app.Name) ? app.Name : app.AppId);
            AddShortcut(path, name, app.Icon);
        }
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        OpenSettings();
    }

    private void AddStoreApp_Click(object sender, RoutedEventArgs e)
    {
        var picker = new StoreAppPickerWindow(DockBackgroundBrush, DockTextBrush)
        {
            Owner = this
        };

        if (picker.ShowDialog() == true && picker.SelectedApp != null)
        {
            var app = picker.SelectedApp;
            var path = $"shell:AppsFolder\\{app.PackageFamilyName}!App";
            var (friendlyName, icon) = ShellItemService.GetShellItemInfo(path, 256);
            var name = !string.IsNullOrWhiteSpace(friendlyName)
                ? friendlyName
                : (string.IsNullOrWhiteSpace(app.Name) ? app.PackageFamilyName : app.Name);
            AddShortcut(path, name, icon);
        }
    }

    private void ToggleEdit_Click(object sender, RoutedEventArgs e)
    {
        IsEditMode = !IsEditMode;
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        System.Windows.Application.Current.Shutdown();
    }

    public void ToggleDockSide()
    {
        _dockSide = _dockSide == DockSide.Left ? DockSide.Right : DockSide.Left;
        SaveConfig();
        AlignDock(!_isHidden);
    }

    public void OpenSettings()
    {
        var draft = CloneConfig(_config);
        var settings = new SettingsWindow(draft)
        {
            Owner = this
        };

        if (settings.ShowDialog() == true)
        {
            ApplyAndSaveConfig(draft);
        }
    }

    private static DockConfig CloneConfig(DockConfig source)
    {
        var shortcuts = source.Shortcuts ?? new();
        return new DockConfig
        {
            DockSide = source.DockSide,
            DockWidth = source.DockWidth,
            IconSize = source.IconSize,
            AutoHideDelaySeconds = source.AutoHideDelaySeconds,
            HideAnimationMs = source.HideAnimationMs,
            UseTransparency = source.UseTransparency,
            BackgroundOpacity = source.BackgroundOpacity,
            BackgroundR = source.BackgroundR,
            BackgroundG = source.BackgroundG,
            BackgroundB = source.BackgroundB,
            UseLightText = source.UseLightText,
            AutoStartEnabled = source.AutoStartEnabled,
            AutoStartPrompted = source.AutoStartPrompted,
            Shortcuts = shortcuts.Select(s => new ShortcutItem
            {
                Name = s.Name,
                Path = s.Path
            }).ToList()
        };
    }

    public void ApplyAndSaveConfig(DockConfig updatedConfig)
    {
        var prevAutoStart = _config.AutoStartEnabled;
        _config = updatedConfig;
        _dockSide = updatedConfig.DockSide;
        if (!_config.AutoStartPrompted && prevAutoStart != _config.AutoStartEnabled)
        {
            _config.AutoStartPrompted = true;
        }
        ApplyVisualConfig();
        SaveConfig();
        AutoStartService.Apply(_config.AutoStartEnabled);
    }

    public void ReloadConfigAndApply()
    {
        var cfg = ConfigService.LoadConfig(out _, out _);
        ApplyAndSaveConfig(cfg);
    }

    private void SaveConfig()
    {
        _config.DockSide = _dockSide;
        _config.DockWidth = IsEditMode ? Math.Max(_preEditWidth, 175) : Math.Max(Width, 175);
        _config.IconSize = IconSize;
        _config.UseLightText = _config.UseLightText;
        _config.Shortcuts = Shortcuts.ToList();
        ConfigService.SaveConfig(_config);
        UpdateVisibleItems();
    }

    private ImageSource? ResolveIcon(string path, ImageSource? existing = null)
    {
        if (existing != null)
        {
            return existing;
        }

        if (path.StartsWith("shell:AppsFolder", StringComparison.OrdinalIgnoreCase))
        {
            var (_, icon) = ShellItemService.GetShellItemInfo(path, 256);
            if (icon != null) return icon;
        }

        var isFileOrDir = File.Exists(path) || Directory.Exists(path);
        if (isFileOrDir)
        {
            return IconService.GetIcon(path, (int)Math.Max(_config.IconSize * 4, 256));
        }

        return null;
    }

    private void HookForegroundWatcher()
    {
        _winEventDelegate = new WinEventDelegate(WinEventProc);
        _winEventHookForeground = NativeMethods.SetWinEventHook(
            NativeMethods.EVENT_SYSTEM_FOREGROUND,
            NativeMethods.EVENT_SYSTEM_FOREGROUND,
            IntPtr.Zero,
            _winEventDelegate,
            0,
            0,
            NativeMethods.WINEVENT_OUTOFCONTEXT);
        _winEventHookLocation = NativeMethods.SetWinEventHook(
            NativeMethods.EVENT_OBJECT_LOCATIONCHANGE,
            NativeMethods.EVENT_OBJECT_LOCATIONCHANGE,
            IntPtr.Zero,
            _winEventDelegate,
            0,
            0,
            NativeMethods.WINEVENT_OUTOFCONTEXT);
    }

    private void UnhookForegroundWatcher()
    {
        if (_winEventHookForeground != IntPtr.Zero)
        {
            NativeMethods.UnhookWinEvent(_winEventHookForeground);
            _winEventHookForeground = IntPtr.Zero;
        }
        if (_winEventHookLocation != IntPtr.Zero)
        {
            NativeMethods.UnhookWinEvent(_winEventHookLocation);
            _winEventHookLocation = IntPtr.Zero;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        UnhookForegroundWatcher();
        base.OnClosed(e);
    }

    private void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        if (eventType == NativeMethods.EVENT_OBJECT_LOCATIONCHANGE)
        {
            if (idObject != NativeMethods.OBJID_WINDOW)
            {
                return;
            }

            var foreground = NativeMethods.GetForegroundWindow();
            if (foreground == IntPtr.Zero || hwnd != foreground)
            {
                return;
            }
        }

        ScheduleFullscreenCheck();
    }

    private void ScheduleFullscreenCheck()
    {
        if (_fullscreenCheckPending)
        {
            return;
        }

        _fullscreenCheckPending = true;
        Dispatcher.BeginInvoke(new Action(() =>
        {
            _fullscreenCheckPending = false;
            UpdateFullscreenState();
        }), DispatcherPriority.Background);
    }

    private void UpdateFullscreenState()
    {
        try
        {
            var foreground = NativeMethods.GetForegroundWindow();
            if (foreground == IntPtr.Zero)
            {
                SetFullscreen(false);
                return;
            }

            if (IsIgnoredForeground(foreground))
            {
                SetFullscreen(false);
                return;
            }

            if (!TryGetWindowBounds(foreground, out var rect))
            {
                SetFullscreen(false);
                return;
            }

            var monitor = NativeMethods.MonitorFromWindow(foreground, NativeMethods.MONITOR_DEFAULTTONEAREST);
            var mi = new NativeMethods.MONITORINFO { cbSize = Marshal.SizeOf(typeof(NativeMethods.MONITORINFO)) };
            if (!NativeMethods.GetMonitorInfo(monitor, ref mi))
            {
                SetFullscreen(false);
                return;
            }

            const int tolerance = 8;
            var fullscreen =
                Math.Abs(rect.Left - mi.rcMonitor.Left) <= tolerance &&
                Math.Abs(rect.Top - mi.rcMonitor.Top) <= tolerance &&
                Math.Abs(rect.Right - mi.rcMonitor.Right) <= tolerance &&
                Math.Abs(rect.Bottom - mi.rcMonitor.Bottom) <= tolerance;
            SetFullscreen(fullscreen);
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }

    private static bool TryGetWindowBounds(IntPtr hwnd, out NativeMethods.RECT rect)
    {
        rect = default;
        if (NativeMethods.DwmGetWindowAttribute(hwnd, NativeMethods.DWMWA_EXTENDED_FRAME_BOUNDS, out rect, Marshal.SizeOf(typeof(NativeMethods.RECT))) == 0)
        {
            return true;
        }
        return NativeMethods.GetWindowRect(hwnd, out rect);
    }

    private void SetFullscreen(bool active)
    {
        if (_fullscreenActive == active)
        {
            return;
        }

        _fullscreenActive = active;
        if (_fullscreenActive)
        {
            Visibility = Visibility.Collapsed;
            Topmost = false;
        }
        else
        {
            Visibility = Visibility.Visible;
            Topmost = true;
            AlignDock(!_isHidden);
            EnsureTopmost();
            ApplyGlassEffect();
        }
    }

    private bool IsIgnoredForeground(IntPtr hwnd)
    {
        var processName = NativeMethods.GetProcessName(hwnd)?.ToLowerInvariant() ?? string.Empty;
        if (processName is "shellexperiencehost" or "startmenuexperiencehost" or "searchui" or "searchapp")
        {
            return true;
        }
        var className = NativeMethods.GetWindowClassName(hwnd)?.ToLowerInvariant() ?? string.Empty;
        // Desktop / wallpaper surfaces should not force hiding the dock.
        if (className is "progman" or "workerw")
        {
            return true;
        }
        return false;
    }

    private void Shortcuts_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        UpdateVisibleItems();
    }

    private void UpdateVisibleItems()
    {
        if (IsEditMode)
        {
            VisibleShortcuts.Clear();
        }
        else
        {
            var totalPages = Math.Max(1, (int)Math.Ceiling(Shortcuts.Count / (double)_itemsPerPage));
            if (_currentPage >= totalPages)
            {
                _currentPage = totalPages - 1;
            }
            if (_currentPage < 0) _currentPage = 0;

            VisibleShortcuts.Clear();
            foreach (var item in Shortcuts.Skip(_currentPage * _itemsPerPage).Take(_itemsPerPage))
            {
                VisibleShortcuts.Add(item);
            }
            OnPropertyChanged(nameof(PageInfo));
        }

        OnPropertyChanged(nameof(HasMultiplePages));
        OnPropertyChanged(nameof(PaginationVisibility));
    }
    public string PageInfo => $"{LocalizationService.Get("Common_Page")} {_currentPage + 1}/{Math.Max(1, (int)Math.Ceiling(Shortcuts.Count / (double)_itemsPerPage))}";

    private void PrevPage_Click(object sender, RoutedEventArgs e)
    {
        if (IsEditMode) return;
        var totalPages = Math.Max(1, (int)Math.Ceiling(Shortcuts.Count / (double)_itemsPerPage));
        if (totalPages <= 1) return;

        _currentPage = _currentPage <= 0 ? totalPages - 1 : _currentPage - 1;
        UpdateVisibleItems();
    }

    private void NextPage_Click(object sender, RoutedEventArgs e)
    {
        if (IsEditMode) return;
        var totalPages = Math.Max(1, (int)Math.Ceiling(Shortcuts.Count / (double)_itemsPerPage));
        if (totalPages <= 1) return;

        _currentPage = _currentPage >= totalPages - 1 ? 0 : _currentPage + 1;
        UpdateVisibleItems();
    }

    private void UpdateItemsPerPage()
    {
        var bounds = GetMonitorBounds();
        var monitorHeight = bounds.Height > 0 ? bounds.Height : SystemParameters.PrimaryScreenHeight;
        var perItem = IconSize + 84;
        var count = (int)Math.Floor(monitorHeight / perItem);
        _itemsPerPage = Math.Max(1, count);
        OnPropertyChanged(nameof(HasMultiplePages));
        OnPropertyChanged(nameof(PaginationVisibility));
    }

    private void RefreshModeUI()
    {
        UpdateVisibleItems();
        OnPropertyChanged(nameof(HasMultiplePages));
        OnPropertyChanged(nameof(PaginationVisibility));
    }

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    private void EnsureTopmost()
    {
        try
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero) return;
            NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0,
                NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }

    private void HideFromWindowSwitchers()
    {
        try
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero) return;
            var exStyle = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
            exStyle |= NativeMethods.WS_EX_TOOLWINDOW;
            exStyle &= ~NativeMethods.WS_EX_APPWINDOW;
            NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE, exStyle);
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }

    private void ApplyGlassEffect()
    {
        try
        {
            if (!_config.UseTransparency)
            {
                DisableGlassEffect();
                return;
            }

            var opacity = Math.Clamp(_config.BackgroundOpacity, 0.2, 1.0);
            // Opacity 1.0 => sin efecto blur (solo color sólido)
            if (opacity >= 0.99)
            {
                DisableGlassEffect();
                return;
            }

            var hr = NativeMethods.DwmIsCompositionEnabled(out var enabled);
            if (hr != 0 || !enabled)
            {
                DisableGlassEffect();
                return;
            }

            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero) return;

            var margins = new NativeMethods.MARGINS { cxLeftWidth = -1, cxRightWidth = -1, cyTopHeight = -1, cyBottomHeight = -1 };
            NativeMethods.DwmExtendFrameIntoClientArea(hwnd, ref margins);

            var blur = new NativeMethods.DWM_BLURBEHIND
            {
                dwFlags = NativeMethods.DWM_BB_ENABLE,
                fEnable = true,
                hRgnBlur = IntPtr.Zero,
                fTransitionOnMaximized = true
            };
            NativeMethods.DwmEnableBlurBehindWindow(hwnd, ref blur);
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }

    private void DisableGlassEffect()
    {
        try
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero) return;

            var hr = NativeMethods.DwmIsCompositionEnabled(out var enabled);
            if (hr != 0 || !enabled)
            {
                return;
            }

            var blur = new NativeMethods.DWM_BLURBEHIND
            {
                dwFlags = NativeMethods.DWM_BB_ENABLE,
                fEnable = false,
                hRgnBlur = IntPtr.Zero,
                fTransitionOnMaximized = false
            };
            NativeMethods.DwmEnableBlurBehindWindow(hwnd, ref blur);
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }
}

public enum DockSide
{
    Left,
    Right
}

// ---- Native interop for fullscreen detection ----
internal delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

internal static class NativeMethods
{
    public const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
    public const uint EVENT_OBJECT_LOCATIONCHANGE = 0x800B;
    public const uint WINEVENT_OUTOFCONTEXT = 0;
    public const uint MONITOR_DEFAULTTONEAREST = 2;
    public const int DWM_BB_ENABLE = 0x1;
    public const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;
    public const int OBJID_WINDOW = 0;
    public const int GWL_EXSTYLE = -20;
    public const int WS_EX_TOOLWINDOW = 0x00000080;
    public const int WS_EX_APPWINDOW = 0x00040000;

    [DllImport("user32.dll")]
    public static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

    [DllImport("user32.dll")]
    public static extern bool UnhookWinEvent(IntPtr hWinEventHook);

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    public static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("dwmapi.dll", PreserveSig = true)]
    public static extern int DwmEnableBlurBehindWindow(IntPtr hWnd, ref DWM_BLURBEHIND pBlurBehind);

    [DllImport("dwmapi.dll", PreserveSig = true)]
    public static extern int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS pMargins);

    [DllImport("dwmapi.dll", PreserveSig = true)]
    public static extern int DwmIsCompositionEnabled(out bool pfEnabled);

    [DllImport("dwmapi.dll", PreserveSig = true)]
    public static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out RECT pvAttribute, int cbAttribute);

    public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    public const uint SWP_NOSIZE = 0x0001;
    public const uint SWP_NOMOVE = 0x0002;
    public const uint SWP_NOACTIVATE = 0x0010;

    public static string? GetProcessName(IntPtr hwnd)
    {
        try
        {
            GetWindowThreadProcessId(hwnd, out var pid);
            if (pid == 0) return null;
            var proc = Process.GetProcessById((int)pid);
            return proc.ProcessName;
        }
        catch
        {
            return null;
        }
    }

    public static string? GetWindowClassName(IntPtr hwnd)
    {
        try
        {
            var sb = new System.Text.StringBuilder(256);
            var len = GetClassName(hwnd, sb, sb.Capacity);
            return len > 0 ? sb.ToString() : null;
        }
        catch
        {
            return null;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MARGINS
    {
        public int cxLeftWidth;
        public int cxRightWidth;
        public int cyTopHeight;
        public int cyBottomHeight;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DWM_BLURBEHIND
    {
        public uint dwFlags;
        [MarshalAs(UnmanagedType.Bool)]
        public bool fEnable;
        public IntPtr hRgnBlur;
        [MarshalAs(UnmanagedType.Bool)]
        public bool fTransitionOnMaximized;
    }
}

