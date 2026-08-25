using System;
using System.Runtime.InteropServices;
using System.Text;

namespace DockBar.Services;

public static class PackageHelper
{
    private const int APPMODEL_ERROR_NO_PACKAGE = 15700;
    private static readonly Lazy<bool> IsPackagedLazy = new(CheckIsPackaged);

    /// <summary>
    /// Returns true if the current process is running inside an MSIX / AppX package container.
    /// </summary>
    public static bool IsPackaged => IsPackagedLazy.Value;

    private static bool CheckIsPackaged()
    {
        try
        {
            var length = 0;
            var result = GetCurrentPackageFullName(ref length, null);
            return result != APPMODEL_ERROR_NO_PACKAGE;
        }
        catch
        {
            return false;
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetCurrentPackageFullName(ref int packageFullNameLength, StringBuilder? packageFullName);
}
