using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DockBar.Services;

public static class ShellItemService
{
    private const int MaxCacheSize = 1024;
    private static readonly object CacheLock = new();
    private static readonly Dictionary<string, string?> NameCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, ImageSource?> IconCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Queue<string> IconCacheKeys = new();
    private static readonly Queue<string> NameCacheKeys = new();

    public static (string? displayName, ImageSource? icon) GetShellItemInfo(string shellPath, int size = 256)
    {
        try
        {
            var resultName = GetDisplayName(shellPath);
            var icon = GetIcon(shellPath, size);
            return (resultName, icon);
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            return (null, null);
        }
    }

    public static string? GetDisplayName(string shellPath)
    {
        try
        {
            lock (CacheLock)
            {
                if (NameCache.TryGetValue(shellPath, out var cachedName))
                {
                    return cachedName;
                }
            }

            if (SHCreateItemFromParsingName(shellPath, IntPtr.Zero, typeof(IShellItem).GUID, out var itemPtr) != 0 || itemPtr == IntPtr.Zero)
            {
                CacheName(shellPath, null);
                return null;
            }

            var item = (IShellItem)Marshal.GetObjectForIUnknown(itemPtr);
            item.GetDisplayName(SIGDN.NORMALDISPLAY, out var namePtr);
            var name = Marshal.PtrToStringUni(namePtr);
            Marshal.FreeCoTaskMem(namePtr);
            Marshal.ReleaseComObject(item);
            CacheName(shellPath, name);
            return name;
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            return null;
        }
    }

    public static ImageSource? GetIcon(string shellPath, int size = 256)
    {
        try
        {
            var cacheKey = $"{shellPath}|{size}";
            lock (CacheLock)
            {
                if (IconCache.TryGetValue(cacheKey, out var cachedIcon))
                {
                    return cachedIcon;
                }
            }

            // Check if shellPath is a Steam protocol link (steam://rungameid/<id>)
            var steamAppId = SteamService.ExtractSteamAppId(shellPath);
            if (!string.IsNullOrWhiteSpace(steamAppId))
            {
                var (_, steamIconPath, _) = SteamService.GetGameInfoByAppId(steamAppId);
                if (!string.IsNullOrWhiteSpace(steamIconPath))
                {
                    var steamIcon = IconService.GetIconFromPath(steamIconPath, size);
                    if (steamIcon != null)
                    {
                        var cropped = AutoCropIfNeeded(steamIcon);
                        CacheIcon(cacheKey, cropped);
                        return cropped;
                    }
                }
            }

            // Check if shellPath maps to a real physical exe / file path
            var physicalPath = ResolveAppIdPath(shellPath);
            if (File.Exists(physicalPath) || Directory.Exists(physicalPath))
            {
                var directIcon = IconService.GetIcon(physicalPath, size);
                if (directIcon != null)
                {
                    var cropped = AutoCropIfNeeded(directIcon);
                    CacheIcon(cacheKey, cropped);
                    return cropped;
                }
            }

            if (SHCreateItemFromParsingName(shellPath, IntPtr.Zero, typeof(IShellItemImageFactory).GUID, out var factoryPtr) != 0 || factoryPtr == IntPtr.Zero)
            {
                CacheIcon(cacheKey, null);
                return null;
            }

            var factory = (IShellItemImageFactory)Marshal.GetObjectForIUnknown(factoryPtr);
            var hresult = factory.GetImage(new SIZE { cx = size, cy = size }, SIIGBF.RESIZETOFIT, out var hbitmap);
            if (hresult != 0 || hbitmap == IntPtr.Zero)
            {
                Marshal.ReleaseComObject(factory);
                CacheIcon(cacheKey, null);
                return null;
            }

            var source = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                hbitmap,
                IntPtr.Zero,
                Int32Rect.Empty,
                BitmapSizeOptions.FromWidthAndHeight(size, size));

            DeleteObject(hbitmap);
            Marshal.ReleaseComObject(factory);

            var finalIcon = AutoCropIfNeeded(source);
            if (finalIcon != null && finalIcon.CanFreeze && !finalIcon.IsFrozen)
            {
                finalIcon.Freeze();
            }

            CacheIcon(cacheKey, finalIcon);
            return finalIcon;
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            return null;
        }
    }

    public static string ResolveAppIdPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return path;

        var appId = path;
        if (appId.StartsWith("shell:AppsFolder\\", StringComparison.OrdinalIgnoreCase))
        {
            appId = appId["shell:AppsFolder\\".Length..];
        }

        var guidMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["{7C5A40EF-A0FB-4BFC-874A-C0F2E0B9FA8E}"] = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            ["{6D809377-6AF0-444B-8957-A3773F02200E}"] = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            ["{D65231B0-B2F1-4857-A4CE-A8E7C6EA7D27}"] = Environment.GetFolderPath(Environment.SpecialFolder.System),
            ["{1AC14E77-02E7-4E5D-B744-2EB1AE5198B7}"] = Environment.GetFolderPath(Environment.SpecialFolder.SystemX86),
            ["{F38BF404-1D43-42F2-9305-67DE0B28FC23}"] = Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            ["{905e63b6-c1bf-494e-b29c-65b732d3d21a}"] = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            ["{F1B32785-6FBA-4FCF-9D55-7B8E7F157091}"] = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            ["{A77F5D77-2E2B-44C3-A6A2-ABA601054A51}"] = Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms),
            ["{A25250C4-50C1-4240-87F2-68E82E5E1A47}"] = Environment.GetFolderPath(Environment.SpecialFolder.Programs),
        };

        foreach (var kvp in guidMap)
        {
            if (appId.StartsWith(kvp.Key, StringComparison.OrdinalIgnoreCase))
            {
                var rel = appId[kvp.Key.Length..].TrimStart('\\', '/');
                var full = Path.Combine(kvp.Value, rel);
                if (File.Exists(full) || Directory.Exists(full))
                {
                    return full;
                }
            }
        }

        if (File.Exists(appId) || Directory.Exists(appId))
        {
            return appId;
        }

        return path;
    }

    public static ImageSource? AutoCropIfNeeded(ImageSource? image)
    {
        if (image is not BitmapSource source)
        {
            return image;
        }

        try
        {
            var conv = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
            int width = conv.PixelWidth;
            int height = conv.PixelHeight;
            if (width < 16 || height < 16)
            {
                return image;
            }

            int stride = width * 4;
            byte[] pixels = new byte[height * stride];
            conv.CopyPixels(pixels, stride, 0);

            int minX = width, minY = height, maxX = 0, maxY = 0;
            // Exclude outermost border inset to ignore faint tile plate border added by Windows shell
            int borderInset = Math.Min(4, width / 16);
            for (int y = borderInset; y < height - borderInset; y++)
            {
                for (int x = borderInset; x < width - borderInset; x++)
                {
                    int idx = y * stride + x * 4;
                    byte a = pixels[idx + 3];
                    if (a > 45)
                    {
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
                }
            }

            if (minX >= maxX || minY >= maxY)
            {
                if (source.CanFreeze && !source.IsFrozen) source.Freeze();
                return source;
            }

            int contentW = maxX - minX + 1;
            int contentH = maxY - minY + 1;

            // If content occupies less than 85% of canvas width or height, crop it!
            if (contentW < width * 0.85 || contentH < height * 0.85)
            {
                int padding = Math.Max(2, (int)(Math.Max(contentW, contentH) * 0.05));
                int cropX = Math.Max(0, minX - padding);
                int cropY = Math.Max(0, minY - padding);
                int cropW = Math.Min(width - cropX, contentW + padding * 2);
                int cropH = Math.Min(height - cropY, contentH + padding * 2);

                var cropped = new CroppedBitmap(source, new Int32Rect(cropX, cropY, cropW, cropH));
                if (cropped.CanFreeze && !cropped.IsFrozen)
                {
                    cropped.Freeze();
                }
                return cropped;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }

        if (source.CanFreeze && !source.IsFrozen)
        {
            source.Freeze();
        }
        return source;
    }

    private static void CacheName(string shellPath, string? displayName)
    {
        lock (CacheLock)
        {
            if (!NameCache.ContainsKey(shellPath))
            {
                if (NameCacheKeys.Count >= MaxCacheSize * 2)
                {
                    var oldest = NameCacheKeys.Dequeue();
                    NameCache.Remove(oldest);
                }
                NameCacheKeys.Enqueue(shellPath);
            }
            NameCache[shellPath] = displayName;
        }
    }

    private static void CacheIcon(string cacheKey, ImageSource? icon)
    {
        lock (CacheLock)
        {
            if (!IconCache.ContainsKey(cacheKey))
            {
                if (IconCacheKeys.Count >= MaxCacheSize)
                {
                    var oldest = IconCacheKeys.Dequeue();
                    IconCache.Remove(oldest);
                }
                IconCacheKeys.Enqueue(cacheKey);
            }
            IconCache[cacheKey] = icon;
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    private static extern int SHCreateItemFromParsingName([MarshalAs(UnmanagedType.LPWStr)] string pszPath, IntPtr pbc, [MarshalAs(UnmanagedType.LPStruct)] Guid riid, out IntPtr ppv);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    [ComImport]
    [Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItem
    {
        void BindToHandler(IntPtr pbc, [MarshalAs(UnmanagedType.LPStruct)] Guid bhid, [MarshalAs(UnmanagedType.LPStruct)] Guid riid, out IntPtr ppv);
        void GetParent(out IShellItem ppsi);
        void GetDisplayName(SIGDN sigdnName, out IntPtr ppszName);
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

    [StructLayout(LayoutKind.Sequential)]
    private struct SIZE
    {
        public int cx;
        public int cy;
    }

    private enum SIGDN : uint
    {
        NORMALDISPLAY = 0,
        PARENTRELATIVEPARSING = 0x80018001,
        DESKTOPABSOLUTEPARSING = 0x80028000,
        PARENTRELATIVEEDITING = 0x80031001,
        DESKTOPABSOLUTEEDITING = 0x8004c000,
        FILESYSPATH = 0x80058000,
        URL = 0x80068000
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
}
