using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using DockBar.Models;

namespace DockBar.Services;

public static class ConfigService
{
    private const string FileName = "shortcuts.json";
    public static string ConfigDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DockBar");

    private static string ConfigFilePath => Path.Combine(ConfigDirectory, FileName);

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
            // Backwards compatibility with the initial list-only format.
            if (json.TrimStart().StartsWith("["))
            {
                var shortcuts = JsonSerializer.Deserialize<List<ShortcutItem>>(json) ?? new List<ShortcutItem>();
                var cfg = CreateDefault();
                cfg.Shortcuts = shortcuts;
                createdDefault = false;
                return cfg;
            }

            var config = JsonSerializer.Deserialize<DockConfig>(json) ?? CreateDefault();
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
            Directory.CreateDirectory(ConfigDirectory);
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(config, options);
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
        if (config.BackgroundOpacity <= 0 || config.BackgroundOpacity > 1) config.BackgroundOpacity = 0.85;
        if (config.BackgroundR == 0 && config.BackgroundG == 0 && config.BackgroundB == 0)
        {
            // keep black; leave else as-is
        }
        // default text color: light
        // no further action needed; property already defaults to true.
        return config;
    }
}
