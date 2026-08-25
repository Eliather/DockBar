using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace DockBar.Services;

public static class AutoStartService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "DockBar";
    private const string StartupTaskId = "DockBarStartupTask";

    public static void Apply(bool enable)
    {
        if (PackageHelper.IsPackaged)
        {
            _ = ApplyPackagedAsync(enable);
            return;
        }

        ApplyRegistry(enable);
    }

    public static async Task ApplyAsync(bool enable)
    {
        if (PackageHelper.IsPackaged)
        {
            await ApplyPackagedAsync(enable);
            return;
        }

        ApplyRegistry(enable);
    }

    public static bool IsEnabled()
    {
        if (PackageHelper.IsPackaged)
        {
            try
            {
                var task = Windows.ApplicationModel.StartupTask.GetAsync(StartupTaskId).AsTask().GetAwaiter().GetResult();
                return task.State == Windows.ApplicationModel.StartupTaskState.Enabled ||
                       task.State == Windows.ApplicationModel.StartupTaskState.EnabledByPolicy;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                return false;
            }
        }

        return IsEnabledRegistry();
    }

    public static async Task<bool> IsEnabledAsync()
    {
        if (PackageHelper.IsPackaged)
        {
            try
            {
                var task = await Windows.ApplicationModel.StartupTask.GetAsync(StartupTaskId);
                return task.State == Windows.ApplicationModel.StartupTaskState.Enabled ||
                       task.State == Windows.ApplicationModel.StartupTaskState.EnabledByPolicy;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                return false;
            }
        }

        return IsEnabledRegistry();
    }

    private static async Task ApplyPackagedAsync(bool enable)
    {
        try
        {
            var task = await Windows.ApplicationModel.StartupTask.GetAsync(StartupTaskId);
            if (enable)
            {
                if (task.State == Windows.ApplicationModel.StartupTaskState.Disabled)
                {
                    await task.RequestEnableAsync();
                }
            }
            else
            {
                if (task.State == Windows.ApplicationModel.StartupTaskState.Enabled)
                {
                    task.Disable();
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }

    private static void ApplyRegistry(bool enable)
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

    private static bool IsEnabledRegistry()
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
