using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace DockBar.Services;

public static class IconService
{
    private const int MaxCacheSize = 128;
    private static readonly object CacheLock = new();
    private static readonly Dictionary<string, ImageSource?> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Queue<string> CacheKeys = new();
    private static readonly Dictionary<string, string> ShortcutTargetCache = new(StringComparer.OrdinalIgnoreCase);

    public static ImageSource? GetIcon(string path, int preferredSize = 64)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path)) return null;

            // 1. Handle Steam URL protocols (steam://rungameid/<id> or steam://run/<id>)
            if (path.StartsWith("steam://", StringComparison.OrdinalIgnoreCase))
            {
                var appId = SteamService.ExtractSteamAppId(path);
                if (!string.IsNullOrWhiteSpace(appId))
                {
                    var (_, iconPath, _) = SteamService.GetGameInfoByAppId(appId);
                    if (!string.IsNullOrWhiteSpace(iconPath))
                    {
                        var steamIcon = GetIconFromPath(iconPath, preferredSize);
                        if (steamIcon != null) return steamIcon;
                    }
                }
            }

            // 2. Handle .url files
            if (path.EndsWith(".url", StringComparison.OrdinalIgnoreCase) && File.Exists(path))
            {
                var (url, iconFile, _, _) = SteamService.ParseUrlFile(path);
                if (!string.IsNullOrWhiteSpace(iconFile) && File.Exists(iconFile))
                {
                    var urlIcon = GetIconFromPath(iconFile, preferredSize);
                    if (urlIcon != null) return urlIcon;
                }

                if (!string.IsNullOrWhiteSpace(url) && url.StartsWith("steam://", StringComparison.OrdinalIgnoreCase))
                {
                    var appId = SteamService.ExtractSteamAppId(url);
                    if (!string.IsNullOrWhiteSpace(appId))
                    {
                        var (_, iconPath, _) = SteamService.GetGameInfoByAppId(appId);
                        if (!string.IsNullOrWhiteSpace(iconPath))
                        {
                            var steamIcon = GetIconFromPath(iconPath, preferredSize);
                            if (steamIcon != null) return steamIcon;
                        }
                    }
                }
            }

            var resolvedPath = ResolveShortcutTarget(path);
            var cacheKey = BuildCacheKey("path", resolvedPath, preferredSize);
            if (TryGetCached(cacheKey, out var cached))
            {
                return cached;
            }

            if (!File.Exists(resolvedPath) && !Directory.Exists(resolvedPath))
            {
                StoreCached(cacheKey, null);
                return null;
            }

            var resolvedIcon = FreezeIfPossible(GetJumboIcon(resolvedPath))
                ?? FreezeIfPossible(GetHighResIcon(resolvedPath, preferredSize));

            if (resolvedIcon == null)
            {
                using var icon = Icon.ExtractAssociatedIcon(resolvedPath);
                if (icon != null)
                {
                    resolvedIcon = FreezeIfPossible(Imaging.CreateBitmapSourceFromHIcon(
                        icon.Handle,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromWidthAndHeight(preferredSize, preferredSize)));
                }
            }

            StoreCached(cacheKey, resolvedIcon);
            return resolvedIcon;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
            return null;
        }
    }

    public static ImageSource? GetIconFromPath(string path, int preferredSize = 64)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            var resolvedPath = ResolveShortcutTarget(path);
            var cacheKey = BuildCacheKey("custom", resolvedPath, preferredSize);
            if (TryGetCached(cacheKey, out var cached))
            {
                return cached;
            }

            if (!File.Exists(resolvedPath) && !Directory.Exists(resolvedPath))
            {
                StoreCached(cacheKey, null);
                return null;
            }

            var ext = Path.GetExtension(resolvedPath).ToLowerInvariant();
            if (ext is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif")
            {
                var bitmap = LoadBitmap(resolvedPath, preferredSize);
                StoreCached(cacheKey, bitmap);
                return bitmap;
            }

            if (ext == ".ico")
            {
                using var icon = new Icon(resolvedPath);
                var source = FreezeIfPossible(Imaging.CreateBitmapSourceFromHIcon(
                    icon.Handle,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromWidthAndHeight(preferredSize, preferredSize)));
                StoreCached(cacheKey, source);
                return source;
            }

            var resolvedIcon = GetIcon(resolvedPath, preferredSize);
            StoreCached(cacheKey, resolvedIcon);
            return resolvedIcon;
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            return null;
        }
    }

    private static ImageSource? LoadBitmap(string path, int size)
    {
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth = Math.Max(size, 1);
            bitmap.UriSource = new Uri(path, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            return null;
        }
    }

    private static string BuildCacheKey(string kind, string path, int size)
    {
        return $"{kind}|{size}|{path}";
    }

    private static bool TryGetCached(string key, out ImageSource? image)
    {
        lock (CacheLock)
        {
            return Cache.TryGetValue(key, out image);
        }
    }

    private static void StoreCached(string key, ImageSource? image)
    {
        lock (CacheLock)
        {
            if (!Cache.ContainsKey(key))
            {
                if (CacheKeys.Count >= MaxCacheSize)
                {
                    var oldestKey = CacheKeys.Dequeue();
                    Cache.Remove(oldestKey);
                }

                CacheKeys.Enqueue(key);
            }

            Cache[key] = image;
        }
    }

    private static ImageSource? FreezeIfPossible(ImageSource? image)
    {
        if (image is Freezable freezable && freezable.CanFreeze && !freezable.IsFrozen)
        {
            freezable.Freeze();
        }

        return image;
    }

    private static string ResolveShortcutTarget(string path)
    {
        try
        {
            if (!path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
            {
                return path;
            }

            lock (CacheLock)
            {
                if (ShortcutTargetCache.TryGetValue(path, out var cachedTarget))
                {
                    return cachedTarget;
                }
            }

            var resolved = path;
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType != null)
            {
                var shellObj = Activator.CreateInstance(shellType);
                if (shellObj != null)
                {
                    dynamic shell = shellObj;
                    dynamic? shortcut = shell.CreateShortcut(path);
                    string? target = shortcut?.TargetPath as string;
                    if (!string.IsNullOrWhiteSpace(target))
                    {
                        resolved = target;
                    }
                }
            }

            lock (CacheLock)
            {
                ShortcutTargetCache[path] = resolved;
            }

            return resolved;
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }

        return path;
    }

    private static ImageSource? GetHighResIcon(string path, int size)
    {
        try
        {
            var handle = IntPtr.Zero;
            var large = new IntPtr[1];
            var count = ExtractIconEx(path, 0, large, null, 1);
            if (count > 0 && large[0] != IntPtr.Zero)
            {
                handle = large[0];
            }

            if (handle == IntPtr.Zero)
            {
                return null;
            }

            var source = Imaging.CreateBitmapSourceFromHIcon(
                handle,
                Int32Rect.Empty,
                BitmapSizeOptions.FromWidthAndHeight(size, size));
            DestroyIcon(handle);
            return source;
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            return null;
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern uint ExtractIconEx(
        string lpszFile,
        int nIconIndex,
        IntPtr[]? phiconLarge,
        IntPtr[]? phiconSmall,
        uint nIcons);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    // ---- High-quality 256px via SHGetImageList (jumbo) ----
    private static ImageSource? GetJumboIcon(string path)
    {
        try
        {
            var shinfo = new SHFILEINFO();
            var flags = SHGFI.SysIconIndex | SHGFI.LargeIcon;
            var ret = SHGetFileInfo(path, 0, ref shinfo, (uint)Marshal.SizeOf(shinfo), flags);
            if (ret == IntPtr.Zero)
            {
                return null;
            }

            var iidImageList = new Guid("46EB5926-582E-4017-9FDF-E8998DAA0950"); // IImageList
            if (SHGetImageList((int)SHIL.Jumbo, ref iidImageList, out var imageList) != 0)
            {
                // fallback to extra large if jumbo not available
                SHGetImageList((int)SHIL.ExtraLarge, ref iidImageList, out imageList);
            }

            if (imageList == null)
            {
                return null;
            }

            const int ILD_TRANSPARENT = 0x1;
            imageList.GetIcon(shinfo.iIcon, ILD_TRANSPARENT, out var hicon);
            if (hicon == IntPtr.Zero)
            {
                return null;
            }

            var source = Imaging.CreateBitmapSourceFromHIcon(
                hicon,
                Int32Rect.Empty,
                BitmapSizeOptions.FromWidthAndHeight(256, 256));
            DestroyIcon(hicon);
            return source;
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            return null;
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "SHGetFileInfoW")]
    private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, SHGFI uFlags);

    [DllImport("shell32.dll")]
    private static extern int SHGetImageList(int iImageList, ref Guid riid, out IImageList ppv);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    [Flags]
    private enum SHGFI : uint
    {
        Icon = 0x000000100,
        DisplayName = 0x000000200,
        Typename = 0x000000400,
        Attributes = 0x000000800,
        IconLocation = 0x000001000,
        ExeType = 0x000002000,
        SysIconIndex = 0x000004000,
        LinkOverlay = 0x000008000,
        Selected = 0x000010000,
        AttrSpecified = 0x000020000,
        LargeIcon = 0x000000000,
        SmallIcon = 0x000000001,
        OpenIcon = 0x000000002,
        ShellIconSize = 0x000000004,
        PIDL = 0x000000008,
        UseFileAttributes = 0x000000010,
        AddOverlays = 0x000000020,
        OverlayIndex = 0x000000040
    }

    private enum SHIL
    {
        Large = 0x0,
        Small = 0x1,
        ExtraLarge = 0x2,
        SysSmall = 0x3,
        Jumbo = 0x4
    }

    [ComImport]
    [Guid("46EB5926-582E-4017-9FDF-E8998DAA0950")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IImageList
    {
        [PreserveSig]
        int Add(IntPtr hbmImage, IntPtr hbmMask, ref int pi);
        [PreserveSig]
        int ReplaceIcon(int i, IntPtr hicon, ref int pi);
        [PreserveSig]
        int SetOverlayImage(int iImage, int iOverlay);
        [PreserveSig]
        int Replace(int i, IntPtr hbmImage, IntPtr hbmMask);
        [PreserveSig]
        int AddMasked(IntPtr hbmImage, int crMask, ref int pi);
        [PreserveSig]
        int Draw(ref IMAGELISTDRAWPARAMS pimldp);
        [PreserveSig]
        int Remove(int i);
        [PreserveSig]
        int GetIcon(int i, int flags, out IntPtr picon);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IMAGELISTDRAWPARAMS
    {
        public int cbSize;
        public IntPtr himl;
        public int i;
        public IntPtr hdcDst;
        public int x;
        public int y;
        public int cx;
        public int cy;
        public int xBitmap;    // not used
        public int yBitmap;    // not used
        public int rgbBk;
        public int rgbFg;
        public int fStyle;
        public int dwRop;
        public int fState;
        public int Frame;
        public int crEffect;
    }
}
