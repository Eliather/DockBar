using System;
using System.Diagnostics;
using Microsoft.Win32;

namespace DockBar.Services;

public static class AutoStartService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "DockBar";

    public static void Apply(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey);
            if (key == null)
            {
                return;
            }

            if (enable)
            {
                key.SetValue(ValueName, GetExecutablePath());
            }
            else
            {
                key.DeleteValue(ValueName, false);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }

    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            var value = key?.GetValue(ValueName) as string;
            return !string.IsNullOrWhiteSpace(value);
        }
        catch
        {
            return false;
        }
    }

    private static string GetExecutablePath()
    {
        var path = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(path))
        {
            path = Process.GetCurrentProcess().MainModule?.FileName;
        }
        return path ?? "DockBar.exe";
    }
}
