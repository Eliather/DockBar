using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DockBar.Models;

public class ExperimentalConfig
{
    public bool ShowClock { get; set; } = false;
    public double ClockFontSize { get; set; } = 18;
    public bool ClockFormat24H { get; set; } = true;
    public bool ShowClockSeconds { get; set; } = false;
    public bool ShowClockDate { get; set; } = true;
    public double EdgeTriggerPx { get; set; } = 8;
    public bool ShowVolumeControl { get; set; } = false;
    public bool ShowMediaControl { get; set; } = false;
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

    // Sección aislada de configuraciones experimentales
    public ExperimentalConfig Experimental { get; set; } = new();

    // Propiedades puente para enlaces directos en XAML y lógica interna
    [JsonIgnore]
    public bool ShowClock
    {
        get => Experimental.ShowClock;
        set => Experimental.ShowClock = value;
    }

    [JsonIgnore]
    public double ClockFontSize
    {
        get => Experimental.ClockFontSize;
        set => Experimental.ClockFontSize = value;
    }

    [JsonIgnore]
    public bool ClockFormat24H
    {
        get => Experimental.ClockFormat24H;
        set => Experimental.ClockFormat24H = value;
    }

    [JsonIgnore]
    public bool ShowClockSeconds
    {
        get => Experimental.ShowClockSeconds;
        set => Experimental.ShowClockSeconds = value;
    }

    [JsonIgnore]
    public bool ShowClockDate
    {
        get => Experimental.ShowClockDate;
        set => Experimental.ShowClockDate = value;
    }

    [JsonIgnore]
    public double EdgeTriggerPx
    {
        get => Experimental.EdgeTriggerPx;
        set => Experimental.EdgeTriggerPx = value;
    }

    [JsonIgnore]
    public bool ShowVolumeControl
    {
        get => Experimental.ShowVolumeControl;
        set => Experimental.ShowVolumeControl = value;
    }

    [JsonIgnore]
    public bool ShowMediaControl
    {
        get => Experimental.ShowMediaControl;
        set => Experimental.ShowMediaControl = value;
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
            Experimental = new ExperimentalConfig
            {
                ShowClock = ShowClock,
                ClockFontSize = ClockFontSize,
                ClockFormat24H = ClockFormat24H,
                ShowClockSeconds = ShowClockSeconds,
                ShowClockDate = ShowClockDate,
                EdgeTriggerPx = EdgeTriggerPx,
                ShowVolumeControl = ShowVolumeControl,
                ShowMediaControl = ShowMediaControl
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
        EdgeTriggerPx = source.EdgeTriggerPx;
        ShowVolumeControl = source.ShowVolumeControl;
        ShowMediaControl = source.ShowMediaControl;

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

