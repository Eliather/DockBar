using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using DockBar.Models;

namespace DockBar.Services;

public static class StoreAppService
{
    private static readonly object CacheLock = new();
    private static List<StoreAppInfo>? _cachedApps;

    public static List<StoreAppInfo> GetInstalledApps()
    {
        try
        {
            lock (CacheLock)
            {
                if (_cachedApps != null)
                {
                    return CloneApps(_cachedApps);
                }
            }

            var apps = GetAppsNative();
            if (apps.Count == 0)
            {
                apps = GetAppsPowerShellFallback();
            }

            var orderedApps = apps
                .GroupBy(a => a.AppId, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .OrderBy(a => a.FriendlyName ?? a.Name)
                .ToList();

            lock (CacheLock)
            {
                _cachedApps = orderedApps;
            }

            return CloneApps(orderedApps);
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            return new List<StoreAppInfo>();
        }
    }

    private static List<StoreAppInfo> GetAppsNative()
    {
        var apps = new List<StoreAppInfo>();
        try
        {
            var guidIShellItem = new Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe");
            if (SHCreateItemFromParsingName("shell:AppsFolder", IntPtr.Zero, ref guidIShellItem, out var folderItemPtr) != 0 || folderItemPtr == IntPtr.Zero)
            {
                return apps;
            }

            var folderItem = (IShellItem)Marshal.GetObjectForIUnknown(folderItemPtr);
            var bhidEnumItems = new Guid("1e028f8d-da7b-4b24-9eec-2e994e0150d0");
            var guidIEnumShellItems = new Guid("7e9fac06-8f08-4ded-a230-928ead39d05c");

            if (folderItem.BindToHandler(IntPtr.Zero, ref bhidEnumItems, ref guidIEnumShellItems, out var enumPtr) == 0 && enumPtr != IntPtr.Zero)
            {
                var enumItems = (IEnumShellItems)Marshal.GetObjectForIUnknown(enumPtr);
                while (enumItems.Next(1, out var childItem, out var fetched) == 0 && fetched == 1 && childItem != null)
                {
                    try
                    {
                        childItem.GetDisplayName(SIGDN.NORMALDISPLAY, out var namePtr);
                        var name = Marshal.PtrToStringUni(namePtr) ?? string.Empty;
                        Marshal.FreeCoTaskMem(namePtr);

                        childItem.GetDisplayName(SIGDN.DESKTOPABSOLUTEPARSING, out var parsingPtr);
                        var parsingName = Marshal.PtrToStringUni(parsingPtr) ?? string.Empty;
                        Marshal.FreeCoTaskMem(parsingPtr);

                        var appId = parsingName;
                        if (appId.StartsWith("shell:AppsFolder\\", StringComparison.OrdinalIgnoreCase))
                        {
                            appId = appId["shell:AppsFolder\\".Length..];
                        }

                        if (!string.IsNullOrWhiteSpace(appId) && !string.IsNullOrWhiteSpace(name))
                        {
                            var shellPath = $"shell:AppsFolder\\{appId}";
                            var icon = ShellItemService.GetIcon(shellPath, 256);

                            apps.Add(new StoreAppInfo
                            {
                                Name = name,
                                FriendlyName = name,
                                AppId = appId,
                                PackageFamilyName = appId.Contains("!") ? appId.Split('!')[0] : appId,
                                Icon = icon
                            });
                        }
                    }
                    catch
                    {
                        // Ignore individual item enumeration errors
                    }
                    finally
                    {
                        Marshal.ReleaseComObject(childItem);
                    }
                }
                Marshal.ReleaseComObject(enumItems);
            }

            Marshal.ReleaseComObject(folderItem);
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }

        return apps;
    }

    private static List<StoreAppInfo> GetAppsPowerShellFallback()
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
            proc.WaitForExit(3000);

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

            return apps;
        }
        catch
        {
            return new List<StoreAppInfo>();
        }
    }

    private static List<StoreAppInfo> CloneApps(List<StoreAppInfo> apps)
    {
        return apps.Select(app => new StoreAppInfo
        {
            Name = app.Name,
            FriendlyName = app.FriendlyName,
            AppId = app.AppId,
            PackageFamilyName = app.PackageFamilyName,
            Icon = app.Icon
        }).ToList();
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    private static extern int SHCreateItemFromParsingName(
        [MarshalAs(UnmanagedType.LPWStr)] string pszPath,
        IntPtr pbc,
        ref Guid riid,
        out IntPtr ppv);

    [ComImport]
    [Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItem
    {
        [PreserveSig]
        int BindToHandler(IntPtr pbc, ref Guid bhid, ref Guid riid, out IntPtr ppv);
        void GetParent(out IShellItem ppsi);
        [PreserveSig]
        int GetDisplayName(SIGDN sigdnName, out IntPtr ppszName);
        void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
        void Compare(IShellItem psi, uint hint, out int piOrder);
    }

    [ComImport]
    [Guid("7e9fac06-8f08-4ded-a230-928ead39d05c")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IEnumShellItems
    {
        [PreserveSig]
        int Next(uint celt, out IShellItem? rgelt, out uint pceltFetched);
        void Skip(uint celt);
        void Reset();
        void Clone(out IEnumShellItems ppenum);
    }

    private enum SIGDN : uint
    {
        NORMALDISPLAY = 0,
        PARENTRELATIVEPARSING = 0x80018001,
        DESKTOPABSOLUTEPARSING = 0x80028000
    }
}
