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

            // Migración segura si los campos experimentales se guardaron previamente en la raíz
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.ValueKind == JsonValueKind.Object)
                {
                    config.Experimental ??= new();

                    if (!root.TryGetProperty("Experimental", out _) && !root.TryGetProperty("experimental", out _))
                    {
                        if (root.TryGetProperty("ShowClock", out var sc) && (sc.ValueKind == JsonValueKind.True || sc.ValueKind == JsonValueKind.False))
                            config.Experimental.ShowClock = sc.GetBoolean();
                        if (root.TryGetProperty("ClockFontSize", out var cfs) && cfs.TryGetDouble(out var fs))
                            config.Experimental.ClockFontSize = fs;
                        if (root.TryGetProperty("ClockFormat24H", out var cf24) && (cf24.ValueKind == JsonValueKind.True || cf24.ValueKind == JsonValueKind.False))
                            config.Experimental.ClockFormat24H = cf24.GetBoolean();
                        if (root.TryGetProperty("ShowClockSeconds", out var scs) && (scs.ValueKind == JsonValueKind.True || scs.ValueKind == JsonValueKind.False))
                            config.Experimental.ShowClockSeconds = scs.GetBoolean();
                        if (root.TryGetProperty("ShowClockDate", out var scd) && (scd.ValueKind == JsonValueKind.True || scd.ValueKind == JsonValueKind.False))
                            config.Experimental.ShowClockDate = scd.GetBoolean();
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

        config.Experimental ??= new();
        if (config.Experimental.ClockFontSize <= 0)
        {
            config.Experimental.ClockFontSize = 18;
        }
        else
        {
            config.Experimental.ClockFontSize = Math.Clamp(config.Experimental.ClockFontSize, 10, 36);
        }

        return config;
    }
}
