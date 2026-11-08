using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace DockBar.Models;

public class ClockConfig
{
    public bool ShowClock { get; set; } = false;
    public double ClockFontSize { get; set; } = 18;
    public bool ClockFormat24H { get; set; } = true;
    public bool ShowClockSeconds { get; set; } = false;
    public bool ShowClockDate { get; set; } = true;
}

public class MediaConfig
{
    public bool ShowVolumeControl { get; set; } = false;
    public bool ShowMediaControl { get; set; } = false;
    public bool ShowMediaSeekBar { get; set; } = false;
    public bool ShowMediaThumbnail { get; set; } = false;
    public bool MediaThumbnailOnly { get; set; } = false;
}

public class AiProviderConfig : INotifyPropertyChanged
{
    private bool _enabled = false;
    private bool _show5Hr = true;
    private bool _showWeek = true;
    private bool _useAutoDetect = true;
    private string _webToken = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool Enabled
    {
        get => _enabled;
        set
        {
            if (_enabled != value)
            {
                _enabled = value;
                OnPropertyChanged();
            }
        }
    }

    public bool Show5Hr
    {
        get => _show5Hr;
        set
        {
            if (_show5Hr != value)
            {
                _show5Hr = value;
                OnPropertyChanged();
            }
        }
    }

    public bool ShowWeek
    {
        get => _showWeek;
        set
        {
            if (_showWeek != value)
            {
                _showWeek = value;
                OnPropertyChanged();
            }
        }
    }

    public bool UseAutoDetect
    {
        get => _useAutoDetect;
        set
        {
            if (_useAutoDetect != value)
            {
                _useAutoDetect = value;
                OnPropertyChanged();
            }
        }
    }

    public string WebToken
    {
        get => _webToken;
        set
        {
            var normalized = value ?? string.Empty;
            if (_webToken != normalized)
            {
                _webToken = normalized;
                OnPropertyChanged();
            }
        }
    }

    public AiProviderConfig Clone() => new()
    {
        Enabled = Enabled,
        Show5Hr = Show5Hr,
        ShowWeek = ShowWeek,
        UseAutoDetect = UseAutoDetect,
        WebToken = WebToken ?? string.Empty
    };

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public class AiConfig : INotifyPropertyChanged
{
    private bool _showAiQuota = false;
    private int _refreshIntervalSeconds = 60;

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool ShowAiQuota
    {
        get => _showAiQuota;
        set
        {
            if (_showAiQuota != value)
            {
                _showAiQuota = value;
                OnPropertyChanged();
            }
        }
    }

    public int RefreshIntervalSeconds
    {
        get => _refreshIntervalSeconds;
        set
        {
            if (_refreshIntervalSeconds != value)
            {
                _refreshIntervalSeconds = value;
                OnPropertyChanged();
            }
        }
    }

    public AiProviderConfig Antigravity { get; set; } = new() { Enabled = true, Show5Hr = true, ShowWeek = false, UseAutoDetect = true };
    public AiProviderConfig Gemini { get; set; } = new() { Enabled = false, Show5Hr = true, ShowWeek = true, UseAutoDetect = true };
    public AiProviderConfig Claude { get; set; } = new() { Enabled = false, Show5Hr = true, ShowWeek = true, UseAutoDetect = true };
    public AiProviderConfig ChatGpt { get; set; } = new() { Enabled = false, Show5Hr = true, ShowWeek = true, UseAutoDetect = true };

    public AiConfig Clone() => new()
    {
        ShowAiQuota = ShowAiQuota,
        RefreshIntervalSeconds = RefreshIntervalSeconds,
        Antigravity = (Antigravity ?? new()).Clone(),
        Gemini = (Gemini ?? new()).Clone(),
        Claude = (Claude ?? new()).Clone(),
        ChatGpt = (ChatGpt ?? new()).Clone()
    };

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public class ExperimentalConfig
{
    public double EdgeTriggerPx { get; set; } = 8;
    public bool ShowResourceMonitor { get; set; } = false;
    public bool ShowHardwareModelNames { get; set; } = false;
    public bool ShowPowerControl { get; set; } = false;
    public bool ShowCaffeine { get; set; } = false;
    public bool UseSliderPagination { get; set; } = false;
    public List<string> WidgetOrder { get; set; } = new() { "Clock", "Media", "Volume", "Resource", "AiQuota", "Power", "Caffeine", "Pagination" };
}

public class DockConfig
{
    public DockSide DockSide { get; set; } = DockSide.Left;
    public List<ShortcutItem> Shortcuts { get; set; } = new();
    public double DockWidth { get; set; } = 175;
    public double IconSize { get; set; } = 40;
    public double TopDockHeight { get; set; } = 118;
    public double TopIconSize { get; set; } = 40;
    public double AutoHideDelaySeconds { get; set; } = 0; // Win8-style immediate hide on leave by default
    public bool AlwaysShow { get; set; } = false;
    public double HideAnimationMs { get; set; } = 200;
    public bool UseTransparency { get; set; } = true;
    public double BackgroundOpacity { get; set; } = 0.45;
    public byte BackgroundR { get; set; } = 0;
    public byte BackgroundG { get; set; } = 0;
    public byte BackgroundB { get; set; } = 0;
    public byte AccentR { get; set; } = 55;
    public byte AccentG { get; set; } = 115;
    public byte AccentB { get; set; } = 245;
    public bool UseLightText { get; set; } = true;
    public bool EnableTextShadow { get; set; } = true;
    public bool AutoStartEnabled { get; set; } = false;
    public bool AutoStartPrompted { get; set; } = false;

    // Categorías de configuración dedicadas
    public ClockConfig Clock { get; set; } = new();
    public MediaConfig Media { get; set; } = new();
    public AiConfig Ai { get; set; } = new();
    public ExperimentalConfig Experimental { get; set; } = new();

    [JsonIgnore]
    public bool ShowAiQuota
    {
        get => Ai.ShowAiQuota;
        set => Ai.ShowAiQuota = value;
    }

    // Propiedades puente para enlaces directos en XAML y compatibilidad transparente
    [JsonIgnore]
    public bool ShowClock
    {
        get => Clock.ShowClock;
        set => Clock.ShowClock = value;
    }

    [JsonIgnore]
    public double ClockFontSize
    {
        get => Clock.ClockFontSize;
        set => Clock.ClockFontSize = value;
    }

    [JsonIgnore]
    public bool ClockFormat24H
    {
        get => Clock.ClockFormat24H;
        set => Clock.ClockFormat24H = value;
    }

    [JsonIgnore]
    public bool ShowClockSeconds
    {
        get => Clock.ShowClockSeconds;
        set => Clock.ShowClockSeconds = value;
    }

    [JsonIgnore]
    public bool ShowClockDate
    {
        get => Clock.ShowClockDate;
        set => Clock.ShowClockDate = value;
    }

    [JsonIgnore]
    public bool ShowVolumeControl
    {
        get => Media.ShowVolumeControl;
        set => Media.ShowVolumeControl = value;
    }

    [JsonIgnore]
    public bool ShowMediaControl
    {
        get => Media.ShowMediaControl;
        set => Media.ShowMediaControl = value;
    }

    [JsonIgnore]
    public bool ShowMediaSeekBar
    {
        get => Media.ShowMediaSeekBar;
        set => Media.ShowMediaSeekBar = value;
    }

    [JsonIgnore]
    public bool ShowMediaThumbnail
    {
        get => Media.ShowMediaThumbnail;
        set => Media.ShowMediaThumbnail = value;
    }

    [JsonIgnore]
    public bool MediaThumbnailOnly
    {
        get => Media.MediaThumbnailOnly;
        set => Media.MediaThumbnailOnly = value;
    }

    [JsonIgnore]
    public double EdgeTriggerPx
    {
        get => Experimental.EdgeTriggerPx;
        set => Experimental.EdgeTriggerPx = value;
    }

    [JsonIgnore]
    public bool ShowResourceMonitor
    {
        get => Experimental.ShowResourceMonitor;
        set => Experimental.ShowResourceMonitor = value;
    }

    [JsonIgnore]
    public bool ShowHardwareModelNames
    {
        get => Experimental.ShowHardwareModelNames;
        set => Experimental.ShowHardwareModelNames = value;
    }

    [JsonIgnore]
    public bool ShowPowerControl
    {
        get => Experimental.ShowPowerControl;
        set => Experimental.ShowPowerControl = value;
    }

    [JsonIgnore]
    public bool ShowCaffeine
    {
        get => Experimental.ShowCaffeine;
        set => Experimental.ShowCaffeine = value;
    }

    [JsonIgnore]
    public bool UseSliderPagination
    {
        get => Experimental.UseSliderPagination;
        set => Experimental.UseSliderPagination = value;
    }

    [JsonIgnore]
    public List<string> WidgetOrder
    {
        get => Experimental.WidgetOrder;
        set => Experimental.WidgetOrder = value;
    }

    [JsonIgnore]
    public double ActiveDockSize
    {
        get => DockSide == DockSide.Top ? TopDockHeight : DockWidth;
        set
        {
            if (DockSide == DockSide.Top)
            {
                TopDockHeight = value;
            }
            else
            {
                DockWidth = value;
            }
        }
    }

    [JsonIgnore]
    public double ActiveIconSize
    {
        get => DockSide == DockSide.Top ? TopIconSize : IconSize;
        set
        {
            if (DockSide == DockSide.Top)
            {
                TopIconSize = value;
            }
            else
            {
                IconSize = value;
            }
        }
    }

    public DockConfig Clone()
    {
        var shortcuts = Shortcuts ?? new();
        return new DockConfig
        {
            DockSide = DockSide,
            DockWidth = DockWidth,
            IconSize = IconSize,
            TopDockHeight = TopDockHeight,
            TopIconSize = TopIconSize,
            AutoHideDelaySeconds = AutoHideDelaySeconds,
            AlwaysShow = AlwaysShow,
            HideAnimationMs = HideAnimationMs,
            UseTransparency = UseTransparency,
            BackgroundOpacity = BackgroundOpacity,
            BackgroundR = BackgroundR,
            BackgroundG = BackgroundG,
            BackgroundB = BackgroundB,
            AccentR = AccentR,
            AccentG = AccentG,
            AccentB = AccentB,
            UseLightText = UseLightText,
            EnableTextShadow = EnableTextShadow,
            AutoStartEnabled = AutoStartEnabled,
            AutoStartPrompted = AutoStartPrompted,
            Clock = new ClockConfig
            {
                ShowClock = ShowClock,
                ClockFontSize = ClockFontSize,
                ClockFormat24H = ClockFormat24H,
                ShowClockSeconds = ShowClockSeconds,
                ShowClockDate = ShowClockDate
            },
            Media = new MediaConfig
            {
                ShowVolumeControl = ShowVolumeControl,
                ShowMediaControl = ShowMediaControl,
                ShowMediaSeekBar = ShowMediaSeekBar,
                ShowMediaThumbnail = ShowMediaThumbnail,
                MediaThumbnailOnly = MediaThumbnailOnly
            },
            Ai = (Ai ?? new()).Clone(),
            Experimental = new ExperimentalConfig
            {
                EdgeTriggerPx = EdgeTriggerPx,
                ShowResourceMonitor = ShowResourceMonitor,
                ShowHardwareModelNames = ShowHardwareModelNames,
                ShowPowerControl = ShowPowerControl,
                ShowCaffeine = ShowCaffeine,
                UseSliderPagination = UseSliderPagination,
                WidgetOrder = WidgetOrder != null ? new List<string>(WidgetOrder) : new() { "Clock", "Media", "Volume", "Resource", "AiQuota", "Power", "Caffeine", "Pagination" }
            },
            Shortcuts = shortcuts.Select(s => new ShortcutItem
            {
                Name = s.Name,
                Path = s.Path,
                Arguments = s.Arguments,
                IconPath = s.IconPath
            }).ToList()
        };
    }

    public void CopyFrom(DockConfig source)
    {
        DockSide = source.DockSide;
        DockWidth = source.DockWidth;
        IconSize = source.IconSize;
        TopDockHeight = source.TopDockHeight;
        TopIconSize = source.TopIconSize;
        AutoHideDelaySeconds = source.AutoHideDelaySeconds;
        AlwaysShow = source.AlwaysShow;
        HideAnimationMs = source.HideAnimationMs;
        UseTransparency = source.UseTransparency;
        BackgroundOpacity = source.BackgroundOpacity;
        BackgroundR = source.BackgroundR;
        BackgroundG = source.BackgroundG;
        BackgroundB = source.BackgroundB;
        AccentR = source.AccentR;
        AccentG = source.AccentG;
        AccentB = source.AccentB;
        UseLightText = source.UseLightText;
        EnableTextShadow = source.EnableTextShadow;
        AutoStartEnabled = source.AutoStartEnabled;
        AutoStartPrompted = source.AutoStartPrompted;

        ShowClock = source.ShowClock;
        ClockFontSize = source.ClockFontSize;
        ClockFormat24H = source.ClockFormat24H;
        ShowClockSeconds = source.ShowClockSeconds;
        ShowClockDate = source.ShowClockDate;

        ShowVolumeControl = source.ShowVolumeControl;
        ShowMediaControl = source.ShowMediaControl;
        ShowMediaSeekBar = source.ShowMediaSeekBar;
        ShowMediaThumbnail = source.ShowMediaThumbnail;
        MediaThumbnailOnly = source.MediaThumbnailOnly;

        Ai = (source.Ai ?? new()).Clone();

        EdgeTriggerPx = source.EdgeTriggerPx;
        ShowResourceMonitor = source.ShowResourceMonitor;
        ShowHardwareModelNames = source.ShowHardwareModelNames;
        ShowPowerControl = source.ShowPowerControl;
        ShowCaffeine = source.ShowCaffeine;
        UseSliderPagination = source.UseSliderPagination;
        WidgetOrder = source.WidgetOrder != null ? new List<string>(source.WidgetOrder) : new() { "Clock", "Media", "Volume", "Resource", "AiQuota", "Power", "Caffeine", "Pagination" };

        if (source.Shortcuts != null)
        {
            Shortcuts = source.Shortcuts.Select(s => new ShortcutItem
            {
                Name = s.Name,
                Path = s.Path,
                Arguments = s.Arguments,
                IconPath = s.IconPath
            }).ToList();
        }
    }
}
