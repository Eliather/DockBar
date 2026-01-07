using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using DockBar.Models;

namespace DockBar.Services;

public static class StoreAppService
{
    public static List<StoreAppInfo> GetInstalledApps()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powershell",
                Arguments = "-NoProfile -Command \"Get-StartApps | Select-Object Name, AppID | ConvertTo-Json -Compress\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi);
            if (proc == null)
            {
                return new List<StoreAppInfo>();
            }

            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(4000);

            if (string.IsNullOrWhiteSpace(output))
            {
                return new List<StoreAppInfo>();
            }

            var apps = new List<StoreAppInfo>();
            using var doc = JsonDocument.Parse(output);
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in doc.RootElement.EnumerateArray())
                {
                    var name = element.TryGetProperty("Name", out var n) ? n.GetString() ?? string.Empty : string.Empty;
                    var appId = element.TryGetProperty("AppID", out var a) ? a.GetString() ?? string.Empty : string.Empty;
                    if (!string.IsNullOrWhiteSpace(appId))
                    {
                        apps.Add(new StoreAppInfo
                        {
                            Name = name,
                            FriendlyName = name,
                            AppId = appId,
                            PackageFamilyName = appId.Contains("!") ? appId.Split('!')[0] : appId
                        });
                    }
                }
            }
            else if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                var element = doc.RootElement;
                var name = element.TryGetProperty("Name", out var n) ? n.GetString() ?? string.Empty : string.Empty;
                var appId = element.TryGetProperty("AppID", out var a) ? a.GetString() ?? string.Empty : string.Empty;
                if (!string.IsNullOrWhiteSpace(appId))
                {
                    apps.Add(new StoreAppInfo
                    {
                        Name = name,
                        FriendlyName = name,
                        AppId = appId,
                        PackageFamilyName = appId.Contains("!") ? appId.Split('!')[0] : appId
                    });
                }
            }

            foreach (var app in apps)
            {
                var path = $"shell:AppsFolder\\{app.AppId}";
                var (friendly, icon) = ShellItemService.GetShellItemInfo(path, 256);
                if (!string.IsNullOrWhiteSpace(friendly))
                {
                    app.FriendlyName = friendly;
                    app.Name = friendly;
                }
                if (icon != null)
                {
                    app.Icon = icon;
                }
            }

            return apps
                .GroupBy(a => a.AppId, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .OrderBy(a => a.FriendlyName ?? a.Name)
                .ToList();
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            return new List<StoreAppInfo>();
        }
    }
}
