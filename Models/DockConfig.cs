using System.Collections.Generic;

namespace DockBar.Models;

public class DockConfig
{
    public DockSide DockSide { get; set; } = DockSide.Left;
    public List<ShortcutItem> Shortcuts { get; set; } = new();
    public double DockWidth { get; set; } = 175;
    public double IconSize { get; set; } = 40;
    public double AutoHideDelaySeconds { get; set; } = 0; // Win8-style immediate hide on leave by default
    public double HideAnimationMs { get; set; } = 200;
    public bool UseTransparency { get; set; } = true;
    public double BackgroundOpacity { get; set; } = 0.85;
    public byte BackgroundR { get; set; } = 0;
    public byte BackgroundG { get; set; } = 0;
    public byte BackgroundB { get; set; } = 0;
    public bool UseLightText { get; set; } = true;
    public bool AutoStartEnabled { get; set; } = false;
    public bool AutoStartPrompted { get; set; } = false;
}
