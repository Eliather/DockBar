using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
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
    private const double GlassOpacity = 0.45;
    private const int InitialRecoveryDelayMs = 600;
    private const int RetryRecoveryDelayMs = 900;
    private const int ResumeRecoveryPasses = 3;
    private const int DisplayRecoveryPasses = 2;
    private const int ConfigWatcherDebounceMs = 450;
    private const int ConfigWatcherSuppressMs = 1500;

    private readonly DispatcherTimer _hideTimer;
    private readonly DispatcherTimer _configReloadTimer;
    private DockConfig _config = new();
    private bool _isHidden;
    private DockSide _dockSide = DockSide.Left;
    private const double EdgeRevealPx = 2;
    private bool _isAnimating;
    private double _iconSize = 28;
    private SolidColorBrush _dockBackgroundBrush = new(System.Windows.Media.Color.FromRgb(16, 16, 16));
    private SolidColorBrush _dockTextBrush = new(System.Windows.Media.Color.FromRgb(242, 242, 242));
    private System.Windows.Media.Brush _dockBorderBrush = System.Windows.Media.Brushes.Transparent;
    private System.Windows.Media.Effects.DropShadowEffect? _dockTextShadowEffect;
    private Thickness _dockBorderThickness = new(0, 0, 1, 0);
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
    private readonly DispatcherTimer _fullscreenDebounceTimer;
    private bool _isPaused = false;
    private bool _updateCheckRunning;
    private readonly DispatcherTimer _systemRecoveryTimer;
    private bool _reloadConfigOnRecovery;
    private int _recoveryPassesRemaining;
    private bool _itemsPerPageRefreshQueued;
    private bool _syncingEditModeScroll;
    private EdgeHotspotWindow? _edgeHotspot;
    private FileSystemWatcher? _configWatcher;
    private DateTime _suppressConfigWatcherUntilUtc;

    public DockConfig Config => _config;
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

    public System.Windows.Media.Brush DockBorderBrush
    {
        get => _dockBorderBrush;
        private set
        {
            if (_dockBorderBrush != value)
            {
                _dockBorderBrush = value;
                OnPropertyChanged();
            }
        }
    }

    public Thickness DockBorderThickness
    {
        get => _dockBorderThickness;
        private set
        {
            if (_dockBorderThickness != value)
            {
                _dockBorderThickness = value;
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

    public System.Windows.Media.Effects.DropShadowEffect? DockTextShadowEffect
    {
        get => _dockTextShadowEffect;
        private set
        {
            if (!Equals(_dockTextShadowEffect, value))
            {
                _dockTextShadowEffect = value;
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
                    UpdateLayout();
                    AlignDock(true);
                    QueueDockRealign(true);
                    ShowDockAnimated();
                    Dispatcher.BeginInvoke(new Action(UpdateEditModeScrollBar), DispatcherPriority.Loaded);
                }
                else
                {
                    Width = Math.Max(_config.DockWidth, 175);
                    UpdateLayout();
                    AlignDock(!_isHidden);
                    QueueDockRealign(!_isHidden);
                    StartHideTimer();
                    if (EditModeScrollBar != null)
                    {
                        EditModeScrollBar.Visibility = Visibility.Collapsed;
                    }
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

        _fullscreenDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        _fullscreenDebounceTimer.Tick += (_, _) =>
        {
            _fullscreenDebounceTimer.Stop();
            UpdateFullscreenState();
        };

        _systemRecoveryTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(InitialRecoveryDelayMs)
        };
        _systemRecoveryTimer.Tick += SystemRecoveryTimer_Tick;
        _configReloadTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(ConfigWatcherDebounceMs)
        };
        _configReloadTimer.Tick += ConfigReloadTimer_Tick;

        LoadConfigAndShortcuts();
        InitializeConfigWatcher();
        Shortcuts.CollectionChanged += Shortcuts_CollectionChanged;
        RegisterSystemEventHandlers();
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
        if (createdDefault)
        {
            var message = hadError
                ? LocalizationService.Get("Config_ReadError")
                : LocalizationService.Get("Config_NotFound");
            ThemedMessageBox.Show(message, "DockBar", MessageBoxButton.OK, MessageBoxImage.Warning);
            PersistConfigToDisk(loaded);
        }
        ApplyConfigState(loaded);

        HandleAutoStartPrompt();
        AutoStartService.Apply(_config.AutoStartEnabled);
    }

    private void ApplyConfigState(DockConfig config, bool forceIconRefresh = true)
    {
        _config = config ?? new DockConfig();
        _config.Shortcuts ??= new();
        _dockSide = _config.DockSide;
        ApplyVisualConfig();
        ReplaceShortcuts(_config.Shortcuts, forceIconRefresh);
    }

    private bool TryReloadConfigFromDisk(bool allowReplacingWithEmptyState)
    {
        var hadRuntimeShortcuts = Shortcuts.Count > 0 || (_config.Shortcuts?.Count ?? 0) > 0;
        var loaded = ConfigService.LoadConfig(out var createdDefault, out var hadError);
        var loadedShortcuts = loaded.Shortcuts ?? new();

        var suspiciousLoad =
            hadError ||
            (createdDefault && hadRuntimeShortcuts) ||
            (!allowReplacingWithEmptyState && hadRuntimeShortcuts && loadedShortcuts.Count == 0 && File.Exists(ConfigService.ConfigFilePath));

        if (suspiciousLoad)
        {
            return false;
        }

        ApplyConfigState(loaded);
        return true;
    }

    private void ReplaceShortcuts(System.Collections.Generic.IEnumerable<ShortcutItem>? shortcuts, bool forceIconRefresh = true)
    {
        Shortcuts.Clear();

        foreach (var item in shortcuts ?? Enumerable.Empty<ShortcutItem>())
        {
            if (forceIconRefresh || item.Icon == null)
            {
                item.Icon = ResolveIcon(item);
            }

            Shortcuts.Add(item);
        }

        UpdateVisibleItems();
    }

    private void HandleAutoStartPrompt()
    {
        if (_config.AutoStartPrompted)
        {
            return;
        }

        var result = ThemedMessageBox.Show(
            LocalizationService.Get("AutoStart_Prompt"),
            LocalizationService.Get("AutoStart_Title"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        _config.AutoStartEnabled = result == MessageBoxResult.Yes;
        _config.AutoStartPrompted = true;
        PersistConfigToDisk(_config);
    }

    private void ApplyVisualConfig()
    {
        ThemeService.Apply(_config);
        IconSize = _config.IconSize;
        Width = Math.Max(_config.DockWidth, 175);
        UpdateBackgroundBrush();
        UpdateTextBrush();
        UpdateHideTimerInterval();
        UpdateItemsPerPage();
        AlignDock(!_isHidden);
        UpdateVisibleItems();
        ApplyGlassEffect();
        UpdateEdgeHotspotState();
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
            ? Math.Clamp(_config.BackgroundOpacity, 0.0, 1.0)
            : 1.0;
        var alpha = (byte)Math.Clamp((int)Math.Round(opacity * 255), 0, 255);
        var brush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(alpha, color.R, color.G, color.B));
        brush.Freeze();
        DockBackgroundBrush = brush;

        // Windows 7 Aero Glass styling: Rectangular straight sidebar with 1px border on the inner dividing edge
        if (_dockSide == DockSide.Left)
        {
            DockBorderThickness = new Thickness(0, 0, 1, 0);
        }
        else
        {
            DockBorderThickness = new Thickness(1, 0, 0, 0);
        }

        if (_config.UseTransparency)
        {
            var borderColor = _config.UseLightText
                ? System.Windows.Media.Color.FromArgb(55, 255, 255, 255)
                : System.Windows.Media.Color.FromArgb(45, 0, 0, 0);
            var borderBrush = new SolidColorBrush(borderColor);
            borderBrush.Freeze();
            DockBorderBrush = borderBrush;
        }
        else
        {
            var borderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(50, 128, 128, 128));
            borderBrush.Freeze();
            DockBorderBrush = borderBrush;
        }
    }
    private void UpdateTextBrush()
    {
        var color = _config.UseLightText
            ? System.Windows.Media.Color.FromRgb(242, 242, 242)
            : System.Windows.Media.Color.FromRgb(10, 10, 10);
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        DockTextBrush = brush;

        if (_config.EnableTextShadow)
        {
            var shadowColor = _config.UseLightText
                ? System.Windows.Media.Color.FromArgb(230, 0, 0, 0)
                : System.Windows.Media.Color.FromArgb(230, 255, 255, 255);
            var effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = shadowColor,
                BlurRadius = 4,
                ShadowDepth = 1.2,
                Direction = 270,
                Opacity = 0.85
            };
            effect.Freeze();
            DockTextShadowEffect = effect;
        }
        else
        {
            DockTextShadowEffect = null;
        }
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        AlignDock(true);
        HookForegroundWatcher();
        ApplyGlassEffect();
        QueueItemsPerPageRefresh();
        Dispatcher.BeginInvoke(new Action(() => _ = CheckForUpdatesAsync(false)), DispatcherPriority.Background);
    }

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!IsLoaded || _isAnimating || _fullscreenActive)
        {
            return;
        }

        if (e.WidthChanged)
        {
            QueueDockRealign(!_isHidden);
        }

        if (e.HeightChanged || e.WidthChanged)
        {
            UpdateItemsPerPage();
            UpdateVisibleItems();
            QueueItemsPerPageRefresh();
            if (IsEditMode)
            {
                Dispatcher.BeginInvoke(new Action(UpdateEditModeScrollBar), DispatcherPriority.Loaded);
            }
        }
    }

    private void EditListScroll_ScrollChanged(object sender, System.Windows.Controls.ScrollChangedEventArgs e)
    {
        UpdateEditModeScrollBar();
    }

    private void EditModeScrollBar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_syncingEditModeScroll || EditListScroll == null)
        {
            return;
        }

        EditListScroll.ScrollToVerticalOffset(e.NewValue);
    }

    private void UpdateEditModeScrollBar()
    {
        if (EditListScroll == null || EditModeScrollBar == null)
        {
            return;
        }

        var canScroll = IsEditMode && EditListScroll.ScrollableHeight > 1;
        _syncingEditModeScroll = true;
        EditModeScrollBar.Minimum = 0;
        EditModeScrollBar.Maximum = Math.Max(0, EditListScroll.ScrollableHeight);
        EditModeScrollBar.Value = Math.Min(EditListScroll.VerticalOffset, EditModeScrollBar.Maximum);
        EditModeScrollBar.IsEnabled = canScroll;
        EditModeScrollBar.Visibility = canScroll ? Visibility.Visible : Visibility.Collapsed;
        _syncingEditModeScroll = false;
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
        ApplyGlassEffect();
    }

    private void RegisterSystemEventHandlers()
    {
        Win32.SystemEvents.PowerModeChanged += SystemEvents_PowerModeChanged;
        Win32.SystemEvents.SessionSwitch += SystemEvents_SessionSwitch;
        Win32.SystemEvents.DisplaySettingsChanged += SystemEvents_DisplaySettingsChanged;
    }

    private void UnregisterSystemEventHandlers()
    {
        Win32.SystemEvents.PowerModeChanged -= SystemEvents_PowerModeChanged;
        Win32.SystemEvents.SessionSwitch -= SystemEvents_SessionSwitch;
        Win32.SystemEvents.DisplaySettingsChanged -= SystemEvents_DisplaySettingsChanged;
    }

    private void SystemEvents_PowerModeChanged(object? sender, Win32.PowerModeChangedEventArgs e)
    {
        if (e.Mode == Win32.PowerModes.Resume)
        {
            ScheduleDockRecovery(reloadConfigIfMissing: true);
        }
    }

    private void SystemEvents_SessionSwitch(object? sender, Win32.SessionSwitchEventArgs e)
    {
        if (e.Reason is Win32.SessionSwitchReason.SessionUnlock or Win32.SessionSwitchReason.ConsoleConnect)
        {
            ScheduleDockRecovery(reloadConfigIfMissing: true);
        }
    }

    private void SystemEvents_DisplaySettingsChanged(object? sender, EventArgs e)
    {
        ScheduleDockRecovery(reloadConfigIfMissing: false);
    }

    private void ScheduleDockRecovery(bool reloadConfigIfMissing)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(new Action(() => ScheduleDockRecovery(reloadConfigIfMissing)));
            return;
        }

        _reloadConfigOnRecovery |= reloadConfigIfMissing;
        _recoveryPassesRemaining = Math.Max(
            _recoveryPassesRemaining,
            reloadConfigIfMissing ? ResumeRecoveryPasses : DisplayRecoveryPasses);
        _systemRecoveryTimer.Stop();
        _systemRecoveryTimer.Interval = TimeSpan.FromMilliseconds(InitialRecoveryDelayMs);
        _systemRecoveryTimer.Start();
    }

    private void InitializeConfigWatcher()
    {
        try
        {
            Directory.CreateDirectory(ConfigService.ConfigDirectory);
            ResetConfigWatcher();
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }

    private void ResetConfigWatcher()
    {
        DisposeConfigWatcher();

        try
        {
            var watcher = new FileSystemWatcher(ConfigService.ConfigDirectory, Path.GetFileName(ConfigService.ConfigFilePath))
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime | NotifyFilters.LastWrite | NotifyFilters.Size,
                IncludeSubdirectories = false,
                EnableRaisingEvents = false
            };

            watcher.Changed += ConfigWatcher_Changed;
            watcher.Created += ConfigWatcher_Changed;
            watcher.Renamed += ConfigWatcher_Renamed;
            watcher.Error += ConfigWatcher_Error;
            watcher.EnableRaisingEvents = true;
            _configWatcher = watcher;
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }

    private void DisposeConfigWatcher()
    {
        if (_configWatcher == null)
        {
            return;
        }

        _configWatcher.EnableRaisingEvents = false;
        _configWatcher.Changed -= ConfigWatcher_Changed;
        _configWatcher.Created -= ConfigWatcher_Changed;
        _configWatcher.Renamed -= ConfigWatcher_Renamed;
        _configWatcher.Error -= ConfigWatcher_Error;
        _configWatcher.Dispose();
        _configWatcher = null;
    }

    private void ConfigWatcher_Changed(object sender, FileSystemEventArgs e)
    {
        QueueConfigReloadFromWatcher();
    }

    private void ConfigWatcher_Renamed(object sender, RenamedEventArgs e)
    {
        QueueConfigReloadFromWatcher();
    }

    private void ConfigWatcher_Error(object sender, ErrorEventArgs e)
    {
        Dispatcher.BeginInvoke(new Action(ResetConfigWatcher));
    }

    private void QueueConfigReloadFromWatcher()
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(new Action(QueueConfigReloadFromWatcher));
            return;
        }

        if (DateTime.UtcNow < _suppressConfigWatcherUntilUtc)
        {
            return;
        }

        _configReloadTimer.Stop();
        _configReloadTimer.Start();
    }

    private void ConfigReloadTimer_Tick(object? sender, EventArgs e)
    {
        _configReloadTimer.Stop();
        TryReloadConfigFromDisk(allowReplacingWithEmptyState: false);
    }

    private void SystemRecoveryTimer_Tick(object? sender, EventArgs e)
    {
        _systemRecoveryTimer.Stop();
        var reloadConfigIfMissing = _reloadConfigOnRecovery;
        RecoverDockAfterResume(reloadConfigIfMissing);

        if (reloadConfigIfMissing)
        {
            QueueConfigReloadFromWatcher();
        }

        _recoveryPassesRemaining = Math.Max(0, _recoveryPassesRemaining - 1);
        if (_recoveryPassesRemaining > 0 && ShouldRetryDockRecovery())
        {
            _systemRecoveryTimer.Interval = TimeSpan.FromMilliseconds(RetryRecoveryDelayMs);
            _systemRecoveryTimer.Start();
            return;
        }

        _reloadConfigOnRecovery = false;
        _recoveryPassesRemaining = 0;
        _systemRecoveryTimer.Interval = TimeSpan.FromMilliseconds(InitialRecoveryDelayMs);
    }

    private void RecoverDockAfterResume(bool reloadConfigIfMissing)
    {
        try
        {
            BeginAnimation(Window.LeftProperty, null);
            _isAnimating = false;
            _fullscreenActive = false;
            Visibility = Visibility.Visible;
            Topmost = true;

            UpdateItemsPerPage();
            AlignDock(!_isHidden);
            ApplyGlassEffect();
            EnsureTopmost();

            var needsStateRebuild = reloadConfigIfMissing || Shortcuts.Count == 0 || (!IsEditMode && VisibleShortcuts.Count == 0);
            var reloadedFromDisk = false;
            if (needsStateRebuild)
            {
                reloadedFromDisk = TryReloadConfigFromDisk(allowReplacingWithEmptyState: false);
            }

            if (!reloadedFromDisk)
            {
                if (Shortcuts.Count == 0 && (_config.Shortcuts?.Count ?? 0) > 0)
                {
                    ReplaceShortcuts(_config.Shortcuts);
                }

                RefreshShortcutIcons(force: true);
                UpdateVisibleItems();
            }

            if (!IsEditMode && Shortcuts.Count > 0 && VisibleShortcuts.Count == 0)
            {
                _currentPage = 0;
                UpdateVisibleItems();
            }

            UpdateEdgeHotspotState();
            ScheduleFullscreenCheck();
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }

    private bool ShouldRetryDockRecovery()
    {
        if (IsEditMode)
        {
            return false;
        }

        var hasKnownShortcuts = Shortcuts.Count > 0 || (_config.Shortcuts?.Count ?? 0) > 0;
        if (!hasKnownShortcuts)
        {
            return false;
        }

        return Shortcuts.Count == 0 || VisibleShortcuts.Count == 0;
    }

    private void RefreshShortcutIcons(bool force = false)
    {
        foreach (var item in Shortcuts)
        {
            if (force || item.Icon == null)
            {
                item.Icon = ResolveIcon(item);
            }
        }
    }

    private void AlignDock(bool showState)
    {
        var area = GetMonitorBounds();
        Top = area.Top;
        Height = area.Height;
        Left = showState ? GetShownLeft(area) : GetHiddenLeft(area);
        UpdateEdgeHotspotState();
    }

    private double GetShownLeft(Rect area)
    {
        var dockWidth = GetDockWidthForPositioning();
        return _dockSide == DockSide.Left
            ? area.Left
            : area.Right - dockWidth;
    }

    private double GetHiddenLeft(Rect area)
    {
        var dockWidth = GetDockWidthForPositioning();
        return _dockSide == DockSide.Left
            ? area.Left - (dockWidth - EdgeRevealPx)
            : area.Right - EdgeRevealPx;
    }

    private double GetDockWidthForPositioning()
    {
        return Math.Max(Width, ActualWidth);
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
                return ConvertMonitorRectToDip(monitor, mi.rcMonitor);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }

        return new Rect(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop, SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
    }

    private Rect ConvertMonitorRectToDip(IntPtr monitor, NativeMethods.RECT rect)
    {
        var source = PresentationSource.FromVisual(this);
        if (source?.CompositionTarget != null)
        {
            var transform = source.CompositionTarget.TransformFromDevice;
            var topLeft = transform.Transform(new System.Windows.Point(rect.Left, rect.Top));
            var bottomRight = transform.Transform(new System.Windows.Point(rect.Right, rect.Bottom));
            return new Rect(topLeft, bottomRight);
        }

        var scaleX = 1.0;
        var scaleY = 1.0;
        if (NativeMethods.TryGetMonitorDpi(monitor, out var dpiX, out var dpiY))
        {
            scaleX = 96.0 / dpiX;
            scaleY = 96.0 / dpiY;
        }

        var left = rect.Left * scaleX;
        var top = rect.Top * scaleY;
        var width = (rect.Right - rect.Left) * scaleX;
        var height = (rect.Bottom - rect.Top) * scaleY;
        return new Rect(left, top, width, height);
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
        UpdateEdgeHotspotState();
        AnimateLeft(Left, GetShownLeft(GetMonitorBounds()));
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
        AnimateLeft(Left, GetHiddenLeft(GetMonitorBounds()));
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

    private void QueueDockRealign(bool showState)
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (_fullscreenActive)
            {
                return;
            }

            UpdateLayout();
            AlignDock(showState);
            EnsureTopmost();
        }), DispatcherPriority.Render);
    }

    private void Window_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
        {
            return;
        }

        var files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);
        var changed = false;
        foreach (var file in files)
        {
            changed |= AddShortcut(file, persist: false);
        }
        if (changed)
        {
            SaveConfig();
        }
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

    private bool AddShortcut(string path, string? displayName = null, ImageSource? iconOverride = null, bool persist = true, string? arguments = null)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var isFileOrDir = File.Exists(path) || Directory.Exists(path);
        if (!isFileOrDir)
        {
            if (!(Uri.TryCreate(path, UriKind.Absolute, out var uri) && !uri.IsFile && !string.IsNullOrWhiteSpace(uri.Scheme)))
            {
                return false;
            }
        }

        if (Shortcuts.Any(s =>
                string.Equals(s.Path, path, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(s.Arguments ?? string.Empty, arguments ?? string.Empty, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        string? friendly = null;
        ImageSource? shellIcon = null;
        string? resolvedIconPath = null;

        if (path.EndsWith(".url", StringComparison.OrdinalIgnoreCase) && File.Exists(path))
        {
            var (targetUrl, iconFile, _, urlTitle) = SteamService.ParseUrlFile(path);
            if (!string.IsNullOrWhiteSpace(iconFile) && File.Exists(iconFile))
            {
                resolvedIconPath = iconFile;
            }
            if (!string.IsNullOrWhiteSpace(targetUrl))
            {
                var steamAppId = SteamService.ExtractSteamAppId(targetUrl);
                if (!string.IsNullOrWhiteSpace(steamAppId))
                {
                    var (gameName, steamIconPath, _) = SteamService.GetGameInfoByAppId(steamAppId);
                    if (!string.IsNullOrWhiteSpace(gameName) && string.IsNullOrWhiteSpace(displayName))
                    {
                        displayName = gameName;
                    }
                    if (resolvedIconPath == null && !string.IsNullOrWhiteSpace(steamIconPath))
                    {
                        resolvedIconPath = steamIconPath;
                    }
                }
            }
        }
        else if (path.StartsWith("steam://", StringComparison.OrdinalIgnoreCase))
        {
            var steamAppId = SteamService.ExtractSteamAppId(path);
            if (!string.IsNullOrWhiteSpace(steamAppId))
            {
                var (gameName, steamIconPath, _) = SteamService.GetGameInfoByAppId(steamAppId);
                if (!string.IsNullOrWhiteSpace(gameName) && string.IsNullOrWhiteSpace(displayName))
                {
                    displayName = gameName;
                }
                if (!string.IsNullOrWhiteSpace(steamIconPath))
                {
                    resolvedIconPath = steamIconPath;
                }
            }
        }

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
                    : (path.StartsWith("shell:", StringComparison.OrdinalIgnoreCase) ? path : (Uri.TryCreate(path, UriKind.Absolute, out var parsedUri) ? parsedUri.Host : path));

        var item = new ShortcutItem
        {
            Name = string.IsNullOrEmpty(name) ? path : name,
            Path = path,
            Arguments = arguments,
            IconPath = resolvedIconPath,
            Icon = iconOverride
                   ?? (resolvedIconPath != null ? ShellItemService.AutoCropIfNeeded(IconService.GetIconFromPath(resolvedIconPath, (int)Math.Max(_config.IconSize * 4, 256))) : null)
                   ?? shellIcon
                   ?? (isFileOrDir ? IconService.GetIcon(path, (int)Math.Max(_config.IconSize * 4, 256)) : null)
        };

        Shortcuts.Add(item);
        if (persist)
        {
            SaveConfig();
        }
        return true;
    }

    private void Shortcut_Click(object sender, RoutedEventArgs e)
    {
        if (IsEditMode)
        {
            return;
        }

        if (sender is not System.Windows.Controls.Button button || button.Tag is not ShortcutItem item)
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = item.Path,
                Arguments = item.Arguments ?? string.Empty,
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
        if (sender is not System.Windows.Controls.Button button || button.Tag is not ShortcutItem item)
        {
            return;
        }

        if (item != null)
        {
            Shortcuts.Remove(item);
            SaveConfig();
        }
    }

    private void Rename_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button button || button.Tag is not ShortcutItem item)
        {
            return;
        }

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
        try
        {
            if (sender is not System.Windows.Controls.Button button || button.Tag is not ShortcutItem item)
            {
                return;
            }

            var dialog = new Win32.OpenFileDialog
            {
                Title = LocalizationService.Get("Dialog_SelectIconTitle"),
                Filter = LocalizationService.Get("Dialog_ImageFilter"),
                Multiselect = false
            };

            if (dialog.ShowDialog(this) == true)
            {
                var target = dialog.FileName;
                if (item != null)
                {
                    item.IconPath = target;
                    item.Icon = IconService.GetIconFromPath(target, (int)Math.Max(_config.IconSize * 4, 256)) ?? item.Icon;
                    SaveConfig();
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }

    private void AddShortcutMenu_Click(object sender, RoutedEventArgs e)
    {
        var menu = new System.Windows.Controls.ContextMenu
        {
            PlacementTarget = sender as System.Windows.Controls.Button,
            Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom
        };

        if (TryFindResource("DockContextMenuStyle") is Style menuStyle)
        {
            menu.Style = menuStyle;
        }

        var menuItemStyle = TryFindResource("DockContextMenuItemStyle") as Style;

        var fileItem = new System.Windows.Controls.MenuItem
        {
            Header = LocalizationService.Get("AddMenu_File")
        };
        if (menuItemStyle != null) fileItem.Style = menuItemStyle;
        fileItem.Click += (_, _) => AddFileShortcut();

        var storeItem = new System.Windows.Controls.MenuItem
        {
            Header = LocalizationService.Get("AddMenu_Store")
        };
        if (menuItemStyle != null) storeItem.Style = menuItemStyle;
        storeItem.Click += (_, _) => AddStoreAppFlow();

        var uriItem = new System.Windows.Controls.MenuItem
        {
            Header = LocalizationService.Get("AddMenu_Uri")
        };
        if (menuItemStyle != null) uriItem.Style = menuItemStyle;
        uriItem.Click += (_, _) => AddUriShortcut();

        menu.Items.Add(fileItem);
        menu.Items.Add(storeItem);
        menu.Items.Add(uriItem);
        menu.IsOpen = true;
    }

    private void AddFileShortcut()
    {
        try
        {
            var dialog = new Win32.OpenFileDialog
            {
                Title = LocalizationService.Get("Dialog_SelectShortcutTitle"),
                Filter = LocalizationService.Get("Dialog_ExecutableFilter"),
                Multiselect = true
            };

            if (dialog.ShowDialog(this) == true)
            {
                var changed = false;
                foreach (var file in dialog.FileNames)
                {
                    changed |= AddShortcut(file, persist: false);
                }

                if (changed)
                {
                    SaveConfig();
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
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
            AddShortcut(addLink.ResolvedTarget, addLink.DisplayName, persist: true, arguments: addLink.ResolvedArguments);
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
        AddStoreAppFlow();
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
        RevealDockOnCurrentSide();
    }

    public void TogglePause()
    {
        _isPaused = !_isPaused;
        if (_isPaused)
        {
            Visibility = Visibility.Collapsed;
            Topmost = false;
            _edgeHotspot?.HideHotspot();
        }
        else
        {
            Visibility = Visibility.Visible;
            Topmost = true;
            EnsureTopmost();
            UpdateEdgeHotspotState();
        }
    }

    public void RevealDockOnCurrentSide()
    {
        BeginAnimation(Window.LeftProperty, null);
        _isAnimating = false;
        _fullscreenActive = false;
        _isHidden = false;
        StopHideTimer();
        Visibility = Visibility.Visible;
        Topmost = true;
        Show();
        AlignDock(true);
        EnsureTopmost();
        ApplyGlassEffect();
        UpdateEdgeHotspotState();
    }

    private void EnsureEdgeHotspot()
    {
        if (_edgeHotspot != null)
        {
            return;
        }

        _edgeHotspot = new EdgeHotspotWindow();
        _edgeHotspot.HotspotTriggered += (_, _) =>
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                StopHideTimer();
                ShowDockAnimated();
            }));
        };
    }

    private void UpdateEdgeHotspotState()
    {
        if (_isPaused)
        {
            _edgeHotspot?.HideHotspot();
            return;
        }

        if (!IsLoaded)
        {
            return;
        }

        EnsureEdgeHotspot();
        if (_edgeHotspot == null)
        {
            return;
        }

        var shouldShow =
            _isHidden &&
            !_isAnimating &&
            !_fullscreenActive &&
            !IsEditMode &&
            Visibility == Visibility.Visible;

        if (!shouldShow)
        {
            _edgeHotspot.HideHotspot();
            return;
        }

        _edgeHotspot.ShowOnEdge(GetMonitorBounds(), _dockSide, Math.Max(EdgeRevealPx, 6));
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
            EnableTextShadow = source.EnableTextShadow,
            AutoStartEnabled = source.AutoStartEnabled,
            AutoStartPrompted = source.AutoStartPrompted,
            Shortcuts = shortcuts.Select(s => new ShortcutItem
            {
                Name = s.Name,
                Path = s.Path,
                Arguments = s.Arguments,
                IconPath = s.IconPath
            }).ToList()
        };
    }

    public void ApplyAndSaveConfig(DockConfig updatedConfig)
    {
        var prevAutoStart = _config.AutoStartEnabled;
        updatedConfig.Shortcuts ??= new();
        _config = updatedConfig;
        if (!_config.AutoStartPrompted && prevAutoStart != _config.AutoStartEnabled)
        {
            _config.AutoStartPrompted = true;
        }
        ApplyConfigState(_config);
        SaveConfig();
        AutoStartService.Apply(_config.AutoStartEnabled);
    }

    public void ReloadConfigAndApply()
    {
        TryReloadConfigFromDisk(allowReplacingWithEmptyState: true);
    }

    public async Task CheckForUpdatesAsync(bool userInitiated)
    {
        if (_updateCheckRunning)
        {
            return;
        }

        _updateCheckRunning = true;
        try
        {
            var latest = await UpdateService.GetLatestReleaseAsync(CancellationToken.None);
            if (latest == null)
            {
                if (userInitiated)
                {
                    ShowUpdateMessage(
                        LocalizationService.Get("Update_CheckFailed"),
                        LocalizationService.Get("Update_Title"),
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
                return;
            }

            var current = UpdateService.GetCurrentVersion();
            if (latest.Version <= current)
            {
                if (userInitiated)
                {
                    var msg = string.Format(LocalizationService.Get("Update_UpToDate"), $"v{current}");
                    ShowUpdateMessage(
                        msg,
                        LocalizationService.Get("Update_Title"),
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                return;
            }

            var updateWindow = new UpdateWindow(latest, current);
            if (IsVisible && WindowState != WindowState.Minimized)
            {
                updateWindow.Owner = this;
                updateWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }
            else
            {
                updateWindow.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }

            updateWindow.ShowDialog();
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            if (userInitiated)
            {
                ShowUpdateMessage(
                    LocalizationService.Get("Update_CheckFailed"),
                    LocalizationService.Get("Update_Title"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }
        finally
        {
            _updateCheckRunning = false;
        }
    }

    private void ShowUpdateMessage(string message, string title, MessageBoxButton button, MessageBoxImage image)
    {
        var owner = IsVisible && WindowState != WindowState.Minimized ? this : null;
        ThemedMessageBox.Show(owner, message, title, button, image);
    }

    private void SaveConfig()
    {
        _config.DockSide = _dockSide;
        _config.DockWidth = IsEditMode ? Math.Max(_preEditWidth, 175) : Math.Max(Width, 175);
        _config.IconSize = IconSize;
        _config.UseLightText = _config.UseLightText;
        _config.Shortcuts = Shortcuts.ToList();
        PersistConfigToDisk(_config);
        UpdateVisibleItems();
    }

    private void PersistConfigToDisk(DockConfig config)
    {
        _suppressConfigWatcherUntilUtc = DateTime.UtcNow.AddMilliseconds(ConfigWatcherSuppressMs);
        ConfigService.SaveConfig(config);
    }

    private ImageSource? ResolveIcon(ShortcutItem item)
    {
        if (item.Icon != null)
        {
            return item.Icon;
        }

        if (!string.IsNullOrWhiteSpace(item.IconPath) && File.Exists(item.IconPath))
        {
            var custom = IconService.GetIconFromPath(item.IconPath, (int)Math.Max(_config.IconSize * 4, 256));
            if (custom != null)
            {
                return custom;
            }
        }

        var path = item.Path;

        // 1. Steam URL protocols
        if (path.StartsWith("steam://", StringComparison.OrdinalIgnoreCase))
        {
            var appId = SteamService.ExtractSteamAppId(path);
            if (!string.IsNullOrWhiteSpace(appId))
            {
                var (_, iconPath, _) = SteamService.GetGameInfoByAppId(appId);
                if (!string.IsNullOrWhiteSpace(iconPath))
                {
                    var steamIcon = IconService.GetIconFromPath(iconPath, (int)Math.Max(_config.IconSize * 4, 256));
                    if (steamIcon != null) return ShellItemService.AutoCropIfNeeded(steamIcon);
                }
            }
        }

        // 2. .url files
        if (path.EndsWith(".url", StringComparison.OrdinalIgnoreCase) && File.Exists(path))
        {
            var urlIcon = IconService.GetIcon(path, (int)Math.Max(_config.IconSize * 4, 256));
            if (urlIcon != null) return ShellItemService.AutoCropIfNeeded(urlIcon);
        }

        var physicalPath = ShellItemService.ResolveAppIdPath(path);
        if (File.Exists(physicalPath) || Directory.Exists(physicalPath))
        {
            var icon = IconService.GetIcon(physicalPath, (int)Math.Max(_config.IconSize * 4, 256));
            if (icon != null)
            {
                return ShellItemService.AutoCropIfNeeded(icon);
            }
        }

        if (path.StartsWith("shell:AppsFolder", StringComparison.OrdinalIgnoreCase))
        {
            var icon = ShellItemService.GetIcon(path, (int)Math.Max(_config.IconSize * 4, 256));
            if (icon != null) return icon;
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
        _edgeHotspot?.Close();
        _edgeHotspot = null;
        _systemRecoveryTimer.Stop();
        _configReloadTimer.Stop();
        DisposeConfigWatcher();
        UnregisterSystemEventHandlers();
        UnhookForegroundWatcher();
        base.OnClosed(e);
    }

    private void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        if (eventType == NativeMethods.EVENT_OBJECT_LOCATIONCHANGE)
        {
            if (idObject != NativeMethods.OBJID_WINDOW || idChild != 0 || hwnd == IntPtr.Zero)
            {
                return;
            }

            var myHwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == myHwnd)
            {
                return;
            }
        }

        ScheduleFullscreenCheck();
    }

    private void ScheduleFullscreenCheck()
    {
        if (_isPaused) return;
        _fullscreenDebounceTimer.Stop();
        _fullscreenDebounceTimer.Start();
    }

    private void UpdateFullscreenState()
    {
        if (_isPaused) return;

        try
        {
            var foreground = NativeMethods.GetForegroundWindow();
            if (foreground == IntPtr.Zero || IsIgnoredForeground(foreground))
            {
                SetFullscreen(false);
                return;
            }

            if (IsOverlayForeground(foreground))
            {
                // Ignore overlays taking focus so we don't change state during in-game overlays
                return;
            }

            if (!NativeMethods.IsWindowVisible(foreground) || NativeMethods.IsIconic(foreground))
            {
                SetFullscreen(false);
                return;
            }

            var monitor = NativeMethods.MonitorFromWindow(foreground, NativeMethods.MONITOR_DEFAULTTONEAREST);
            if (monitor == IntPtr.Zero)
            {
                SetFullscreen(false);
                return;
            }

            var mi = new NativeMethods.MONITORINFO { cbSize = Marshal.SizeOf(typeof(NativeMethods.MONITORINFO)) };
            if (!NativeMethods.GetMonitorInfo(monitor, ref mi))
            {
                SetFullscreen(false);
                return;
            }

            // Get window bounds (both DWM extended frame and Win32 rect)
            var hasDwmBounds = NativeMethods.DwmGetWindowAttribute(foreground, NativeMethods.DWMWA_EXTENDED_FRAME_BOUNDS, out var dwmRect, Marshal.SizeOf(typeof(NativeMethods.RECT))) == 0
                && dwmRect.Right > dwmRect.Left && dwmRect.Bottom > dwmRect.Top;
            var hasWinBounds = NativeMethods.GetWindowRect(foreground, out var winRect)
                && winRect.Right > winRect.Left && winRect.Bottom > winRect.Top;

            if (!hasDwmBounds && !hasWinBounds)
            {
                SetFullscreen(false);
                return;
            }

            // Tolerance to accommodate invisible sizing borders (-8px) and DPI scaling differences
            const int tolerance = 10;
            var coversMonitor = (hasDwmBounds && IsWindowCoveringRect(dwmRect, mi.rcMonitor, tolerance)) ||
                                (hasWinBounds && IsWindowCoveringRect(winRect, mi.rcMonitor, tolerance));

            if (!coversMonitor)
            {
                SetFullscreen(false);
                return;
            }

            // If the monitor's work area equals the monitor area (e.g., Windows Taskbar is set to Auto-Hide or hidden),
            // a regular maximized window (such as Chrome or VS Code) will also cover the entire monitor.
            // We distinguish standard desktop maximized apps from fullscreen games / F11 mode by checking window styles.
            var isWorkAreaSameAsMonitor =
                Math.Abs(mi.rcWork.Left - mi.rcMonitor.Left) <= tolerance &&
                Math.Abs(mi.rcWork.Top - mi.rcMonitor.Top) <= tolerance &&
                Math.Abs(mi.rcWork.Right - mi.rcMonitor.Right) <= tolerance &&
                Math.Abs(mi.rcWork.Bottom - mi.rcMonitor.Bottom) <= tolerance;

            if (isWorkAreaSameAsMonitor)
            {
                var style = NativeMethods.GetWindowLong(foreground, NativeMethods.GWL_STYLE);
                var isMaximized = NativeMethods.IsZoomed(foreground) || ((style & NativeMethods.WS_MAXIMIZE) != 0);
                var isPopup = ((uint)style & NativeMethods.WS_POPUP) != 0;
                var hasThickFrame = (style & NativeMethods.WS_THICKFRAME) != 0;
                var hasCaption = (style & NativeMethods.WS_CAPTION) == NativeMethods.WS_CAPTION;

                // Standard desktop windows when maximized on auto-hide taskbar have thickframe or caption, and are not popup windows.
                if (isMaximized && !isPopup && (hasCaption || hasThickFrame))
                {
                    SetFullscreen(false);
                    return;
                }
            }

            // Foreground window covers the monitor and is not a standard maximized desktop window -> Fullscreen / Borderless game detected!
            SetFullscreen(true);
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }

    private static bool IsWindowCoveringRect(NativeMethods.RECT windowRect, NativeMethods.RECT targetRect, int tolerance)
    {
        return windowRect.Left <= targetRect.Left + tolerance &&
               windowRect.Top <= targetRect.Top + tolerance &&
               windowRect.Right >= targetRect.Right - tolerance &&
               windowRect.Bottom >= targetRect.Bottom - tolerance;
    }

    private bool IsOverlayForeground(IntPtr hwnd)
    {
        var processName = NativeMethods.GetProcessName(hwnd)?.ToLowerInvariant() ?? string.Empty;
        if (processName is "igo64" or "eadesktop" or "origin" or "originwebhelper" 
            or "gamebar" or "gamebarftserver" or "discord" or "nvsphelper64" 
            or "eabackgroundservice" or "overlay" or "steamwebhelper")
        {
            return true;
        }

        var className = NativeMethods.GetWindowClassName(hwnd)?.ToLowerInvariant() ?? string.Empty;
        if (className is "cef-osr-ipc-msg-wnd" or "tooltips_class32")
        {
            return true;
        }

        return false;
    }

    private bool IsIgnoredForeground(IntPtr hwnd)
    {
        var myHwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == myHwnd)
        {
            return true;
        }

        if (_edgeHotspot != null && new WindowInteropHelper(_edgeHotspot).Handle == hwnd)
        {
            return true;
        }

        var processName = NativeMethods.GetProcessName(hwnd)?.ToLowerInvariant() ?? string.Empty;
        if (processName is "dockbar" or "shellexperiencehost" or "startmenuexperiencehost" or "searchui" or "searchapp")
        {
            return true;
        }
        var className = NativeMethods.GetWindowClassName(hwnd)?.ToLowerInvariant() ?? string.Empty;
        // Desktop / wallpaper / shell surfaces should not force hiding the dock.
        if (className is "progman" or "workerw" or "shell_traywnd" or "shell_secondarytraywnd")
        {
            return true;
        }
        return false;
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
            if (!active && !_isPaused && IsVisible)
            {
                EnsureTopmost();
            }
            return;
        }

        _fullscreenActive = active;
        if (_fullscreenActive)
        {
            Visibility = Visibility.Collapsed;
            Topmost = false;
            UpdateEdgeHotspotState();
        }
        else
        {
            Visibility = Visibility.Visible;
            Topmost = true;
            AlignDock(!_isHidden);
            EnsureTopmost();
            ApplyGlassEffect();
            UpdateEdgeHotspotState();
        }
    }

    private void Shortcuts_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        UpdateVisibleItems();
        if (IsEditMode)
        {
            Dispatcher.BeginInvoke(new Action(UpdateEditModeScrollBar), DispatcherPriority.Loaded);
        }
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
        QueueItemsPerPageRefresh();
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
        if (TryMeasureItemsPerPage(out var measuredCount))
        {
            _itemsPerPage = measuredCount;
        }
        else
        {
            const double fallbackItemVerticalChrome = 40;
            const double fallbackChromeWithoutPagination = 72;
            const double fallbackChromeWithPagination = 112;

            var bounds = GetMonitorBounds();
            var monitorHeight = bounds.Height > 0 ? bounds.Height : SystemParameters.PrimaryScreenHeight;
            var perItem = Math.Max(IconSize + fallbackItemVerticalChrome, 1);
            var usableHeight = Math.Max(1, monitorHeight - fallbackChromeWithoutPagination);
            var count = Math.Max(1, (int)Math.Floor(usableHeight / perItem));

            if (Shortcuts.Count > count)
            {
                usableHeight = Math.Max(1, monitorHeight - fallbackChromeWithPagination);
                count = Math.Max(1, (int)Math.Floor(usableHeight / perItem));
            }

            _itemsPerPage = Math.Max(1, count);
        }

        OnPropertyChanged(nameof(HasMultiplePages));
        OnPropertyChanged(nameof(PaginationVisibility));
    }

    private void QueueItemsPerPageRefresh()
    {
        if (_itemsPerPageRefreshQueued || !IsLoaded || IsEditMode)
        {
            return;
        }

        _itemsPerPageRefreshQueued = true;
        Dispatcher.BeginInvoke(new Action(() =>
        {
            _itemsPerPageRefreshQueued = false;
            RefreshItemsPerPageFromLayout();
        }), DispatcherPriority.Render);
    }

    private void RefreshItemsPerPageFromLayout()
    {
        if (!TryMeasureItemsPerPage(out var measuredCount) || measuredCount == _itemsPerPage)
        {
            return;
        }

        _itemsPerPage = measuredCount;
        UpdateVisibleItems();
    }

    private bool TryMeasureItemsPerPage(out int count)
    {
        count = 0;
        if (!IsLoaded || IsEditMode || Shortcuts.Count == 0)
        {
            return false;
        }

        UpdateLayout();
        NormalList.UpdateLayout();

        if (DockLayoutRoot.RowDefinitions.Count == 0)
        {
            return false;
        }

        var availableHeight = DockLayoutRoot.RowDefinitions[0].ActualHeight;
        if (availableHeight <= 1)
        {
            return false;
        }

        if (NormalList.ItemContainerGenerator.ContainerFromIndex(0) is not FrameworkElement firstItem ||
            firstItem.ActualHeight <= 1)
        {
            return false;
        }

        count = Math.Max(1, (int)Math.Floor((availableHeight + 2) / firstItem.ActualHeight));
        return true;
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
        if (_isPaused || _fullscreenActive) return;

        try
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero) return;
            NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0,
                NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_NOOWNERZORDER);
            _edgeHotspot?.EnsureTopmost();
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }

    private void HideFromWindowSwitchers()
    {
        WindowSwitcherHelper.HideFromWindowSwitchers(this);
    }

    private void ApplyGlassEffect()
    {
        GlassEffectHelper.Apply(this, _config.UseTransparency, _config.UseLightText);
    }

    private void DisableGlassEffect()
    {
        GlassEffectHelper.Apply(this, false, _config.UseLightText);
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
    public const int MDT_EFFECTIVE_DPI = 0;
    public const int DWM_BB_ENABLE = 0x1;
    public const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;
    public const int OBJID_WINDOW = 0;
    public const int GWL_STYLE = -16;
    public const int GWL_EXSTYLE = -20;
    public const int WS_EX_TOOLWINDOW = 0x00000080;
    public const int WS_EX_APPWINDOW = 0x00040000;
    public const int WS_MAXIMIZE = 0x01000000;
    public const uint WS_POPUP = 0x80000000;
    public const int WS_CAPTION = 0x00C00000;
    public const int WS_THICKFRAME = 0x00040000;

    [DllImport("user32.dll")]
    public static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

    [DllImport("user32.dll")]
    public static extern bool UnhookWinEvent(IntPtr hWinEventHook);

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    public static extern bool IsZoomed(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr hWnd);

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

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

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
    public const uint SWP_NOOWNERZORDER = 0x0200;

    public static string? GetProcessName(IntPtr hwnd)
    {
        try
        {
            GetWindowThreadProcessId(hwnd, out var pid);
            if (pid == 0) return null;
            using var proc = Process.GetProcessById((int)pid);
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

    public static bool TryGetMonitorDpi(IntPtr monitor, out uint dpiX, out uint dpiY)
    {
        dpiX = 96;
        dpiY = 96;

        if (monitor == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            return GetDpiForMonitor(monitor, MDT_EFFECTIVE_DPI, out dpiX, out dpiY) == 0;
        }
        catch
        {
            dpiX = 96;
            dpiY = 96;
            return false;
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

