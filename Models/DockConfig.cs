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
}
