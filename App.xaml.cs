using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using DockBar.Services;
using WinForms = System.Windows.Forms;

namespace DockBar;

public partial class App : System.Windows.Application
{
    private static readonly TimeSpan TrayMenuReopenGuard = TimeSpan.FromMilliseconds(400);
    private static readonly FieldInfo? NotifyIconIdField = typeof(WinForms.NotifyIcon).GetField("id", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo? NotifyIconWindowField = typeof(WinForms.NotifyIcon).GetField("window", BindingFlags.Instance | BindingFlags.NonPublic);
    private WinForms.NotifyIcon? _notifyIcon;
    private MainWindow? _window;
    private TrayMenuWindow? _trayMenu;
    private bool _trayMenuToggleQueued;
    private DateTime _lastTrayMenuClosedUtc;

    protected override void OnStartup(StartupEventArgs e)
    {
        try
        {
            base.OnStartup(e);

            _window = new MainWindow();
            _window.Show();
            CreateTrayIcon();
        }
        catch (Exception ex)
        {
            File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log"), ex.ToString());
            System.Windows.MessageBox.Show(ex.ToString(), "DockBar Startup Error");
        }
    }

    private void CreateTrayIcon()
    {
        var trayIcon = LoadTrayIcon();
        _notifyIcon = new WinForms.NotifyIcon
        {
            Text = "DockBar",
            Icon = trayIcon,
            Visible = true
        };

        _notifyIcon.MouseUp += NotifyIcon_MouseUp;
        _notifyIcon.DoubleClick += (_, _) => ShowWindow();
    }

    private void NotifyIcon_MouseUp(object? sender, WinForms.MouseEventArgs e)
    {
        if (e.Button == WinForms.MouseButtons.Left)
        {
            ShowWindow();
            return;
        }

        if (e.Button != WinForms.MouseButtons.Right)
        {
            return;
        }

        if (_trayMenuToggleQueued)
        {
            return;
        }

        _trayMenuToggleQueued = true;
        var anchorBounds = GetTrayAnchorBounds() ?? CreateFallbackAnchorBounds(WinForms.Control.MousePosition);
        Dispatcher.BeginInvoke(new Action(() =>
        {
            try
            {
                ToggleTrayMenu(anchorBounds);
            }
            finally
            {
                _trayMenuToggleQueued = false;
            }
        }));
    }

    private void ToggleTrayMenu(System.Drawing.Rectangle anchorBounds)
    {
        if (_trayMenu != null)
        {
            return;
        }

        if (DateTime.UtcNow - _lastTrayMenuClosedUtc < TrayMenuReopenGuard)
        {
            return;
        }

        var menu = new TrayMenuWindow();
        menu.PauseRequested += (_, _) => _window?.TogglePause();
        menu.ToggleSideRequested += (_, _) => _window?.ToggleDockSide();
        menu.SettingsRequested += (_, _) => OpenSettingsWindow();
        menu.UpdateRequested += async (_, _) =>
        {
            if (_window == null)
            {
                _window = new MainWindow();
            }

            await _window.CheckForUpdatesAsync(true);
        };
        menu.ConfigFolderRequested += (_, _) => OpenConfigFolder();
        menu.ExitRequested += (_, _) => ExitApp();
        menu.Closed += (_, _) =>
        {
            if (ReferenceEquals(_trayMenu, menu))
            {
                _trayMenu = null;
                _lastTrayMenuClosedUtc = DateTime.UtcNow;
            }
        };

        _trayMenu = menu;
        menu.ShowAt(anchorBounds);
    }

    private System.Drawing.Rectangle? GetTrayAnchorBounds()
    {
        if (_notifyIcon == null || NotifyIconIdField == null || NotifyIconWindowField == null)
        {
            return null;
        }

        try
        {
            var id = (int)(NotifyIconIdField.GetValue(_notifyIcon) ?? 0);
            var nativeWindow = NotifyIconWindowField.GetValue(_notifyIcon) as WinForms.NativeWindow;
            var handle = nativeWindow?.Handle ?? IntPtr.Zero;
            if (id <= 0 || handle == IntPtr.Zero)
            {
                return null;
            }

            var identifier = new NOTIFYICONIDENTIFIER
            {
                cbSize = (uint)Marshal.SizeOf<NOTIFYICONIDENTIFIER>(),
                hWnd = handle,
                uID = (uint)id
            };

            if (Shell_NotifyIconGetRect(ref identifier, out var rect) != 0)
            {
                return null;
            }

            return System.Drawing.Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
            return null;
        }
    }

    private static System.Drawing.Rectangle CreateFallbackAnchorBounds(System.Drawing.Point point)
    {
        const int size = 16;
        var half = size / 2;
        return new System.Drawing.Rectangle(point.X - half, point.Y - half, size, size);
    }

    private static Icon LoadTrayIcon()
    {
        try
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var iconPath = Path.Combine(baseDir, "Dock.ico");
            if (File.Exists(iconPath))
            {
                const uint IMAGE_ICON = 1;
                const uint LR_LOADFROMFILE = 0x00000010;
                var hIcon = LoadImage(IntPtr.Zero, iconPath, IMAGE_ICON, 16, 16, LR_LOADFROMFILE);
                if (hIcon != IntPtr.Zero)
                {
                    return Icon.FromHandle(hIcon);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
        }

        return SystemIcons.Application;
    }

    private void ShowWindow()
    {
        if (_window == null)
        {
            _window = new MainWindow();
        }

        if (_window.WindowState == WindowState.Minimized)
        {
            _window.WindowState = WindowState.Normal;
        }

        _window.RevealDockOnCurrentSide();
        _window.Activate();
        _window.Focus();
    }

    private void OpenSettingsWindow()
    {
        if (_window == null)
        {
            _window = new MainWindow();
        }

        _window.OpenSettings();
    }

    private void OpenConfigFolder()
    {
        try
        {
            var folder = Services.ConfigService.ConfigDirectory;
            System.IO.Directory.CreateDirectory(folder);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = folder,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
        }
    }

    private void ExitApp()
    {
        _trayMenu?.RequestClose();
        _notifyIcon?.Dispose();
        _window?.Close();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayMenu?.RequestClose();
        _notifyIcon?.Dispose();
        base.OnExit(e);
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr LoadImage(IntPtr hinst, string lpszName, uint uType, int cxDesired, int cyDesired, uint fuLoad);

    [DllImport("shell32.dll")]
    private static extern int Shell_NotifyIconGetRect(ref NOTIFYICONIDENTIFIER identifier, out RECT iconLocation);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NOTIFYICONIDENTIFIER
    {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uID;
        public Guid guidItem;
    }
}
