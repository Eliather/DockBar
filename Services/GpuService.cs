using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;

namespace DockBar.Services;

public class GpuDevice : INotifyPropertyChanged
{
    public int Index { get; set; }
    public string Name { get; set; } = "";
    public string DefaultLabel { get; set; } = "GPU";
    public string ShortModelName { get; set; } = "";
    public string LuidPattern { get; set; } = "";
    public long DedicatedVramBytes { get; set; }
    public string DedicatedVramText { get; set; } = "";

    private string _label = "GPU";
    public string Label
    {
        get => _label;
        set
        {
            if (_label != value)
            {
                _label = value;
                OnPropertyChanged();
            }
        }
    }

    public void UpdateLabel(bool showHardwareNames)
    {
        if (showHardwareNames)
        {
            if (!string.IsNullOrWhiteSpace(ShortModelName))
                Label = ShortModelName;
            else
                Label = DefaultLabel;
        }
        else
        {
            Label = DefaultLabel;
        }
    }

    private int _usagePercent;
    public int UsagePercent
    {
        get => _usagePercent;
        set
        {
            if (_usagePercent != value)
            {
                _usagePercent = value;
                OnPropertyChanged();
            }
        }
    }

    private string _usageText = "0%";
    public string UsageText
    {
        get => _usageText;
        set
        {
            if (_usageText != value)
            {
                _usageText = value;
                OnPropertyChanged();
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class GpuService : IDisposable
{
    private static readonly Lazy<GpuService> _instance = new(() => new GpuService());
    public static GpuService Instance => _instance.Value;

    public ObservableCollection<GpuDevice> Devices { get; } = new();
    public int DeviceCount => Devices.Count;
    public bool HasGpus => Devices.Count > 0;
    public string DetectedGpusSummary { get; private set; } = "";

    private IntPtr _hPdhQuery = IntPtr.Zero;
    private IntPtr _hPdhCounter = IntPtr.Zero;
    private bool _initialized;
    private bool _pdhInitialized;
    private readonly object _pdhLock = new();

    #region DXGI Interop Definitions
    [DllImport("dxgi.dll")]
    private static extern int CreateDXGIFactory([MarshalAs(UnmanagedType.LPStruct)] Guid riid, out IntPtr ppFactory);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DXGI_ADAPTER_DESC
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string Description;
        public uint VendorId;
        public uint DeviceId;
        public uint SubSysId;
        public uint Revision;
        public UIntPtr DedicatedVideoMemory;
        public UIntPtr DedicatedSystemMemory;
        public UIntPtr SharedSystemMemory;
        public long AdapterLuid;
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int EnumAdaptersDelegate(IntPtr thisPtr, uint adapterIndex, out IntPtr ppAdapter);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int GetDescDelegate(IntPtr thisPtr, out DXGI_ADAPTER_DESC pDesc);
    #endregion

    #region PDH Interop Definitions
    [DllImport("pdh.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern int PdhOpenQuery(string? szDataSource, IntPtr dwUserData, out IntPtr phQuery);

    [DllImport("pdh.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern int PdhAddEnglishCounter(IntPtr hQuery, string szFullCounterPath, IntPtr dwUserData, out IntPtr phCounter);

    [DllImport("pdh.dll")]
    private static extern int PdhCollectQueryData(IntPtr hQuery);

    [DllImport("pdh.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern int PdhGetFormattedCounterArray(
        IntPtr hCounter,
        uint dwFormat,
        ref uint lpdwBufferSize,
        ref uint lpdwItemCount,
        IntPtr itemBuffer
    );

    [DllImport("pdh.dll")]
    private static extern int PdhCloseQuery(IntPtr hQuery);

    private const uint PDH_FMT_DOUBLE = 0x00000200;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct PDH_FMT_COUNTERVALUE_ITEM
    {
        public IntPtr szName;
        public uint CStatus;
        public int dummy; // 4-byte padding on 64-bit
        public double doubleValue;
    }
    #endregion

    public void Initialize()
    {
        if (_initialized) return;
        _initialized = true;

        try
        {
            EnumerateGpus();
            InitializePdh();
        }
        catch { }
    }

    private void EnumerateGpus()
    {
        Devices.Clear();
        var detectedList = new List<GpuDevice>();

        try
        {
            Guid dxgiFactoryGuid = new("7b7166ec-21c7-44ae-b21a-c9ae321ae369");
            int hr = CreateDXGIFactory(dxgiFactoryGuid, out IntPtr pFactory);
            if (hr == 0 && pFactory != IntPtr.Zero)
            {
                try
                {
                    IntPtr factoryVTable = Marshal.ReadIntPtr(pFactory);
                    IntPtr enumAdaptersPtr = Marshal.ReadIntPtr(factoryVTable, 7 * IntPtr.Size);
                    var enumAdapters = (EnumAdaptersDelegate)Marshal.GetDelegateForFunctionPointer(enumAdaptersPtr, typeof(EnumAdaptersDelegate));

                    uint index = 0;
                    while (true)
                    {
                        int res = enumAdapters(pFactory, index, out IntPtr pAdapter);
                        if (res != 0 || pAdapter == IntPtr.Zero) break;

                        try
                        {
                            IntPtr adapterVTable = Marshal.ReadIntPtr(pAdapter);
                            IntPtr getDescPtr = Marshal.ReadIntPtr(adapterVTable, 8 * IntPtr.Size);
                            var getDesc = (GetDescDelegate)Marshal.GetDelegateForFunctionPointer(getDescPtr, typeof(GetDescDelegate));

                            int descRes = getDesc(pAdapter, out DXGI_ADAPTER_DESC desc);
                            if (descRes == 0)
                            {
                                string name = desc.Description?.Trim() ?? "";

                                // Filter out Microsoft software renderers / WARP
                                bool isSoftware = desc.VendorId == 0x1414 ||
                                                  name.Contains("Microsoft Basic Render", StringComparison.OrdinalIgnoreCase) ||
                                                  name.Contains("Microsoft Remote Display", StringComparison.OrdinalIgnoreCase);

                                if (!isSoftware && !string.IsNullOrEmpty(name))
                                {
                                    uint luidHigh = (uint)(desc.AdapterLuid >> 32);
                                    uint luidLow = (uint)(desc.AdapterLuid & 0xFFFFFFFF);
                                    string luidPattern = $"_luid_0x{luidHigh:x8}_0x{luidLow:x8}_";

                                    long vramBytes = (long)desc.DedicatedVideoMemory.ToUInt64();
                                    string vramText;
                                    if (vramBytes >= 1024L * 1024L * 1024L)
                                    {
                                        vramText = $"{vramBytes / (1024.0 * 1024.0 * 1024.0):F1} GB";
                                    }
                                    else
                                    {
                                        vramText = $"{vramBytes / (1024.0 * 1024.0):F0} MB";
                                    }

                                    string shortGpu = CleanGpuName(name);
                                    detectedList.Add(new GpuDevice
                                    {
                                        Index = detectedList.Count,
                                        Name = name,
                                        ShortModelName = shortGpu,
                                        LuidPattern = luidPattern,
                                        DedicatedVramBytes = vramBytes,
                                        DedicatedVramText = vramText,
                                        UsagePercent = 0,
                                        UsageText = "0%"
                                    });
                                }
                            }
                        }
                        finally
                        {
                            Marshal.Release(pAdapter);
                        }

                        index++;
                    }
                }
                finally
                {
                    Marshal.Release(pFactory);
                }
            }
        }
        catch { }

        // Configure dynamic labels based on total GPU count
        if (detectedList.Count == 1)
        {
            detectedList[0].DefaultLabel = "GPU";
            detectedList[0].Label = "GPU";
        }
        else
        {
            for (int i = 0; i < detectedList.Count; i++)
            {
                detectedList[i].DefaultLabel = $"GPU {i}";
                detectedList[i].Label = $"GPU {i}";
            }
        }

        foreach (var device in detectedList)
        {
            Devices.Add(device);
        }

        if (detectedList.Count > 0)
        {
            var names = new List<string>();
            foreach (var d in detectedList)
            {
                names.Add(d.Name);
            }
            DetectedGpusSummary = string.Join(", ", names);
        }
        else
        {
            DetectedGpusSummary = "";
        }
    }

    public static string CleanGpuName(string? fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName)) return "GPU";
        string clean = fullName
            .Replace("(R)", "", StringComparison.OrdinalIgnoreCase)
            .Replace("(TM)", "", StringComparison.OrdinalIgnoreCase);

        if (clean.Contains("Intel", StringComparison.OrdinalIgnoreCase) &&
            (clean.Contains("Graphics", StringComparison.OrdinalIgnoreCase) || clean.Contains("HD", StringComparison.OrdinalIgnoreCase) || clean.Contains("UHD", StringComparison.OrdinalIgnoreCase)))
        {
            if (clean.Contains("Iris", StringComparison.OrdinalIgnoreCase))
                return "Iris Xe";
            if (clean.Contains("Arc", StringComparison.OrdinalIgnoreCase))
                return "Intel Arc";
            return "Intel GPU";
        }

        // AMD integrated graphics (APUs) without discrete RX brand
        if ((clean.Contains("AMD", StringComparison.OrdinalIgnoreCase) || clean.Contains("Radeon", StringComparison.OrdinalIgnoreCase)) &&
            !clean.Contains("RX ", StringComparison.OrdinalIgnoreCase))
        {
            if (clean.Contains("Vega", StringComparison.OrdinalIgnoreCase))
            {
                var vMatch = System.Text.RegularExpressions.Regex.Match(clean, @"Vega\s*\d+", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (vMatch.Success) return vMatch.Value;
                return "Vega GPU";
            }
            var mMatch = System.Text.RegularExpressions.Regex.Match(clean, @"\d{3}M", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (mMatch.Success) return $"Radeon {mMatch.Value}";

            if (clean.Trim().Equals("AMD Radeon Graphics", StringComparison.OrdinalIgnoreCase) ||
                clean.Trim().Equals("Radeon Graphics", StringComparison.OrdinalIgnoreCase))
            {
                return "Radeon GPU";
            }
        }

        clean = clean
            .Replace("NVIDIA ", "", StringComparison.OrdinalIgnoreCase)
            .Replace("GeForce ", "", StringComparison.OrdinalIgnoreCase)
            .Replace("AMD ", "", StringComparison.OrdinalIgnoreCase)
            .Replace("Radeon ", "", StringComparison.OrdinalIgnoreCase)
            .Replace("Laptop GPU", "", StringComparison.OrdinalIgnoreCase)
            .Replace("Desktop GPU", "", StringComparison.OrdinalIgnoreCase)
            .Replace(" Series", "", StringComparison.OrdinalIgnoreCase)
            .Replace(" Graphics", "", StringComparison.OrdinalIgnoreCase);
        clean = System.Text.RegularExpressions.Regex.Replace(clean, @"\s+", " ").Trim();
        return string.IsNullOrEmpty(clean) ? "GPU" : clean;
    }

    private void InitializePdh()
    {
        lock (_pdhLock)
        {
            try
            {
                if (PdhOpenQuery(null, IntPtr.Zero, out _hPdhQuery) == 0 && _hPdhQuery != IntPtr.Zero)
                {
                    if (PdhAddEnglishCounter(_hPdhQuery, @"\GPU Engine(*)\Utilization Percentage", IntPtr.Zero, out _hPdhCounter) == 0)
                    {
                        PdhCollectQueryData(_hPdhQuery);
                        _pdhInitialized = true;
                    }
                }
            }
            catch
            {
                _pdhInitialized = false;
            }
        }
    }

    public Task UpdateUsageAsync()
    {
        if (!_pdhInitialized || Devices.Count == 0 || _hPdhQuery == IntPtr.Zero || _hPdhCounter == IntPtr.Zero)
        {
            return Task.CompletedTask;
        }

        return Task.Run(() =>
        {
            lock (_pdhLock)
            {
                if (_hPdhQuery == IntPtr.Zero || _hPdhCounter == IntPtr.Zero) return;

                int res = PdhCollectQueryData(_hPdhQuery);
                if (res != 0) return;

                uint bufferSize = 0;
                uint itemCount = 0;
                PdhGetFormattedCounterArray(_hPdhCounter, PDH_FMT_DOUBLE, ref bufferSize, ref itemCount, IntPtr.Zero);
                if (bufferSize == 0 || itemCount == 0) return;

                IntPtr buffer = Marshal.AllocHGlobal((int)bufferSize);
                try
                {
                    res = PdhGetFormattedCounterArray(_hPdhCounter, PDH_FMT_DOUBLE, ref bufferSize, ref itemCount, buffer);
                    if (res != 0) return;

                    int itemSize = Marshal.SizeOf<PDH_FMT_COUNTERVALUE_ITEM>();
                    var usageSums = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

                    for (int i = 0; i < itemCount; i++)
                    {
                        IntPtr itemPtr = new(buffer.ToInt64() + i * itemSize);
                        var item = Marshal.PtrToStructure<PDH_FMT_COUNTERVALUE_ITEM>(itemPtr);

                        if (item.szName != IntPtr.Zero && item.doubleValue > 0.001)
                        {
                            string? name = Marshal.PtrToStringUni(item.szName);
                            if (name != null)
                            {
                                foreach (var dev in Devices)
                                {
                                    if (name.Contains(dev.LuidPattern, StringComparison.OrdinalIgnoreCase))
                                    {
                                        usageSums.TryGetValue(dev.LuidPattern, out double current);
                                        usageSums[dev.LuidPattern] = current + item.doubleValue;
                                        break;
                                    }
                                }
                            }
                        }
                    }

                    System.Windows.Application.Current?.Dispatcher?.InvokeAsync(() =>
                    {
                        foreach (var dev in Devices)
                        {
                            usageSums.TryGetValue(dev.LuidPattern, out double sum);
                            int pct = (int)Math.Clamp(Math.Round(sum), 0, 100);
                            dev.UsagePercent = pct;
                            dev.UsageText = $"{pct}%";
                        }
                    });
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
        });
    }

    public void Dispose()
    {
        lock (_pdhLock)
        {
            if (_hPdhQuery != IntPtr.Zero)
            {
                try
                {
                    PdhCloseQuery(_hPdhQuery);
                }
                catch { }
                _hPdhQuery = IntPtr.Zero;
                _hPdhCounter = IntPtr.Zero;
                _pdhInitialized = false;
            }
        }
    }
}
