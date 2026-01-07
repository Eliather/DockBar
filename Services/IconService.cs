using System;
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
    public static ImageSource? GetIcon(string path, int preferredSize = 64)
    {
        try
        {
            var resolvedPath = ResolveShortcutTarget(path);

            if (!File.Exists(resolvedPath) && !Directory.Exists(resolvedPath))
            {
                return null;
            }

            var jumbo = GetJumboIcon(resolvedPath);
            if (jumbo != null)
            {
                return jumbo;
            }

            // Try high-res extraction first.
            var high = GetHighResIcon(resolvedPath, preferredSize);
            if (high != null)
            {
                return high;
            }

            using var icon = Icon.ExtractAssociatedIcon(resolvedPath);
            if (icon == null)
            {
                return null;
            }

            return Imaging.CreateBitmapSourceFromHIcon(
                icon.Handle,
                Int32Rect.Empty,
                BitmapSizeOptions.FromWidthAndHeight(preferredSize, preferredSize));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
            return null;
        }
    }

    private static string ResolveShortcutTarget(string path)
    {
        try
        {
            if (!path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
            {
                return path;
            }

            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null)
            {
                return path;
            }

            var shellObj = Activator.CreateInstance(shellType);
            if (shellObj == null)
            {
                return path;
            }

            dynamic shell = shellObj;
            dynamic? shortcut = shell.CreateShortcut(path);
            string? target = shortcut?.TargetPath as string;
            if (!string.IsNullOrWhiteSpace(target))
            {
                return target;
            }
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

            using var icon = Icon.FromHandle(handle);
            var source = Imaging.CreateBitmapSourceFromHIcon(
                icon.Handle,
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

            using var icon = Icon.FromHandle(hicon);
            var source = Imaging.CreateBitmapSourceFromHIcon(
                icon.Handle,
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

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, SHGFI uFlags);

    [DllImport("shell32.dll")]
    private static extern int SHGetImageList(int iImageList, ref Guid riid, out IImageList ppv);

    [StructLayout(LayoutKind.Sequential)]
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
