using System;
using System.Runtime.InteropServices;
using System.Windows;

namespace DockBar.Services;

public sealed class AudioService : IDisposable
{
    private static readonly Lazy<AudioService> _instance = new(() => new AudioService());
    public static AudioService Instance => _instance.Value;

    private static readonly Guid IID_IAudioEndpointVolume = new("5CDF2C82-841E-4546-9722-0CF74078229A");
    private static readonly Guid EventContext = Guid.NewGuid();

    private readonly object _syncLock = new();
    private IMMDeviceEnumerator? _enumerator;
    private NotificationClient? _notificationClient;
    private IAudioEndpointVolume? _endpointVolume;
    private AudioEndpointVolumeCallback? _callback;
    private bool _disposed;

    private float _cachedVolume = 0.5f;
    private bool _cachedMuted;
    private string _deviceName = string.Empty;

    public event EventHandler? VolumeChanged;

    public string DeviceName
    {
        get
        {
            lock (_syncLock)
            {
                return _deviceName;
            }
        }
    }

    public float Volume
    {
        get => _cachedVolume;
        set => SetVolume(value);
    }

    public int VolumePercent => (int)Math.Round(Math.Clamp(_cachedVolume, 0f, 1f) * 100f);

    public bool IsMuted
    {
        get => _cachedMuted;
        set => SetMute(value);
    }

    public AudioService()
    {
        Initialize();
    }

    private void Initialize()
    {
        lock (_syncLock)
        {
            try
            {
                if (_enumerator == null)
                {
                    _enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
                    _notificationClient = new NotificationClient(() =>
                    {
                        // Notificación en hilo COM de Windows al cambiar dispositivo predeterminado o desconectar/conectar
                        Reinitialize();
                    });
                    _enumerator.RegisterEndpointNotificationCallback(_notificationClient);
                }

                InitializeEndpointLocked();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AudioService] Initialize failed: {ex.Message}");
            }
        }
    }

    private void InitializeEndpointLocked()
    {
        CleanupEndpointLocked();

        if (_enumerator == null) return;

        const int eRender = 0;
        const int eConsole = 0;
        const int eMultimedia = 1;
        const int CLSCTX_INPROC_SERVER = 1;

        IMMDevice? device = null;
        // Prioridad 1: eConsole (salida de audio predeterminada seleccionada en Windows)
        int hr = _enumerator.GetDefaultAudioEndpoint(eRender, eConsole, out device);
        if (hr != 0 || device == null)
        {
            // Fallback: eMultimedia
            _enumerator.GetDefaultAudioEndpoint(eRender, eMultimedia, out device);
        }

        if (device != null)
        {
            _deviceName = GetDeviceFriendlyName(device);

            var iid = IID_IAudioEndpointVolume;
            if (device.Activate(ref iid, CLSCTX_INPROC_SERVER, IntPtr.Zero, out var iface) == 0 && iface is IAudioEndpointVolume volume)
            {
                _endpointVolume = volume;

                _callback = new AudioEndpointVolumeCallback((vol, muted) =>
                {
                    _cachedVolume = vol;
                    _cachedMuted = muted;
                    RaiseVolumeChanged();
                });

                _endpointVolume.RegisterControlChangeNotify(_callback);

                if (_endpointVolume.GetMasterVolumeLevelScalar(out var currentVol) == 0)
                {
                    _cachedVolume = currentVol;
                }

                if (_endpointVolume.GetMute(out var currentMute) == 0)
                {
                    _cachedMuted = currentMute;
                }
            }
        }
    }

    public void Reinitialize()
    {
        lock (_syncLock)
        {
            try
            {
                InitializeEndpointLocked();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AudioService] Reinitialize failed: {ex.Message}");
            }
        }
        RaiseVolumeChanged();
    }

    public void SetVolume(float volume)
    {
        volume = Math.Clamp(volume, 0f, 1f);
        _cachedVolume = volume;

        lock (_syncLock)
        {
            if (_endpointVolume == null)
            {
                InitializeEndpointLocked();
            }

            if (_endpointVolume != null)
            {
                try
                {
                    var ctx = EventContext;
                    _endpointVolume.SetMasterVolumeLevelScalar(volume, ref ctx);
                }
                catch
                {
                    InitializeEndpointLocked();
                    try
                    {
                        var ctx = EventContext;
                        _endpointVolume?.SetMasterVolumeLevelScalar(volume, ref ctx);
                    }
                    catch { }
                }
            }
        }

        RaiseVolumeChanged();
    }

    public void ChangeVolumeRelative(float delta)
    {
        SetVolume(_cachedVolume + delta);
    }

    public void ToggleMute()
    {
        SetMute(!_cachedMuted);
    }

    public void SetMute(bool mute)
    {
        _cachedMuted = mute;

        lock (_syncLock)
        {
            if (_endpointVolume == null)
            {
                InitializeEndpointLocked();
            }

            if (_endpointVolume != null)
            {
                try
                {
                    var ctx = EventContext;
                    _endpointVolume.SetMute(mute, ref ctx);
                }
                catch
                {
                    InitializeEndpointLocked();
                    try
                    {
                        var ctx = EventContext;
                        _endpointVolume?.SetMute(mute, ref ctx);
                    }
                    catch { }
                }
            }
        }

        RaiseVolumeChanged();
    }

    private void RaiseVolumeChanged()
    {
        var app = System.Windows.Application.Current;
        if (app != null && !app.Dispatcher.CheckAccess())
        {
            app.Dispatcher.BeginInvoke(new Action(() => VolumeChanged?.Invoke(this, EventArgs.Empty)));
        }
        else
        {
            VolumeChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void CleanupEndpointLocked()
    {
        try
        {
            if (_endpointVolume != null && _callback != null)
            {
                _endpointVolume.UnregisterControlChangeNotify(_callback);
            }
        }
        catch { }

        _endpointVolume = null;
        _callback = null;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            lock (_syncLock)
            {
                CleanupEndpointLocked();

                if (_enumerator != null && _notificationClient != null)
                {
                    try
                    {
                        _enumerator.UnregisterEndpointNotificationCallback(_notificationClient);
                    }
                    catch { }
                    _notificationClient = null;
                    _enumerator = null;
                }
            }
        }
    }

    private static string GetDeviceFriendlyName(IMMDevice dev)
    {
        try
        {
            if (dev.OpenPropertyStore(0 /* STGM_READ */, out var storeObj) == 0 && storeObj is IPropertyStore store)
            {
                var key = PKEY_Device_FriendlyName;
                if (store.GetValue(ref key, out var pv) == 0)
                {
                    try
                    {
                        if (pv.vt == 31 /* VT_LPWSTR */ && pv.pwszVal != IntPtr.Zero)
                        {
                            return Marshal.PtrToStringUni(pv.pwszVal) ?? string.Empty;
                        }
                    }
                    finally
                    {
                        PropVariantClear(ref pv);
                    }
                }
            }
        }
        catch { }
        return string.Empty;
    }

    #region COM Declarations

    [ComImport]
    [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class MMDeviceEnumeratorComObject { }

    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(int dataFlow, int dwStateMask, out IntPtr ppDevices);
        [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice ppEndpoint);
        [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string pwstrId, out IMMDevice ppDevice);
        [PreserveSig] int RegisterEndpointNotificationCallback(IMMNotificationClient pClient);
        [PreserveSig] int UnregisterEndpointNotificationCallback(IMMNotificationClient pClient);
    }

    [Guid("7991EEC9-7E89-4D85-83EB-DE61AC464601"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMNotificationClient
    {
        [PreserveSig] int OnDeviceStateChanged([MarshalAs(UnmanagedType.LPWStr)] string pwstrDeviceId, int dwNewState);
        [PreserveSig] int OnDeviceAdded([MarshalAs(UnmanagedType.LPWStr)] string pwstrDeviceId);
        [PreserveSig] int OnDeviceRemoved([MarshalAs(UnmanagedType.LPWStr)] string pwstrDeviceId);
        [PreserveSig] int OnDefaultDeviceChanged(int flow, int role, [MarshalAs(UnmanagedType.LPWStr)] string pwstrDefaultDeviceId);
        [PreserveSig] int OnPropertyValueChanged([MarshalAs(UnmanagedType.LPWStr)] string pwstrDeviceId, IntPtr key);
    }

    private sealed class NotificationClient : IMMNotificationClient
    {
        private readonly Action _onDeviceChanged;

        public NotificationClient(Action onDeviceChanged)
        {
            _onDeviceChanged = onDeviceChanged;
        }

        public int OnDeviceStateChanged(string pwstrDeviceId, int dwNewState)
        {
            _onDeviceChanged();
            return 0;
        }

        public int OnDeviceAdded(string pwstrDeviceId)
        {
            _onDeviceChanged();
            return 0;
        }

        public int OnDeviceRemoved(string pwstrDeviceId)
        {
            _onDeviceChanged();
            return 0;
        }

        public int OnDefaultDeviceChanged(int flow, int role, string pwstrDefaultDeviceId)
        {
            // flow 0 = eRender, role 0 = eConsole, role 1 = eMultimedia
            if (flow == 0 && (role == 0 || role == 1))
            {
                _onDeviceChanged();
            }
            return 0;
        }

        public int OnPropertyValueChanged(string pwstrDeviceId, IntPtr key) => 0;
    }

    [Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid iid, int dwClsCtx, IntPtr pActivationParams, [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);
        [PreserveSig] int OpenPropertyStore(int stgmAccess, [MarshalAs(UnmanagedType.IUnknown)] out object ppProperties);
        [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string ppstrId);
        [PreserveSig] int GetState(out int pdwState);
    }

    [Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        [PreserveSig] int GetCount(out uint cProps);
        [PreserveSig] int GetAt(uint iProp, out PROPERTYKEY pkey);
        [PreserveSig] int GetValue(ref PROPERTYKEY key, out PROPVARIANT pv);
        [PreserveSig] int SetValue(ref PROPERTYKEY key, ref PROPVARIANT pv);
        [PreserveSig] int Commit();
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct PROPERTYKEY
    {
        public Guid fmtid;
        public uint pid;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct PROPVARIANT
    {
        [FieldOffset(0)] public ushort vt;
        [FieldOffset(8)] public IntPtr pwszVal;
    }

    private static readonly PROPERTYKEY PKEY_Device_FriendlyName = new()
    {
        fmtid = new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"),
        pid = 14
    };

    [DllImport("ole32.dll")]
    private static extern int PropVariantClear(ref PROPVARIANT pvar);

    [Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolume
    {
        [PreserveSig] int RegisterControlChangeNotify(IAudioEndpointVolumeCallback pNotify);
        [PreserveSig] int UnregisterControlChangeNotify(IAudioEndpointVolumeCallback pNotify);
        [PreserveSig] int GetChannelCount(out uint pnChannelCount);
        [PreserveSig] int SetMasterVolumeLevel(float fLevelDB, ref Guid pguidEventContext);
        [PreserveSig] int SetMasterVolumeLevelScalar(float fLevel, ref Guid pguidEventContext);
        [PreserveSig] int GetMasterVolumeLevel(out float pfLevelDB);
        [PreserveSig] int GetMasterVolumeLevelScalar(out float pfLevel);
        [PreserveSig] int SetChannelVolumeLevel(uint nChannel, float fLevelDB, ref Guid pguidEventContext);
        [PreserveSig] int SetChannelVolumeLevelScalar(uint nChannel, float fLevel, ref Guid pguidEventContext);
        [PreserveSig] int GetChannelVolumeLevel(uint nChannel, out float pfLevelDB);
        [PreserveSig] int GetChannelVolumeLevelScalar(uint nChannel, out float pfLevel);
        [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool bMute, ref Guid pguidEventContext);
        [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool pbMute);
        [PreserveSig] int GetVolumeStepInfo(out uint pnStep, out uint pnStepCount);
        [PreserveSig] int VolumeStepUp(ref Guid pguidEventContext);
        [PreserveSig] int VolumeStepDown(ref Guid pguidEventContext);
        [PreserveSig] int QueryHardwareSupport(out uint pdwHardwareSupportMask);
        [PreserveSig] int GetVolumeRange(out float pflVolumeMindB, out float pflVolumeMaxdB, out float pflVolumeIncrementdB);
    }

    [Guid("657804FA-D6AD-4496-8A60-352752AF4F89"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolumeCallback
    {
        [PreserveSig] int OnNotify(IntPtr pNotify);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AUDIO_VOLUME_NOTIFICATION_DATA
    {
        public Guid guidEventContext;
        [MarshalAs(UnmanagedType.Bool)]
        public bool bMuted;
        public float fMasterVolume;
        public uint nChannels;
        public float fChannelVolume;
    }

    private sealed class AudioEndpointVolumeCallback : IAudioEndpointVolumeCallback
    {
        private readonly Action<float, bool> _onChanged;

        public AudioEndpointVolumeCallback(Action<float, bool> onChanged)
        {
            _onChanged = onChanged;
        }

        public int OnNotify(IntPtr pNotify)
        {
            if (pNotify == IntPtr.Zero) return 0;
            try
            {
                var data = Marshal.PtrToStructure<AUDIO_VOLUME_NOTIFICATION_DATA>(pNotify);
                if (data.guidEventContext == EventContext)
                {
                    // Cambio originado por DockBar: ignorar para evitar feedback en el slider
                    return 0;
                }
                _onChanged(data.fMasterVolume, data.bMuted);
            }
            catch { }
            return 0;
        }
    }

    #endregion
}
