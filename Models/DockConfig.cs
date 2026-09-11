using System;
using System.Collections.Generic;
using System.Linq;
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
}

public class ExperimentalConfig
{
    public double EdgeTriggerPx { get; set; } = 8;
    public bool ShowResourceMonitor { get; set; } = false;
    public bool ShowHardwareModelNames { get; set; } = false;
    public bool ShowCaffeine { get; set; } = false;
    public List<string> WidgetOrder { get; set; } = new() { "Clock", "Media", "Volume", "Resource", "Caffeine" };
}

public class DockConfig
{
    public DockSide DockSide { get; set; } = DockSide.Left;
    public List<ShortcutItem> Shortcuts { get; set; } = new();
    public double DockWidth { get; set; } = 175;
    public double IconSize { get; set; } = 40;
    public double AutoHideDelaySeconds { get; set; } = 0; // Win8-style immediate hide on leave by default
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
    public ExperimentalConfig Experimental { get; set; } = new();

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
    public bool ShowCaffeine
    {
        get => Experimental.ShowCaffeine;
        set => Experimental.ShowCaffeine = value;
    }

    [JsonIgnore]
    public List<string> WidgetOrder
    {
        get => Experimental.WidgetOrder;
        set => Experimental.WidgetOrder = value;
    }

    public DockConfig Clone()
    {
        var shortcuts = Shortcuts ?? new();
        return new DockConfig
        {
            DockSide = DockSide,
            DockWidth = DockWidth,
            IconSize = IconSize,
            AutoHideDelaySeconds = AutoHideDelaySeconds,
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
                ShowMediaSeekBar = ShowMediaSeekBar
            },
            Experimental = new ExperimentalConfig
            {
                EdgeTriggerPx = EdgeTriggerPx,
                ShowResourceMonitor = ShowResourceMonitor,
                ShowHardwareModelNames = ShowHardwareModelNames,
                ShowCaffeine = ShowCaffeine,
                WidgetOrder = WidgetOrder != null ? new List<string>(WidgetOrder) : new() { "Clock", "Media", "Volume", "Resource", "Caffeine" }
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
        AutoHideDelaySeconds = source.AutoHideDelaySeconds;
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

        EdgeTriggerPx = source.EdgeTriggerPx;
        ShowResourceMonitor = source.ShowResourceMonitor;
        ShowHardwareModelNames = source.ShowHardwareModelNames;
        ShowCaffeine = source.ShowCaffeine;
        WidgetOrder = source.WidgetOrder != null ? new List<string>(source.WidgetOrder) : new() { "Clock", "Media", "Volume", "Resource", "Caffeine" };

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
