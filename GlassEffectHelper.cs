using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace DockBar;

internal static class GlassEffectHelper
{
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;
    private const int DWMWA_MICA_EFFECT = 1029;

    private const int DWMSBT_AUTO = 0;
    private const int DWMSBT_NONE = 1;
    private const int DWMSBT_MAINWINDOW = 2;       // Mica
    private const int DWMSBT_TRANSIENTWINDOW = 3;   // Acrylic
    private const int DWMSBT_TABBEDWINDOW = 4;      // Mica Alt

    public static void Apply(Window window, bool enable, bool isDarkMode)
    {
        if (window == null) return;

        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
        {
            void OnSourceInitialized(object? sender, EventArgs e)
            {
                window.SourceInitialized -= OnSourceInitialized;
                var h = new WindowInteropHelper(window).Handle;
                ApplyInternal(window, h, enable, isDarkMode);
            }
            window.SourceInitialized += OnSourceInitialized;
            return;
        }

        ApplyInternal(window, hwnd, enable, isDarkMode);
    }

    private static void ApplyInternal(Window window, IntPtr hwnd, bool enable, bool isDarkMode)
    {
        try
        {
            var hwndSource = HwndSource.FromHwnd(hwnd);
            if (hwndSource?.CompositionTarget != null)
            {
                hwndSource.CompositionTarget.BackgroundColor = System.Windows.Media.Colors.Transparent;
            }

            if (!enable)
            {
                DisableInternal(hwnd);
                return;
            }

            var hrComp = DwmIsCompositionEnabled(out var compEnabled);
            if (hrComp != 0 || !compEnabled)
            {
                DisableInternal(hwnd);
                return;
            }

            // Disable Windows 11 Acrylic system backdrop to eliminate milky fog/noise overlay
            var build = Environment.OSVersion.Version.Build;
            if (build >= 22000)
            {
                var none = DWMSBT_NONE;
                DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref none, sizeof(int));
            }

            // Extend frame into client area
            var margins = new MARGINS { cxLeftWidth = -1, cxRightWidth = -1, cyTopHeight = -1, cyBottomHeight = -1 };
            DwmExtendFrameIntoClientArea(hwnd, ref margins);

            // Set dark/light backdrop mode for DWM
            var darkModeVal = isDarkMode ? 1 : 0;
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkModeVal, sizeof(int));

            // Apply ACCENT_ENABLE_TRANSPARENTGRADIENT for true crystal-clear per-pixel alpha transparency
            ApplyAccentPolicy(hwnd, AccentState.ACCENT_ENABLE_TRANSPARENTGRADIENT, 0);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GlassEffectHelper.ApplyInternal] {ex.Message}");
        }
    }

    private static void DisableInternal(IntPtr hwnd)
    {
        try
        {
            var build = Environment.OSVersion.Version.Build;
            if (build >= 22000)
            {
                var none = DWMSBT_NONE;
                DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref none, sizeof(int));
            }

            ApplyAccentPolicy(hwnd, AccentState.ACCENT_DISABLED, 0);

            var margins = new MARGINS { cxLeftWidth = 0, cxRightWidth = 0, cyTopHeight = 0, cyBottomHeight = 0 };
            DwmExtendFrameIntoClientArea(hwnd, ref margins);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GlassEffectHelper.DisableInternal] {ex.Message}");
        }
    }

    private static void ApplyAccentPolicy(IntPtr hwnd, AccentState state, uint gradientColor = 0)
    {
        try
        {
            var policy = new AccentPolicy
            {
                AccentState = state,
                AccentFlags = (state == AccentState.ACCENT_DISABLED) ? 0 : 2,
                GradientColor = gradientColor,
                AnimationId = 0
            };

            var size = Marshal.SizeOf(policy);
            var ptr = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(policy, ptr, false);
                var data = new WindowCompositionAttributeData
                {
                    Attribute = WindowCompositionAttribute.WCA_ACCENT_POLICY,
                    Data = ptr,
                    SizeOfData = size
                };
                SetWindowCompositionAttribute(hwnd, ref data);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GlassEffectHelper.ApplyAccentPolicy] {ex.Message}");
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MARGINS
    {
        public int cxLeftWidth;
        public int cxRightWidth;
        public int cyTopHeight;
        public int cyBottomHeight;
    }

    private enum AccentState
    {
        ACCENT_DISABLED = 0,
        ACCENT_ENABLE_GRADIENT = 1,
        ACCENT_ENABLE_TRANSPARENTGRADIENT = 2,
        ACCENT_ENABLE_BLURBEHIND = 3,
        ACCENT_ENABLE_ACRYLICBLURBEHIND = 4,
        ACCENT_ENABLE_HOSTBACKDROP = 5,
        ACCENT_INVALID_STATE = 6
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AccentPolicy
    {
        public AccentState AccentState;
        public int AccentFlags;
        public uint GradientColor;
        public int AnimationId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowCompositionAttributeData
    {
        public WindowCompositionAttribute Attribute;
        public IntPtr Data;
        public int SizeOfData;
    }

    private enum WindowCompositionAttribute
    {
        WCA_ACCENT_POLICY = 19
    }

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS pMargins);

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmIsCompositionEnabled(out bool pfEnabled);

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

    [DllImport("user32.dll")]
    private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);
}
