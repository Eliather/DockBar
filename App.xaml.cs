using System;
using System.Drawing;
using System.IO;
using System.Windows;
using DockBar.Services;
using WinForms = System.Windows.Forms;

namespace DockBar;

public partial class App : System.Windows.Application
{
    private WinForms.NotifyIcon? _notifyIcon;
    private MainWindow? _window;

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

        var contextMenu = new WinForms.ContextMenuStrip();
        contextMenu.Items.Add(LocalizationService.Get("Tray_Open"), null, (_, _) => ShowWindow());
        contextMenu.Items.Add(LocalizationService.Get("Tray_ToggleSide"), null, (_, _) => _window?.ToggleDockSide());
        contextMenu.Items.Add(LocalizationService.Get("Tray_Settings"), null, (_, _) => OpenSettingsWindow());
        contextMenu.Items.Add(LocalizationService.Get("Tray_ConfigFolder"), null, (_, _) => OpenConfigFolder());
        contextMenu.Items.Add(LocalizationService.Get("Tray_Exit"), null, (_, _) => ExitApp());
        _notifyIcon.ContextMenuStrip = contextMenu;
        _notifyIcon.DoubleClick += (_, _) => ShowWindow();
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
        _notifyIcon?.Dispose();
        _window?.Close();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _notifyIcon?.Dispose();
        base.OnExit(e);
    }
}
