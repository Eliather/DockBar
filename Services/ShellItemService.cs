using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DockBar.Services;

public static class ShellItemService
{
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
            if (SHCreateItemFromParsingName(shellPath, IntPtr.Zero, typeof(IShellItem).GUID, out var itemPtr) != 0 || itemPtr == IntPtr.Zero)
            {
                return null;
            }

            var item = (IShellItem)Marshal.GetObjectForIUnknown(itemPtr);
            item.GetDisplayName(SIGDN.NORMALDISPLAY, out var namePtr);
            var name = Marshal.PtrToStringUni(namePtr);
            Marshal.FreeCoTaskMem(namePtr);
            Marshal.ReleaseComObject(item);
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
            if (SHCreateItemFromParsingName(shellPath, IntPtr.Zero, typeof(IShellItemImageFactory).GUID, out var factoryPtr) != 0 || factoryPtr == IntPtr.Zero)
            {
                return null;
            }

            var factory = (IShellItemImageFactory)Marshal.GetObjectForIUnknown(factoryPtr);
            var hresult = factory.GetImage(new SIZE { cx = size, cy = size }, SIIGBF.RESIZETOFIT, out var hbitmap);
            if (hresult != 0 || hbitmap == IntPtr.Zero)
            {
                Marshal.ReleaseComObject(factory);
                return null;
            }

            var source = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                hbitmap,
                IntPtr.Zero,
                Int32Rect.Empty,
                BitmapSizeOptions.FromWidthAndHeight(size, size));

            DeleteObject(hbitmap);
            Marshal.ReleaseComObject(factory);
            return source;
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            return null;
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
