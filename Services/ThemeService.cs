using System;
using System.Windows;
using System.Windows.Media;
using DockBar.Models;
using Color = System.Windows.Media.Color;

namespace DockBar.Services;

public static class ThemeService
{
    private static DockConfig? _lastAppliedConfig;

    public static DockConfig? CurrentConfig => _lastAppliedConfig;

    /// <summary>
    /// Applies dynamic theme brushes to Application.Current.Resources based on DockConfig.
    /// </summary>
    public static void Apply(DockConfig config)
    {
        _lastAppliedConfig = config ?? new DockConfig();

        var app = System.Windows.Application.Current;
        if (app == null)
        {
            return;
        }

        var r = _lastAppliedConfig.BackgroundR;
        var g = _lastAppliedConfig.BackgroundG;
        var b = _lastAppliedConfig.BackgroundB;
        var isLightText = _lastAppliedConfig.UseLightText;
        var useTransparency = _lastAppliedConfig.UseTransparency;
        var rawOpacity = useTransparency ? Math.Clamp(_lastAppliedConfig.BackgroundOpacity, 0.0, 1.0) : 1.0;

        // Base canvas & window background: exact user color & opacity matching DockBar
        var canvasAlpha = (byte)Math.Clamp((int)Math.Round(rawOpacity * 255), 0, 255);
        var canvasColor = Color.FromArgb(canvasAlpha, r, g, b);
        var canvasBrush = CreateFrozenBrush(canvasColor);

        // Surfaces / Cards: Semi-translucent frosted glass
        byte surfaceAlpha = isLightText ? (byte)24 : (byte)20;
        var surfaceColor = isLightText
            ? Color.FromArgb(surfaceAlpha, 255, 255, 255)
            : Color.FromArgb(surfaceAlpha, 0, 0, 0);
        var surfaceBrush = CreateFrozenBrush(surfaceColor);

        byte surfaceRaisedAlpha = isLightText ? (byte)45 : (byte)35;
        var surfaceRaisedColor = isLightText
            ? Color.FromArgb(surfaceRaisedAlpha, 255, 255, 255)
            : Color.FromArgb(surfaceRaisedAlpha, 0, 0, 0);
        var surfaceRaisedBrush = CreateFrozenBrush(surfaceRaisedColor);

        // Inset Panels
        byte panelAlpha = isLightText ? (byte)18 : (byte)15;
        var panelColor = isLightText
            ? Color.FromArgb(panelAlpha, 255, 255, 255)
            : Color.FromArgb(panelAlpha, 0, 0, 0);
        var panelBrush = CreateFrozenBrush(panelColor);

        // Inputs (TextBox, etc)
        byte inputAlpha = isLightText ? (byte)30 : (byte)24;
        var inputColor = isLightText
            ? Color.FromArgb(inputAlpha, 255, 255, 255)
            : Color.FromArgb(inputAlpha, 0, 0, 0);
        var inputBrush = CreateFrozenBrush(inputColor);

        // Subtle Glass Borders
        byte borderAlpha = isLightText ? (byte)45 : (byte)35;
        var borderColor = isLightText
            ? Color.FromArgb(borderAlpha, 255, 255, 255)
            : Color.FromArgb(borderAlpha, 0, 0, 0);
        var borderBrush = CreateFrozenBrush(borderColor);

        byte borderStrongAlpha = isLightText ? (byte)75 : (byte)55;
        var borderStrongColor = isLightText
            ? Color.FromArgb(borderStrongAlpha, 255, 255, 255)
            : Color.FromArgb(borderStrongAlpha, 0, 0, 0);
        var borderStrongBrush = CreateFrozenBrush(borderStrongColor);

        // Text & Foreground
        var textColor = isLightText
            ? Color.FromRgb(242, 242, 242)
            : Color.FromRgb(15, 15, 15);
        var textBrush = CreateFrozenBrush(textColor);

        var textMutedColor = isLightText
            ? Color.FromArgb(175, 242, 242, 242)
            : Color.FromArgb(175, 15, 15, 15);
        var textMutedBrush = CreateFrozenBrush(textMutedColor);

        // Accent / Secondary Emphasis Colors
        var ar = _lastAppliedConfig.AccentR != 0 || _lastAppliedConfig.AccentG != 0 || _lastAppliedConfig.AccentB != 0
            ? _lastAppliedConfig.AccentR
            : (byte)55;
        var ag = _lastAppliedConfig.AccentR != 0 || _lastAppliedConfig.AccentG != 0 || _lastAppliedConfig.AccentB != 0
            ? _lastAppliedConfig.AccentG
            : (byte)115;
        var ab = _lastAppliedConfig.AccentR != 0 || _lastAppliedConfig.AccentG != 0 || _lastAppliedConfig.AccentB != 0
            ? _lastAppliedConfig.AccentB
            : (byte)245;

        var accentColor = Color.FromRgb(ar, ag, ab);
        var accentBrush = CreateFrozenBrush(accentColor);

        var accentSoftColor = Color.FromArgb(isLightText ? (byte)40 : (byte)30, ar, ag, ab);
        var accentSoftBrush = CreateFrozenBrush(accentSoftColor);

        // Update application resources
        SetAppResource(app, "AppCanvasBrush", canvasBrush);
        SetAppResource(app, "AppSurfaceBrush", surfaceBrush);
        SetAppResource(app, "AppSurfaceRaisedBrush", surfaceRaisedBrush);
        SetAppResource(app, "AppPanelBrush", panelBrush);
        SetAppResource(app, "AppCardBrush", surfaceBrush);
        SetAppResource(app, "AppInputBrush", inputBrush);
        SetAppResource(app, "AppBorderBrush", borderBrush);
        SetAppResource(app, "AppBorderStrongBrush", borderStrongBrush);
        SetAppResource(app, "AppTextBrush", textBrush);
        SetAppResource(app, "AppTextMutedBrush", textMutedBrush);
        SetAppResource(app, "AppAccentBrush", accentBrush);
        SetAppResource(app, "AppAccentStrongBrush", accentBrush);
        SetAppResource(app, "AppAccentSoftBrush", accentSoftBrush);
    }

    /// <summary>
    /// Applies DWM glass effect to a window using the current config settings.
    /// </summary>
    public static void ApplyWindowBackdrop(Window window, DockConfig? config = null)
    {
        if (window == null) return;
        var cfg = config ?? _lastAppliedConfig ?? ConfigService.LoadConfig();
        GlassEffectHelper.Apply(window, cfg.UseTransparency, cfg.UseLightText);
    }

    private static void SetAppResource(System.Windows.Application app, string key, object value)
    {
        try
        {
            app.Resources[key] = value;
        }
        catch
        {
        }
    }

    private static SolidColorBrush CreateFrozenBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
