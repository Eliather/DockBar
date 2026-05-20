using System;
using System.Drawing;
using System.IO;
using System.Windows;
using DockBar.Services;
using WinForms = System.Windows.Forms;

namespace DockBar;

public partial class App : System.Windows.Application
{
    private static readonly TimeSpan TrayMenuReopenGuard = TimeSpan.FromMilliseconds(150);
    private WinForms.NotifyIcon? _notifyIcon;
    private MainWindow? _window;
    private TrayMenuWindow? _trayMenu;
    private bool _trayMenuToggleQueued;
    private DateTime _lastTrayMenuClosedUtc;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _window = new MainWindow();
        _window.Show();
        CreateTrayIcon();
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
        if (e.Button != WinForms.MouseButtons.Right)
        {
            return;
        }

        if (_trayMenuToggleQueued)
        {
            return;
        }

        _trayMenuToggleQueued = true;
        var anchorPoint = WinForms.Control.MousePosition;
        Dispatcher.BeginInvoke(new Action(() =>
        {
            try
            {
                ToggleTrayMenu(anchorPoint);
            }
            finally
            {
                _trayMenuToggleQueued = false;
            }
        }));
    }

    private void ToggleTrayMenu(System.Drawing.Point anchorPoint)
    {
        if (_trayMenu != null)
        {
            _trayMenu.RequestClose();
            return;
        }

        if (DateTime.UtcNow - _lastTrayMenuClosedUtc < TrayMenuReopenGuard)
        {
            return;
        }

        var menu = new TrayMenuWindow();
        menu.OpenRequested += (_, _) => ShowWindow();
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
        menu.ShowAt(anchorPoint);
    }

    private static Icon LoadTrayIcon()
    {
        try
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var iconPath = Path.Combine(baseDir, "Dock.ico");
            if (File.Exists(iconPath))
            {
                return new Icon(iconPath);
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

        _window.Show();
        _window.Activate();
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
}
