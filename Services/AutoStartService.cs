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
#if DEBUG
        try
        {
            // En desarrollo (DEBUG), nunca registramos ejecutables de desarrollo (bin\Debug, etc.)
            // para evitar que Windows ejecute la versión en desarrollo en el arranque del sistema.
            // Si el registro quedó apuntando previamente a una carpeta de compilación, lo limpiamos.
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, true);
            var currentVal = key?.GetValue(ValueName) as string;
            if (!string.IsNullOrWhiteSpace(currentVal) &&
                (currentVal.Contains(@"\bin\Debug\", StringComparison.OrdinalIgnoreCase) ||
                 currentVal.Contains(@"\bin\Release\", StringComparison.OrdinalIgnoreCase)))
            {
                key?.DeleteValue(ValueName, false);
                Debug.WriteLine("[AutoStartService] Se eliminó la ruta de desarrollo residual del inicio de Windows.");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
        return;
#else
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
#endif
    }

    private static bool IsEnabledRegistry()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            var value = key?.GetValue(ValueName) as string;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

#if DEBUG
            if (value.Contains(@"\bin\Debug\", StringComparison.OrdinalIgnoreCase) ||
                value.Contains(@"\bin\Release\", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
#endif
            return true;
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
