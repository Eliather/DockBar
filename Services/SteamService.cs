using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace DockBar.Services;

public static class SteamService
{
    private static readonly object LockObj = new();
    private static string? _cachedSteamPath;
    private static List<string>? _cachedLibraries;
    private static readonly Dictionary<string, (string name, string? iconPath)> _cachedAppInfo = new(StringComparer.OrdinalIgnoreCase);

    public static string? GetSteamPath()
    {
        lock (LockObj)
        {
            if (_cachedSteamPath != null && Directory.Exists(_cachedSteamPath))
            {
                return _cachedSteamPath;
            }

            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
                var path = key?.GetValue("SteamPath") as string;
                if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
                {
                    _cachedSteamPath = Path.GetFullPath(path);
                    return _cachedSteamPath;
                }

                using var hklmKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam")
                                 ?? Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Valve\Steam");
                var installPath = hklmKey?.GetValue("InstallPath") as string;
                if (!string.IsNullOrWhiteSpace(installPath) && Directory.Exists(installPath))
                {
                    _cachedSteamPath = Path.GetFullPath(installPath);
                    return _cachedSteamPath;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }

            var defaultPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam");
            if (Directory.Exists(defaultPath))
            {
                _cachedSteamPath = defaultPath;
                return _cachedSteamPath;
            }

            var default64 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Steam");
            if (Directory.Exists(default64))
            {
                _cachedSteamPath = default64;
                return _cachedSteamPath;
            }

            return null;
        }
    }

    public static List<string> GetLibraryFolders()
    {
        lock (LockObj)
        {
            if (_cachedLibraries != null && _cachedLibraries.Count > 0)
            {
                return _cachedLibraries;
            }

            var libraries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var steamPath = GetSteamPath();
            if (!string.IsNullOrWhiteSpace(steamPath))
            {
                libraries.Add(steamPath);

                var vdfPath = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
                if (File.Exists(vdfPath))
                {
                    try
                    {
                        var content = File.ReadAllText(vdfPath);
                        var matches = Regex.Matches(content, @"""path""\s+""([^""]+)""", RegexOptions.IgnoreCase);
                        foreach (Match match in matches)
                        {
                            if (match.Groups.Count > 1)
                            {
                                var rawPath = match.Groups[1].Value.Replace(@"\\", @"\");
                                if (Directory.Exists(rawPath))
                                {
                                    libraries.Add(rawPath);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex);
                    }
                }
            }

            _cachedLibraries = libraries.ToList();
            return _cachedLibraries;
        }
    }

    public static (string? name, string? iconPath, string? exePath) GetGameInfoByAppId(string appId)
    {
        if (string.IsNullOrWhiteSpace(appId)) return (null, null, null);

        lock (LockObj)
        {
            if (_cachedAppInfo.TryGetValue(appId, out var cached))
            {
                return (cached.name, cached.iconPath, null);
            }
        }

        string? gameName = null;
        string? installDir = null;
        string? foundLibrary = null;

        var libraries = GetLibraryFolders();
        foreach (var lib in libraries)
        {
            var manifest = Path.Combine(lib, "steamapps", $"appmanifest_{appId}.acf");
            if (File.Exists(manifest))
            {
                try
                {
                    var text = File.ReadAllText(manifest);
                    var nameMatch = Regex.Match(text, @"""name""\s+""([^""]+)""", RegexOptions.IgnoreCase);
                    if (nameMatch.Success)
                    {
                        gameName = nameMatch.Groups[1].Value;
                    }

                    var dirMatch = Regex.Match(text, @"""installdir""\s+""([^""]+)""", RegexOptions.IgnoreCase);
                    if (dirMatch.Success)
                    {
                        installDir = dirMatch.Groups[1].Value;
                        foundLibrary = lib;
                    }
                    break;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex);
                }
            }
        }

        string? foundIcon = null;
        string? foundExe = null;

        // 1. Check game installation folder for main executable
        if (!string.IsNullOrWhiteSpace(foundLibrary) && !string.IsNullOrWhiteSpace(installDir))
        {
            var commonDir = Path.Combine(foundLibrary, "steamapps", "common", installDir);
            if (Directory.Exists(commonDir))
            {
                try
                {
                    var exes = Directory.GetFiles(commonDir, "*.exe", SearchOption.AllDirectories);
                    var filtered = exes.Where(e =>
                    {
                        var fn = Path.GetFileName(e).ToLowerInvariant();
                        return !(fn.Contains("crash") || fn.Contains("helper") || fn.Contains("unins") ||
                                 fn.Contains("installer") || fn.Contains("setup") || fn.Contains("createdump") ||
                                 fn.Contains("vcredist") || fn.Contains("directx") || fn.Contains("easyanticheat") ||
                                 fn.Contains("redist") || fn.Contains("epicweb") || fn.Contains("cef"));
                    }).ToList();

                    string normInstall = NormalizeString(installDir);
                    string normGame = NormalizeString(gameName ?? string.Empty);

                    var bestExe = filtered
                        .OrderByDescending(e =>
                        {
                            var fn = NormalizeString(Path.GetFileNameWithoutExtension(e));
                            int score = 0;

                            bool isRoot = Path.GetDirectoryName(e)?.Equals(commonDir, StringComparison.OrdinalIgnoreCase) == true;
                            if (isRoot) score += 50;

                            if (fn == normInstall || fn == normGame) score += 100;
                            else if (fn.Contains(normInstall) || fn.Contains(normGame)) score += 60;
                            else if (normGame.Contains(fn) || normInstall.Contains(fn)) score += 40;

                            if (fn.EndsWith("64")) score += 5;
                            return score;
                        })
                        .FirstOrDefault();

                    if (bestExe != null)
                    {
                        foundExe = bestExe;
                        foundIcon = bestExe;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex);
                }
            }
        }

        // 2. Check Steam\steam\games\ for cached ico file
        if (foundIcon == null)
        {
            var steamPath = GetSteamPath();
            if (!string.IsNullOrWhiteSpace(steamPath))
            {
                var gamesIconDir = Path.Combine(steamPath, "steam", "games");
                if (Directory.Exists(gamesIconDir))
                {
                    var directIco = Path.Combine(gamesIconDir, $"{appId}.ico");
                    if (File.Exists(directIco))
                    {
                        foundIcon = directIco;
                    }
                }
            }
        }

        if (gameName != null || foundIcon != null)
        {
            lock (LockObj)
            {
                _cachedAppInfo[appId] = (gameName ?? $"Steam App {appId}", foundIcon);
            }
        }

        return (gameName, foundIcon, foundExe);
    }

    private static string NormalizeString(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return string.Empty;
        return new string(s.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
    }

    public static (string? url, string? iconFile, int iconIndex, string? displayName) ParseUrlFile(string urlFilePath)
    {
        if (!File.Exists(urlFilePath)) return (null, null, 0, null);

        string? url = null;
        string? iconFile = null;
        int iconIndex = 0;
        string? displayName = Path.GetFileNameWithoutExtension(urlFilePath);

        try
        {
            var lines = File.ReadAllLines(urlFilePath);
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("URL=", StringComparison.OrdinalIgnoreCase))
                {
                    url = trimmed["URL=".Length..].Trim();
                }
                else if (trimmed.StartsWith("IconFile=", StringComparison.OrdinalIgnoreCase))
                {
                    iconFile = trimmed["IconFile=".Length..].Trim();
                }
                else if (trimmed.StartsWith("IconIndex=", StringComparison.OrdinalIgnoreCase))
                {
                    int.TryParse(trimmed["IconIndex=".Length..].Trim(), out iconIndex);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }

        return (url, iconFile, iconIndex, displayName);
    }

    public static string? ExtractSteamAppId(string? urlOrPath)
    {
        if (string.IsNullOrWhiteSpace(urlOrPath)) return null;

        var match = Regex.Match(urlOrPath, @"steam://(?:rungameid|run)/(\d+)", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            return match.Groups[1].Value;
        }

        // If it is just numeric
        if (urlOrPath.All(char.IsDigit) && urlOrPath.Length >= 3 && urlOrPath.Length <= 10)
        {
            return urlOrPath;
        }

        return null;
    }
}
