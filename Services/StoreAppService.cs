using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DockBar.Models;

namespace DockBar.Services;

public static class StoreAppService
{
    private static readonly object CacheLock = new();
    private static List<StoreAppInfo>? _cachedApps;

    // Persiste los íconos ya resueltos entre refrescos, keyed por AppId.
    // Un ícono raramente cambia para una app que sigue instalada, así que
    // esto evita repetir Steam lookup / resolución de ruta / shell factory
    // en cada forceRefresh.
    private static readonly ConcurrentDictionary<string, StoreAppInfo> _iconCacheByAppId =
        new(StringComparer.OrdinalIgnoreCase);

    // Límite de concurrencia para no saturar disco cuando hay cientos de apps.
    private const int MaxIconConcurrency = 8;

    public static void InvalidateCache()
    {
        lock (CacheLock)
        {
            _cachedApps = null;
        }
        // La caché de íconos sobrevive a propósito. Usar InvalidateIconCache()
        // si de verdad hace falta recalcular todos los íconos desde cero.
    }

    public static void InvalidateIconCache()
    {
        _iconCacheByAppId.Clear();
    }

    public static List<StoreAppInfo> GetInstalledApps(bool forceRefresh = false)
    {
        try
        {
            lock (CacheLock)
            {
                if (!forceRefresh && _cachedApps != null)
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

    // ---- Enumeración nativa ------------------------------------------------

    private sealed class PendingApp
    {
        public string Name = string.Empty;
        public string AppId = string.Empty;
        public IShellItem? ShellItem; // solo se mantiene vivo hasta la fase 3
        public ImageSource? Icon;
    }

    private static List<StoreAppInfo> GetAppsNative()
    {
        var pending = new List<PendingApp>();

        try
        {
            var guidIShellItem = new Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe");
            if (SHCreateItemFromParsingName("shell:AppsFolder", IntPtr.Zero, ref guidIShellItem, out var folderItemPtr) != 0 || folderItemPtr == IntPtr.Zero)
            {
                return new List<StoreAppInfo>();
            }

            var folderItem = (IShellItem)Marshal.GetObjectForIUnknown(folderItemPtr);
            var bhidEnumItems = new Guid("1e028f8d-da7b-4b24-9eec-2e994e0150d0");
            var guidIEnumShellItems = new Guid("7e9fac06-8f08-4ded-a230-928ead39d05c");

            // ---- Fase 1: enumeración secuencial, pero barata -------------------
            // Solo saca name/AppId. Nada de resolución de ícono ocurre acá,
            // así que esta pasada es ultra rápida sin importar cuántas apps haya.
            var seenAppIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

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

                        if (string.IsNullOrWhiteSpace(appId) || string.IsNullOrWhiteSpace(name) || !seenAppIds.Add(appId))
                        {
                            Marshal.ReleaseComObject(childItem);
                            continue;
                        }

                        // Un hit en la caché evita TODO el trabajo restante para
                        // esta app (Steam lookup, resolución de ruta, image factory).
                        if (_iconCacheByAppId.TryGetValue(appId, out var cachedInfo))
                        {
                            pending.Add(new PendingApp { Name = cachedInfo.FriendlyName ?? name, AppId = appId, Icon = cachedInfo.Icon });
                            Marshal.ReleaseComObject(childItem);
                            continue;
                        }

                        // Se mantiene vivo el objeto COM solo para las apps que
                        // todavía necesitan resolverse (puede hacer falta más
                        // adelante para IShellItemImageFactory).
                        pending.Add(new PendingApp { Name = name, AppId = appId, ShellItem = childItem });
                    }
                    catch
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

        try
        {
            // ---- Fase 2: resolución de íconos en paralelo (sin COM) ----------------
            // Steam y resolución de ruta física son I/O puro en .NET, sin depender
            // del objeto COM del ítem, así que vale la pena paralelizarlos.
            var needsResolution = pending.Where(p => p.ShellItem != null).ToList();
            Parallel.ForEach(
                needsResolution,
                new ParallelOptions { MaxDegreeOfParallelism = MaxIconConcurrency },
                item =>
                {
                    var (resolvedName, icon) = ResolveViaSteamOrPath(item.AppId, item.Name);
                    item.Name = resolvedName;
                    item.Icon = icon;
                });

            // ---- Fase 3: fallback secuencial para lo que sigue sin ícono -----------
            // Solo llegan acá las apps sin ícono de Steam/ruta (normalmente pocas:
            // básicamente las UWP). IShellItemImageFactory necesita el objeto COM
            // vivo de la fase 1 en el mismo hilo de enumeración para evitar cross-thread COM.
            foreach (var item in needsResolution)
            {
                if (item.Icon == null && item.ShellItem is IShellItemImageFactory factory)
                {
                    var hresult = factory.GetImage(new SIZE { cx = 48, cy = 48 }, SIIGBF.RESIZETOFIT, out var hbitmap);
                    if (hresult == 0 && hbitmap != IntPtr.Zero)
                    {
                        var source = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                            hbitmap,
                            IntPtr.Zero,
                            System.Windows.Int32Rect.Empty,
                            BitmapSizeOptions.FromWidthAndHeight(48, 48));
                        DeleteObject(hbitmap);
                        item.Icon = ShellItemService.AutoCropIfNeeded(source);
                    }
                }

                if (item.Icon == null)
                {
                    // ShellItemService.GetIcon ya aplica internamente AutoCropIfNeeded y Freeze
                    item.Icon = ShellItemService.GetIcon($"shell:AppsFolder\\{item.AppId}", 48);
                }
            }
        }
        finally
        {
            // Garantizar la liberación de todos los objetos COM pendientes incluso si ocurre una excepción
            foreach (var item in pending)
            {
                if (item.ShellItem != null)
                {
                    try
                    {
                        Marshal.ReleaseComObject(item.ShellItem);
                    }
                    catch
                    {
                        // Ignore COM release errors
                    }
                    item.ShellItem = null;
                }
            }
        }

        // ---- Materializar + Freeze + cachear ------------------------------------
        var apps = new List<StoreAppInfo>(pending.Count);
        foreach (var item in pending)
        {
            if (item.Icon is Freezable freezable && freezable.CanFreeze && !freezable.IsFrozen)
            {
                freezable.Freeze();
            }

            var info = new StoreAppInfo
            {
                Name = item.Name,
                FriendlyName = item.Name,
                AppId = item.AppId,
                PackageFamilyName = item.AppId.Contains('!') ? item.AppId.Split('!')[0] : item.AppId,
                Icon = item.Icon
            };

            _iconCacheByAppId[item.AppId] = info;
            apps.Add(info);
        }

        return apps;
    }

    private static (string name, ImageSource? icon) ResolveViaSteamOrPath(string appId, string fallbackName)
    {
        var name = fallbackName;
        ImageSource? icon = null;

        // 1. Detección de juegos de Steam
        var steamAppId = SteamService.ExtractSteamAppId(appId);
        if (!string.IsNullOrWhiteSpace(steamAppId))
        {
            var (gameTitle, steamIconPath, _) = SteamService.GetGameInfoByAppId(steamAppId);
            if (!string.IsNullOrWhiteSpace(steamIconPath))
            {
                icon = IconService.GetIconFromPath(steamIconPath, 48);
            }
            if (!string.IsNullOrWhiteSpace(gameTitle))
            {
                name = gameTitle;
            }
        }

        // 2. Resolución a ruta física
        if (icon == null)
        {
            var physicalPath = ShellItemService.ResolveAppIdPath(appId);
            if (File.Exists(physicalPath) || Directory.Exists(physicalPath))
            {
                icon = IconService.GetIcon(physicalPath, 48);
            }
        }

        return (name, icon);
    }

    // ---- Fallback de PowerShell (misma caché + resolución en paralelo) --------

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

            void AddFromElement(JsonElement element)
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
                        PackageFamilyName = appId.Contains('!') ? appId.Split('!')[0] : appId
                    });
                }
            }

            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in doc.RootElement.EnumerateArray())
                {
                    AddFromElement(element);
                }
            }
            else if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                AddFromElement(doc.RootElement);
            }

            // Cada llamada crea su propio shell item internamente, así que
            // es independiente y segura de paralelizar.
            Parallel.ForEach(
                apps,
                new ParallelOptions { MaxDegreeOfParallelism = MaxIconConcurrency },
                app =>
                {
                    if (_iconCacheByAppId.TryGetValue(app.AppId, out var cached))
                    {
                        app.FriendlyName = cached.FriendlyName;
                        app.Name = cached.Name;
                        app.Icon = cached.Icon;
                        return;
                    }

                    var path = $"shell:AppsFolder\\{app.AppId}";
                    var (friendly, icon) = ShellItemService.GetShellItemInfo(path, 48);
                    if (!string.IsNullOrWhiteSpace(friendly))
                    {
                        app.FriendlyName = friendly;
                        app.Name = friendly;
                    }
                    if (icon != null)
                    {
                        if (icon is Freezable freezable && freezable.CanFreeze && !freezable.IsFrozen)
                        {
                            freezable.Freeze();
                        }
                        app.Icon = icon;
                    }

                    _iconCacheByAppId[app.AppId] = app;
                });

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

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

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
    [Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemImageFactory
    {
        [PreserveSig]
        int GetImage(SIZE size, SIIGBF flags, out IntPtr phbm);
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

    [StructLayout(LayoutKind.Sequential)]
    private struct SIZE
    {
        public int cx;
        public int cy;
    }

    [Flags]
    private enum SIIGBF
    {
        RESIZETOFIT = 0x00,
        BIGGERSIZEOK = 0x01,
        MEMORYONLY = 0x02,
        ICONONLY = 0x04,
        THUMBNAILONLY = 0x08,
        INCACHEONLY = 0x10,
    }

    private enum SIGDN : uint
    {
        NORMALDISPLAY = 0,
        PARENTRELATIVEPARSING = 0x80018001,
        DESKTOPABSOLUTEPARSING = 0x80028000
    }
}
