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
    private readonly DispatcherTimer _clockTimer;
    private readonly DispatcherTimer _resourceTimer;
    private ulong _lastIdle;
    private ulong _lastKernel;
    private ulong _lastUser;
    private bool _lastSystemTimeSampled;
    private string _ramDetailsText = string.Empty;
    private bool _isCaffeineActive;
    private bool _reloadConfigOnRecovery;
    private int _recoveryPassesRemaining;
    private bool _itemsPerPageRefreshQueued;
    private bool _syncingEditModeScroll;
    private EdgeHotspotWindow? _edgeHotspot;
    private FileSystemWatcher? _configWatcher;
    private DateTime _suppressConfigWatcherUntilUtc;

    public string ClockTimeString { get; private set; } = "";
    public string ClockDateString { get; private set; } = "";
    public string ClockFullDateTooltip { get; private set; } = "";
    public bool IsClockVisible => _config.ShowClock && !IsEditMode;
    public bool IsClockDateVisible => _config.ShowClockDate;
    public double ClockFontSize => _config.ClockFontSize > 0 ? _config.ClockFontSize : 18;
    public double ClockDateFontSize => Math.Max(9, Math.Round(ClockFontSize * 0.65));

    // Widgets experimentales: Volumen, Multimedia, Monitor de Recursos y Caffeine
    public bool IsVolumeVisible => _config.ShowVolumeControl && !IsEditMode;
    public bool IsMediaVisible => _config.ShowMediaControl && !IsEditMode;
    public bool IsMediaSeekBarVisible => _config.ShowMediaControl && _config.ShowMediaSeekBar && !IsEditMode;
    public double MediaPositionSeconds => MediaService.Instance.PositionSeconds;
    public double MediaDurationSeconds => Math.Max(MediaService.Instance.DurationSeconds, 1.0);
    private string? _scrubbingPositionText = null;
    public string MediaPositionText => _scrubbingPositionText ?? MediaService.Instance.PositionText;
    public string MediaDurationText => MediaService.Instance.DurationText;
    public bool CanSeekMedia => MediaService.Instance.CanSeek && MediaService.Instance.DurationSeconds > 0;
    public bool IsResourceMonitorVisible => _config.ShowResourceMonitor && !IsEditMode;
    public bool IsCaffeineVisible => _config.ShowCaffeine && !IsEditMode;

    private int _cpuUsagePercent;
    public int CpuUsagePercent
    {
        get => _cpuUsagePercent;
        set { if (_cpuUsagePercent != value) { _cpuUsagePercent = value; OnPropertyChanged(); } }
    }

    private string _cpuUsageText = "0%";
    public string CpuUsageText
    {
        get => _cpuUsageText;
        set { if (_cpuUsageText != value) { _cpuUsageText = value; OnPropertyChanged(); } }
    }

    private int _ramUsagePercent;
    public int RamUsagePercent
    {
        get => _ramUsagePercent;
        set { if (_ramUsagePercent != value) { _ramUsagePercent = value; OnPropertyChanged(); } }
    }

    private string _ramUsageText = "0%";
    public string RamUsageText
    {
        get => _ramUsageText;
        private set { if (_ramUsageText != value) { _ramUsageText = value; OnPropertyChanged(); } }
    }

    private string _resourceMonitorTooltip = "";
    public string ResourceMonitorTooltip
    {
        get => _resourceMonitorTooltip;
        private set { if (_resourceMonitorTooltip != value) { _resourceMonitorTooltip = value; OnPropertyChanged(); } }
    }

    public ObservableCollection<GpuDevice> GpuDevices => GpuService.Instance.Devices;

    private string _cpuLabel = "CPU";
    public string CpuLabel
    {
        get => _cpuLabel;
        private set { if (_cpuLabel != value) { _cpuLabel = value; OnPropertyChanged(); } }
    }

    public bool IsCaffeineActive
    {
        get => _isCaffeineActive;
        private set
        {
            if (_isCaffeineActive != value)
            {
                _isCaffeineActive = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CaffeineBrush));
                OnPropertyChanged(nameof(CaffeineTooltip));
            }
        }
    }

    public System.Windows.Media.Brush CaffeineBrush => IsCaffeineActive
        ? (TryFindResource("AppAccentBrush") as System.Windows.Media.Brush ?? System.Windows.Media.Brushes.Orange)
        : DockTextBrush;

    public string CaffeineTooltip => IsCaffeineActive
        ? LocalizationService.Get("Dock_CaffeineActiveTooltip")
        : LocalizationService.Get("Dock_CaffeineInactiveTooltip");

    public double VolumePercentValue
    {
        get => AudioService.Instance.VolumePercent;
        set => AudioService.Instance.SetVolume((float)(value / 100.0));
    }

    public string VolumePercentText => $"{AudioService.Instance.VolumePercent}%";

    public string VolumeTooltip
    {
        get
        {
            var dev = AudioService.Instance.DeviceName;
            var action = AudioService.Instance.IsMuted
                ? LocalizationService.Get("Dock_VolumeUnmute")
                : LocalizationService.Get("Dock_VolumeMute");
            return string.IsNullOrWhiteSpace(dev)
                ? $"{action} ({AudioService.Instance.VolumePercent}%)"
                : $"{dev} • {AudioService.Instance.VolumePercent}% ({action})";
        }
    }

    public string VolumeSpeakerIconData
    {
        get
        {
            if (AudioService.Instance.IsMuted || AudioService.Instance.VolumePercent <= 0)
                return "M3,9v6h4l5,5V4L7,9H3z M16.5,10.5l5,5 M21.5,10.5l-5,5";
            if (AudioService.Instance.VolumePercent < 35)
                return "M3,9v6h4l5,5V4L7,9H3z M15,9.5c0.8,0.7 1.3,1.6 1.3,2.5s-0.5,1.8-1.3,2.5";
            if (AudioService.Instance.VolumePercent < 70)
                return "M3,9v6h4l5,5V4L7,9H3z M15,9.5c0.8,0.7 1.3,1.6 1.3,2.5s-0.5,1.8-1.3,2.5 M17.5,7c1.5,1.3 2.5,3.1 2.5,5s-1,3.7-2.5,5";
            return "M3,9v6h4l5,5V4L7,9H3z M15,9.5c0.8,0.7 1.3,1.6 1.3,2.5s-0.5,1.8-1.3,2.5 M17.5,7c1.5,1.3 2.5,3.1 2.5,5s-1,3.7-2.5,5 M20,4.5c2.3,1.9 3.8,4.7 3.8,7.5s-1.5,5.6-3.8,7.5";
        }
    }

    public string MediaTrackText => MediaService.Instance.HasMedia
        ? MediaService.Instance.FullTrackText
        : LocalizationService.Get("Dock_MediaNoTrack");

    public string MediaPlayPauseIconData => MediaService.Instance.IsPlaying
        ? "M6,5h4v14H6V5z M14,5h4v14h-4V5z"
        : "M8,5v14l11-7L8,5z";

    private bool _isUpdatingVolumeFromService;

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
                    _isHidden = false;
                    _isAnimating = false;
                    BeginAnimation(Window.LeftProperty, null);
                    _preEditWidth = _config.DockWidth <= 0 ? Width : _config.DockWidth;
                    Width = Math.Max(350, _preEditWidth);
                    UpdateLayout();
                    AlignDock(true);
                    QueueDockRealign(true);
                    Dispatcher.BeginInvoke(new Action(UpdateEditModeScrollBar), DispatcherPriority.Loaded);
                }
                else
                {
                    _isAnimating = false;
                    BeginAnimation(Window.LeftProperty, null);
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

        _clockTimer = new DispatcherTimer();
        _clockTimer.Tick += (_, _) => UpdateClockDisplay();

        _resourceTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _resourceTimer.Tick += (_, _) => UpdateResourceDisplay();
        GpuService.Instance.Initialize();
        UpdateHardwareLabels();
        Closed += (_, _) =>
        {
            NativeMethods.SetThreadExecutionState(NativeMethods.EXECUTION_STATE.ES_CONTINUOUS);
            GpuService.Instance.Dispose();
        };

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
        UpdateHardwareLabels();
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
        UpdateWidgetsOrder();
        UpdateClockState();
        UpdateResourceMonitorState();
        UpdateItemsPerPage();
        AlignDock(!_isHidden);
        UpdateVisibleItems();
        ApplyGlassEffect();
        UpdateEdgeHotspotState();
        OnPropertyChanged(nameof(IsVolumeVisible));
        OnPropertyChanged(nameof(IsMediaVisible));
        OnPropertyChanged(nameof(IsMediaSeekBarVisible));
        UpdateMediaTimelineUI();
        OnPropertyChanged(nameof(IsClockVisible));
        OnPropertyChanged(nameof(IsResourceMonitorVisible));
        OnPropertyChanged(nameof(IsCaffeineVisible));
    }

    private void UpdateWidgetsOrder()
    {
        if (ExperimentalWidgetsPanel == null || ClockPanel == null || MediaPanel == null || VolumePanel == null)
        {
            return;
        }

        var order = _config.Experimental?.WidgetOrder ?? new List<string> { "Clock", "Media", "Volume", "Resource", "Caffeine" };
        var panelMap = new Dictionary<string, UIElement>(StringComparer.OrdinalIgnoreCase)
        {
            ["Clock"] = ClockPanel,
            ["Media"] = MediaPanel,
            ["Volume"] = VolumePanel
        };
        if (ResourcePanel != null) panelMap["Resource"] = ResourcePanel;
        if (CaffeinePanel != null) panelMap["Caffeine"] = CaffeinePanel;

        ExperimentalWidgetsPanel.Children.Clear();
        foreach (var key in order)
        {
            if (panelMap.TryGetValue(key, out var element))
            {
                ExperimentalWidgetsPanel.Children.Add(element);
            }
        }
        foreach (var kvp in panelMap)
        {
            if (!ExperimentalWidgetsPanel.Children.Contains(kvp.Value))
            {
                ExperimentalWidgetsPanel.Children.Add(kvp.Value);
            }
        }
    }

    private void UpdateResourceMonitorState()
    {
        if (_config.ShowResourceMonitor)
        {
            if (!_resourceTimer.IsEnabled)
            {
                _resourceTimer.Start();
            }
            UpdateResourceDisplay();
        }
        else
        {
            if (_resourceTimer.IsEnabled)
            {
                _resourceTimer.Stop();
            }
        }
        OnPropertyChanged(nameof(IsResourceMonitorVisible));
    }

    private void UpdateResourceDisplay()
    {
        if (!_config.ShowResourceMonitor) return;

        try
        {
            if (NativeMethods.GetSystemTimes(out var idleFt, out var kernelFt, out var userFt))
            {
                ulong idle = FileTimeToUInt64(idleFt);
                ulong kernel = FileTimeToUInt64(kernelFt);
                ulong user = FileTimeToUInt64(userFt);

                if (_lastSystemTimeSampled)
                {
                    ulong idleDelta = idle - _lastIdle;
                    ulong kernelDelta = kernel - _lastKernel;
                    ulong userDelta = user - _lastUser;
                    ulong totalDelta = kernelDelta + userDelta;

                    if (totalDelta > 0)
                    {
                        double idlePct = (double)idleDelta * 100.0 / totalDelta;
                        double cpuPct = Math.Clamp(100.0 - idlePct, 0.0, 100.0);
                        CpuUsagePercent = (int)Math.Round(cpuPct);
                    }
                }

                _lastIdle = idle;
                _lastKernel = kernel;
                _lastUser = user;
                _lastSystemTimeSampled = true;
            }

            var mem = new NativeMethods.MEMORYSTATUSEX();
            if (NativeMethods.GlobalMemoryStatusEx(mem))
            {
                RamUsagePercent = (int)mem.dwMemoryLoad;
                double usedGb = (mem.ullTotalPhys - mem.ullAvailPhys) / (1024.0 * 1024.0 * 1024.0);
                double totalGb = mem.ullTotalPhys / (1024.0 * 1024.0 * 1024.0);
                _ramDetailsText = $"{usedGb:F1} / {totalGb:F1} GB";
            }

            CpuUsageText = $"{CpuUsagePercent}%";
            RamUsageText = $"{RamUsagePercent}%";
            _ = GpuService.Instance.UpdateUsageAsync();

            var sb = new System.Text.StringBuilder();
            sb.Append(LocalizationService.Get("Dock_ResourceMonitorTooltip"));
            sb.Append($"\nCPU: {CpuUsagePercent}%");
            sb.Append($"\nRAM: {RamUsagePercent}% ({_ramDetailsText})");
            foreach (var gpu in GpuDevices)
            {
                sb.Append($"\n{gpu.Label}: {gpu.UsagePercent}% - {gpu.Name} ({gpu.DedicatedVramText} VRAM)");
            }
            ResourceMonitorTooltip = sb.ToString();
        }
        catch { }
    }

    private static string? _detectedCpuName;
    public static string DetectedCpuName
    {
        get
        {
            if (_detectedCpuName != null) return _detectedCpuName;
            try
            {
                var raw = Microsoft.Win32.Registry.GetValue(
                    @"HKEY_LOCAL_MACHINE\HARDWARE\DESCRIPTION\System\CentralProcessor\0",
                    "ProcessorNameString", null) as string;
                _detectedCpuName = CleanCpuName(raw);
            }
            catch
            {
                _detectedCpuName = "CPU";
            }
            return _detectedCpuName;
        }
    }

    public static string CleanCpuName(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "CPU";
        string clean = raw
            .Replace("(R)", "", StringComparison.OrdinalIgnoreCase)
            .Replace("(TM)", "", StringComparison.OrdinalIgnoreCase)
            .Replace("Processor", "", StringComparison.OrdinalIgnoreCase)
            .Replace("CPU", "", StringComparison.OrdinalIgnoreCase);

        // Strip "with Radeon Graphics", "w/ Radeon Graphics", "with Radeon Vega Graphics", etc. (AMD APUs)
        clean = System.Text.RegularExpressions.Regex.Replace(clean, @"\s+(with|w\/)\s+Radeon.*", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        // Strip core counts like "8-Core", "6-Core", "16-Core", "Dual-Core", "Quad-Core" (typical on AMD)
        clean = System.Text.RegularExpressions.Regex.Replace(clean, @"\s+\d+-Core.*", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        clean = System.Text.RegularExpressions.Regex.Replace(clean, @"\s+(Dual|Triple|Quad|Hexa|Octa)-Core.*", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        // Strip Intel Gen prefix
        clean = System.Text.RegularExpressions.Regex.Replace(clean, @"^\s*\d+(th|st|nd|rd)\s+Gen\s+", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        // Strip leading brand name if redundant
        clean = clean.Replace("Intel ", "", StringComparison.OrdinalIgnoreCase)
                     .Replace("AMD ", "", StringComparison.OrdinalIgnoreCase);

        clean = System.Text.RegularExpressions.Regex.Replace(clean, @"\s+", " ").Trim();

        // Shorten Intel Core designations
        clean = clean.Replace("Core 5 ", "i5 ", StringComparison.OrdinalIgnoreCase)
                     .Replace("Core 7 ", "i7 ", StringComparison.OrdinalIgnoreCase)
                     .Replace("Core 3 ", "i3 ", StringComparison.OrdinalIgnoreCase)
                     .Replace("Core 9 ", "i9 ", StringComparison.OrdinalIgnoreCase)
                     .Replace("Core i", "i", StringComparison.OrdinalIgnoreCase);

        return string.IsNullOrEmpty(clean) ? "CPU" : clean;
    }

    public void UpdateHardwareLabels()
    {
        CpuLabel = _config.ShowHardwareModelNames ? DetectedCpuName : "CPU";

        var gpus = GpuDevices;
        for (int i = 0; i < gpus.Count; i++)
        {
            gpus[i].UpdateLabel(_config.ShowHardwareModelNames);
        }
    }

    private static ulong FileTimeToUInt64(System.Runtime.InteropServices.ComTypes.FILETIME ft)
    {
        return ((ulong)(uint)ft.dwHighDateTime << 32) | (uint)ft.dwLowDateTime;
    }

    private void ResourcePanel_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "taskmgr.exe",
                UseShellExecute = true
            });
        }
        catch { }
    }

    private void CaffeinePanel_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        ToggleCaffeine();
    }

    public void ToggleCaffeine()
    {
        IsCaffeineActive = !IsCaffeineActive;
        if (IsCaffeineActive)
        {
            NativeMethods.SetThreadExecutionState(NativeMethods.EXECUTION_STATE.ES_CONTINUOUS | NativeMethods.EXECUTION_STATE.ES_DISPLAY_REQUIRED | NativeMethods.EXECUTION_STATE.ES_SYSTEM_REQUIRED);
        }
        else
        {
            NativeMethods.SetThreadExecutionState(NativeMethods.EXECUTION_STATE.ES_CONTINUOUS);
        }
    }

    private void UpdateClockState()
    {
        if (_config.ShowClock)
        {
            _clockTimer.Interval = _config.ShowClockSeconds
                ? TimeSpan.FromMilliseconds(500)
                : TimeSpan.FromSeconds(1);
            if (!_clockTimer.IsEnabled)
            {
                _clockTimer.Start();
            }
            UpdateClockDisplay();
        }
        else
        {
            if (_clockTimer.IsEnabled)
            {
                _clockTimer.Stop();
            }
        }
        OnPropertyChanged(nameof(IsClockVisible));
        OnPropertyChanged(nameof(IsClockDateVisible));
        OnPropertyChanged(nameof(ClockFontSize));
        OnPropertyChanged(nameof(ClockDateFontSize));
    }

    private void UpdateClockDisplay()
    {
        var now = DateTime.Now;
        var timeFormat = _config.ClockFormat24H ? "HH:mm" : "hh:mm tt";
        if (_config.ShowClockSeconds)
        {
            timeFormat = _config.ClockFormat24H ? "HH:mm:ss" : "hh:mm:ss tt";
        }
        ClockTimeString = now.ToString(timeFormat);
        ClockDateString = now.ToString("ddd, d MMM");
        ClockFullDateTooltip = now.ToLongDateString();
        OnPropertyChanged(nameof(ClockTimeString));
        OnPropertyChanged(nameof(ClockDateString));
        OnPropertyChanged(nameof(ClockFullDateTooltip));
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

        AudioService.Instance.VolumeChanged += AudioService_VolumeChanged;
        MediaService.Instance.MediaStateChanged += MediaService_MediaStateChanged;
        MediaService.Instance.TimelineChanged += MediaService_TimelineChanged;

        SetupMediaSeekSlider();
        _ = RefreshInitialMediaStateAsync();
    }

    private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isUpdatingVolumeFromService) return;
        AudioService.Instance.SetVolume((float)(e.NewValue / 100.0));
    }

    private void VolumeMute_Click(object sender, RoutedEventArgs e)
    {
        AudioService.Instance.ToggleMute();
    }

    private void VolumePanel_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        AudioService.Instance.ChangeVolumeRelative(e.Delta > 0 ? 0.02f : -0.02f);
        e.Handled = true;
    }

    private void VolumePanel_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "ms-settings:sound",
                UseShellExecute = true
            });
            e.Handled = true;
        }
        catch { }
    }

    private void AudioService_VolumeChanged(object? sender, EventArgs e)
    {
        _isUpdatingVolumeFromService = true;
        try
        {
            OnPropertyChanged(nameof(VolumePercentValue));
            OnPropertyChanged(nameof(VolumePercentText));
            OnPropertyChanged(nameof(VolumeTooltip));
            OnPropertyChanged(nameof(VolumeSpeakerIconData));
        }
        finally
        {
            _isUpdatingVolumeFromService = false;
        }
    }

    private void MediaPlayPause_Click(object sender, RoutedEventArgs e)
    {
        _ = MediaService.Instance.TogglePlayPauseAsync();
    }

    private void MediaNext_Click(object sender, RoutedEventArgs e)
    {
        _ = MediaService.Instance.SkipNextAsync();
    }

    private void MediaPrev_Click(object sender, RoutedEventArgs e)
    {
        _ = MediaService.Instance.SkipPreviousAsync();
    }

    private bool _isUserSeekingMedia = false;
    private bool _isUpdatingSliderFromService = false;

    private void SetupMediaSeekSlider()
    {
        if (MediaSeekSlider == null) return;

        MediaSeekSlider.AddHandler(System.Windows.Controls.Primitives.Thumb.DragStartedEvent, new System.Windows.Controls.Primitives.DragStartedEventHandler((s, e) =>
        {
            _isUserSeekingMedia = true;
        }));

        MediaSeekSlider.AddHandler(System.Windows.Controls.Primitives.Thumb.DragCompletedEvent, new System.Windows.Controls.Primitives.DragCompletedEventHandler((s, e) =>
        {
            FinishMediaSeeking();
        }));

        MediaSeekSlider.AddHandler(UIElement.PreviewMouseLeftButtonDownEvent, new MouseButtonEventHandler((s, e) =>
        {
            _isUserSeekingMedia = true;
        }), true);

        MediaSeekSlider.AddHandler(UIElement.PreviewMouseLeftButtonUpEvent, new MouseButtonEventHandler((s, e) =>
        {
            FinishMediaSeeking();
        }), true);

        MediaSeekSlider.LostMouseCapture += (s, e) =>
        {
            FinishMediaSeeking();
        };
    }

    private async Task RefreshInitialMediaStateAsync()
    {
        try
        {
            await MediaService.Instance.RefreshCurrentSessionAsync(true);
        }
        catch { }

        Dispatcher.Invoke(() =>
        {
            MediaService_MediaStateChanged(null, EventArgs.Empty);
            UpdateMediaTimelineUI();
        });
    }

    private void UpdateMediaTimelineUI()
    {
        OnPropertyChanged(nameof(MediaPositionSeconds));
        OnPropertyChanged(nameof(MediaDurationSeconds));
        OnPropertyChanged(nameof(MediaPositionText));
        OnPropertyChanged(nameof(MediaDurationText));
        OnPropertyChanged(nameof(CanSeekMedia));

        if (MediaSeekSlider != null && !_isUserSeekingMedia)
        {
            _isUpdatingSliderFromService = true;
            try
            {
                if (MediaService.Instance.DurationSeconds > 0)
                {
                    MediaSeekSlider.Minimum = 0;
                    double max = MediaService.Instance.DurationSeconds;
                    MediaSeekSlider.Maximum = max;
                    double pos = Math.Clamp(MediaService.Instance.PositionSeconds, 0, max);
                    MediaSeekSlider.Value = pos;
                    MediaSeekSlider.IsEnabled = CanSeekMedia;
                }
                else
                {
                    MediaSeekSlider.Minimum = 0;
                    MediaSeekSlider.Maximum = 100;
                    MediaSeekSlider.Value = 0;
                    MediaSeekSlider.IsEnabled = false;
                }
            }
            finally
            {
                _isUpdatingSliderFromService = false;
            }
        }
    }

    private void MediaService_MediaStateChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(MediaTrackText));
        OnPropertyChanged(nameof(MediaPlayPauseIconData));
        UpdateMediaTimelineUI();
    }

    private void MediaService_TimelineChanged(object? sender, EventArgs e)
    {
        if (_isUserSeekingMedia) return;

        Dispatcher.BeginInvoke(() =>
        {
            if (_isUserSeekingMedia) return;
            UpdateMediaTimelineUI();
        });
    }

    private void MediaSeekSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isUpdatingSliderFromService) return;

        if (_isUserSeekingMedia)
        {
            _scrubbingPositionText = MediaService.FormatTime(TimeSpan.FromSeconds(e.NewValue));
            OnPropertyChanged(nameof(MediaPositionText));
        }
    }

    private void MediaSeekSlider_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        e.Handled = true;
        if (!CanSeekMedia) return;
        double delta = e.Delta > 0 ? 5.0 : -5.0;
        double newPos = Math.Clamp(MediaService.Instance.PositionSeconds + delta, 0, MediaService.Instance.DurationSeconds);
        _ = MediaService.Instance.SeekAsync(newPos);
    }

    private void FinishMediaSeeking()
    {
        if (_isUserSeekingMedia)
        {
            _isUserSeekingMedia = false;
            _scrubbingPositionText = null;
            if (MediaSeekSlider != null)
            {
                double targetSec = MediaSeekSlider.Value;
                _ = MediaService.Instance.SeekAsync(targetSec);
            }
            OnPropertyChanged(nameof(MediaPositionText));
        }
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
        var targetLeft = showState ? GetShownLeft(area) : GetHiddenLeft(area);
        BeginAnimation(Window.LeftProperty, null);
        Left = targetLeft;
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
        if (!double.IsNaN(Width) && Width > 0)
        {
            return Width;
        }
        return ActualWidth > 0 ? ActualWidth : 175;
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
            var area = GetMonitorBounds();
            var targetLeft = !_isHidden ? GetShownLeft(area) : GetHiddenLeft(area);
            BeginAnimation(Window.LeftProperty, null);
            Left = targetLeft;
            UpdateEdgeHotspotState();
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

        _edgeHotspot.ShowOnEdge(GetMonitorBounds(), _dockSide, _config.EdgeTriggerPx > 0 ? _config.EdgeTriggerPx : 8);
    }

    private bool IsMouseOverDock()
    {
        if (IsMouseOver)
        {
            return true;
        }

        try
        {
            if (NativeMethods.GetCursorPos(out var pt))
            {
                var relativePoint = PointFromScreen(new System.Windows.Point(pt.X, pt.Y));
                if (relativePoint.X >= 0 && relativePoint.X <= ActualWidth &&
                    relativePoint.Y >= 0 && relativePoint.Y <= ActualHeight)
                {
                    return true;
                }
            }
        }
        catch
        {
        }

        return false;
    }

    public void OpenSettings()
    {
        var originalConfig = _config.Clone();
        var draft = _config.Clone();
        var settings = new SettingsWindow(draft)
        {
            Owner = this,
            WindowStartupLocation = WindowStartupLocation.Manual
        };

        var monitor = GetMonitorBounds();
        settings.Left = monitor.Left + Math.Max(0, (monitor.Width - settings.Width) / 2);
        settings.Top = monitor.Top + Math.Max(0, (monitor.Height - settings.Height) / 2);

        settings.OnApplyPreview = (appliedDraft) =>
        {
            ApplyConfigState(appliedDraft);
        };

        settings.OnRevertPreview = (baseline) =>
        {
            ApplyConfigState(baseline);
        };

        settings.OnSaveCommitted = (committedDraft) =>
        {
            ApplyAndSaveConfig(committedDraft);
            originalConfig = committedDraft.Clone();
        };

        try
        {
            if (settings.ShowDialog() == true)
            {
                ApplyAndSaveConfig(draft);
            }
            else
            {
                ApplyConfigState(originalConfig);
            }
        }
        finally
        {
            if (IsEditMode)
            {
                IsEditMode = false;
            }

            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!IsMouseOverDock())
                {
                    StartHideTimer();
                }
            }), DispatcherPriority.Input);
        }
    }

    private static DockConfig CloneConfig(DockConfig source) => source.Clone();

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
        AudioService.Instance.VolumeChanged -= AudioService_VolumeChanged;
        MediaService.Instance.MediaStateChanged -= MediaService_MediaStateChanged;
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
            var clockChrome = _config.ShowClock ? 48.0 : 0.0;
            var volumeChrome = _config.ShowVolumeControl ? 36.0 : 0.0;
            var mediaChrome = _config.ShowMediaControl ? (_config.ShowMediaSeekBar ? 86.0 : 64.0) : 0.0;
            var usableHeight = Math.Max(1, monitorHeight - fallbackChromeWithoutPagination - clockChrome - volumeChrome - mediaChrome);
            var count = Math.Max(1, (int)Math.Floor(usableHeight / perItem));

            if (Shortcuts.Count > count)
            {
                usableHeight = Math.Max(1, monitorHeight - fallbackChromeWithPagination - clockChrome - volumeChrome - mediaChrome);
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
        OnPropertyChanged(nameof(IsClockVisible));
        OnPropertyChanged(nameof(IsVolumeVisible));
        OnPropertyChanged(nameof(IsMediaVisible));
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

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetCursorPos(out POINT lpPoint);

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
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

    [Flags]
    public enum EXECUTION_STATE : uint
    {
        ES_AWAYMODE_REQUIRED = 0x00000040,
        ES_CONTINUOUS = 0x80000000,
        ES_DISPLAY_REQUIRED = 0x00000002,
        ES_SYSTEM_REQUIRED = 0x00000001
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern EXECUTION_STATE SetThreadExecutionState(EXECUTION_STATE esFlags);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public class MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;

        public MEMORYSTATUSEX()
        {
            dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetSystemTimes(out System.Runtime.InteropServices.ComTypes.FILETIME lpIdleTime, out System.Runtime.InteropServices.ComTypes.FILETIME lpKernelTime, out System.Runtime.InteropServices.ComTypes.FILETIME lpUserTime);
}

