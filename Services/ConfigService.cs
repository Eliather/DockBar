using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using DockBar.Models;

namespace DockBar.Services;

public static class ConfigService
{
    private const string FileName = "shortcuts.json";
    private const double GlassOpacity = 0.45;
    private static readonly JsonSerializerOptions IndentedJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static string ConfigDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DockBar");

    public static string ConfigFilePath => Path.Combine(ConfigDirectory, FileName);

    public static DockConfig LoadConfig(out bool createdDefault, out bool hadError)
    {
        createdDefault = false;
        hadError = false;
        try
        {
            if (!File.Exists(ConfigFilePath))
            {
                createdDefault = true;
                return CreateDefault();
            }

            var json = File.ReadAllText(ConfigFilePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                createdDefault = true;
                return CreateDefault();
            }

            // Compatibilidad hacia atrás con el formato inicial de solo lista de accesos directos
            if (json.TrimStart().StartsWith("["))
            {
                var shortcuts = JsonSerializer.Deserialize<List<ShortcutItem>>(json, IndentedJsonOptions) ?? new List<ShortcutItem>();
                var cfg = CreateDefault();
                cfg.Shortcuts = shortcuts;
                createdDefault = false;
                return cfg;
            }

            DockConfig? config = null;
            try
            {
                config = JsonSerializer.Deserialize<DockConfig>(json, IndentedJsonOptions);
            }
            catch
            {
                // Rescate defensivo: si existiese algún campo experimental con formato anómalo,
                // intentamos recuperar al menos los accesos directos principales del usuario
                try
                {
                    using var doc = JsonDocument.Parse(json);
                    config = CreateDefault();
                    if (doc.RootElement.TryGetProperty("Shortcuts", out var shortcutsElem) ||
                        doc.RootElement.TryGetProperty("shortcuts", out shortcutsElem))
                    {
                        var shortcuts = shortcutsElem.Deserialize<List<ShortcutItem>>(IndentedJsonOptions);
                        if (shortcuts != null)
                        {
                            config.Shortcuts = shortcuts;
                        }
                    }
                }
                catch
                {
                    createdDefault = true;
                    hadError = true;
                    return CreateDefault();
                }
            }

            config ??= CreateDefault();

            // Migración segura: soporta configs antiguas donde Clock y Media estaban en Experimental o en la raíz
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.ValueKind == JsonValueKind.Object)
                {
                    config.Clock ??= new();
                    config.Media ??= new();
                    config.Experimental ??= new();

                    bool hasClockSection = root.TryGetProperty("Clock", out _) || root.TryGetProperty("clock", out _);
                    bool hasMediaSection = root.TryGetProperty("Media", out _) || root.TryGetProperty("media", out _);
                    bool hasExperimentalSection = root.TryGetProperty("Experimental", out var expElem) || root.TryGetProperty("experimental", out expElem);

                    // 1. Migrar desde Experimental antiguo si no existían secciones dedicadas
                    if (hasExperimentalSection && expElem.ValueKind == JsonValueKind.Object)
                    {
                        if (!hasClockSection)
                        {
                            if (expElem.TryGetProperty("ShowClock", out var sc) && (sc.ValueKind == JsonValueKind.True || sc.ValueKind == JsonValueKind.False))
                                config.Clock.ShowClock = sc.GetBoolean();
                            if (expElem.TryGetProperty("ClockFontSize", out var cfs) && cfs.TryGetDouble(out var fs))
                                config.Clock.ClockFontSize = fs;
                            if (expElem.TryGetProperty("ClockFormat24H", out var cf24) && (cf24.ValueKind == JsonValueKind.True || cf24.ValueKind == JsonValueKind.False))
                                config.Clock.ClockFormat24H = cf24.GetBoolean();
                            if (expElem.TryGetProperty("ShowClockSeconds", out var scs) && (scs.ValueKind == JsonValueKind.True || scs.ValueKind == JsonValueKind.False))
                                config.Clock.ShowClockSeconds = scs.GetBoolean();
                            if (expElem.TryGetProperty("ShowClockDate", out var scd) && (scd.ValueKind == JsonValueKind.True || scd.ValueKind == JsonValueKind.False))
                                config.Clock.ShowClockDate = scd.GetBoolean();
                        }

                        if (!hasMediaSection)
                        {
                            if (expElem.TryGetProperty("ShowVolumeControl", out var svc) && (svc.ValueKind == JsonValueKind.True || svc.ValueKind == JsonValueKind.False))
                                config.Media.ShowVolumeControl = svc.GetBoolean();
                            if (expElem.TryGetProperty("ShowMediaControl", out var smc) && (smc.ValueKind == JsonValueKind.True || smc.ValueKind == JsonValueKind.False))
                                config.Media.ShowMediaControl = smc.GetBoolean();
                            if (expElem.TryGetProperty("ShowMediaSeekBar", out var smsb) && (smsb.ValueKind == JsonValueKind.True || smsb.ValueKind == JsonValueKind.False))
                                config.Media.ShowMediaSeekBar = smsb.GetBoolean();
                        }
                    }

                    // 2. Migrar desde la raíz (versiones muy tempranas previas a secciones)
                    if (!hasClockSection && !hasExperimentalSection)
                    {
                        if (root.TryGetProperty("ShowClock", out var sc) && (sc.ValueKind == JsonValueKind.True || sc.ValueKind == JsonValueKind.False))
                            config.Clock.ShowClock = sc.GetBoolean();
                        if (root.TryGetProperty("ClockFontSize", out var cfs) && cfs.TryGetDouble(out var fs))
                            config.Clock.ClockFontSize = fs;
                        if (root.TryGetProperty("ClockFormat24H", out var cf24) && (cf24.ValueKind == JsonValueKind.True || cf24.ValueKind == JsonValueKind.False))
                            config.Clock.ClockFormat24H = cf24.GetBoolean();
                        if (root.TryGetProperty("ShowClockSeconds", out var scs) && (scs.ValueKind == JsonValueKind.True || scs.ValueKind == JsonValueKind.False))
                            config.Clock.ShowClockSeconds = scs.GetBoolean();
                        if (root.TryGetProperty("ShowClockDate", out var scd) && (scd.ValueKind == JsonValueKind.True || scd.ValueKind == JsonValueKind.False))
                            config.Clock.ShowClockDate = scd.GetBoolean();
                    }

                    if (!hasMediaSection && !hasExperimentalSection)
                    {
                        if (root.TryGetProperty("ShowVolumeControl", out var svc) && (svc.ValueKind == JsonValueKind.True || svc.ValueKind == JsonValueKind.False))
                            config.Media.ShowVolumeControl = svc.GetBoolean();
                        if (root.TryGetProperty("ShowMediaControl", out var smc) && (smc.ValueKind == JsonValueKind.True || smc.ValueKind == JsonValueKind.False))
                            config.Media.ShowMediaControl = smc.GetBoolean();
                        if (root.TryGetProperty("ShowMediaSeekBar", out var smsb) && (smsb.ValueKind == JsonValueKind.True || smsb.ValueKind == JsonValueKind.False))
                            config.Media.ShowMediaSeekBar = smsb.GetBoolean();
                    }

                    if (!hasExperimentalSection)
                    {
                        if (root.TryGetProperty("EdgeTriggerPx", out var etp) && etp.TryGetDouble(out var px))
                            config.Experimental.EdgeTriggerPx = px;
                        if (root.TryGetProperty("ShowResourceMonitor", out var srm) && (srm.ValueKind == JsonValueKind.True || srm.ValueKind == JsonValueKind.False))
                            config.Experimental.ShowResourceMonitor = srm.GetBoolean();
                        if (root.TryGetProperty("ShowHardwareModelNames", out var shm) && (shm.ValueKind == JsonValueKind.True || shm.ValueKind == JsonValueKind.False))
                            config.Experimental.ShowHardwareModelNames = shm.GetBoolean();
                        if (root.TryGetProperty("ShowCaffeine", out var scaf) && (scaf.ValueKind == JsonValueKind.True || scaf.ValueKind == JsonValueKind.False))
                            config.Experimental.ShowCaffeine = scaf.GetBoolean();
                    }
                }
            }
            catch
            {
                // Ignorar advertencias secundarias de migración
            }

            config = EnsureDefaults(config);
            return config;
        }
        catch
        {
            createdDefault = true;
            hadError = true;
            return CreateDefault();
        }
    }

    public static DockConfig LoadConfig()
    {
        return LoadConfig(out _, out _);
    }

    public static void SaveConfig(DockConfig config)
    {
        try
        {
            if (config.BackgroundOpacity < 0 || config.BackgroundOpacity > 1.0)
            {
                config.BackgroundOpacity = GlassOpacity;
            }
            config = EnsureDefaults(config);
            Directory.CreateDirectory(ConfigDirectory);
            var json = JsonSerializer.Serialize(config, IndentedJsonOptions);
            File.WriteAllText(ConfigFilePath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
        }
    }

    private static DockConfig CreateDefault() => new();

    private static DockConfig EnsureDefaults(DockConfig config)
    {
        config.Shortcuts ??= new();
        if (config.DockWidth < 175) config.DockWidth = 175;
        if (config.IconSize <= 0) config.IconSize = 40;
        if (config.HideAnimationMs <= 0) config.HideAnimationMs = 200;
        if (config.AutoHideDelaySeconds < 0) config.AutoHideDelaySeconds = 0;
        if (config.BackgroundOpacity < 0 || config.BackgroundOpacity > 1.0)
        {
            config.BackgroundOpacity = GlassOpacity;
        }

        config.Clock ??= new();
        if (config.Clock.ClockFontSize <= 0)
        {
            config.Clock.ClockFontSize = 18;
        }
        else
        {
            config.Clock.ClockFontSize = Math.Clamp(config.Clock.ClockFontSize, 10, 36);
        }

        config.Media ??= new();

        config.Experimental ??= new();
        if (config.Experimental.EdgeTriggerPx <= 0)
        {
            config.Experimental.EdgeTriggerPx = 8;
        }
        else
        {
            config.Experimental.EdgeTriggerPx = Math.Clamp(config.Experimental.EdgeTriggerPx, 1, 30);
        }

        config.Experimental.WidgetOrder ??= new();
        var validWidgets = new[] { "Clock", "Media", "Volume", "Resource", "Caffeine" };
        var sanitizedOrder = config.Experimental.WidgetOrder
            .Where(w => validWidgets.Contains(w, StringComparer.OrdinalIgnoreCase))
            .Select(w => validWidgets.First(v => string.Equals(v, w, StringComparison.OrdinalIgnoreCase)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        foreach (var v in validWidgets)
        {
            if (!sanitizedOrder.Contains(v, StringComparer.OrdinalIgnoreCase))
            {
                sanitizedOrder.Add(v);
            }
        }
        config.Experimental.WidgetOrder = sanitizedOrder;

        return config;
    }
}
