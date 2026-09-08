using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Windows.Media.Control;

namespace DockBar.Services;

public sealed class MediaService : IDisposable
{
    private static readonly Lazy<MediaService> _instance = new(() => new MediaService());
    public static MediaService Instance => _instance.Value;

    private const byte VK_MEDIA_NEXT_TRACK = 0xB0;
    private const byte VK_MEDIA_PREV_TRACK = 0xB1;
    private const byte VK_MEDIA_STOP = 0xB2;
    private const byte VK_MEDIA_PLAY_PAUSE = 0xB3;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private GlobalSystemMediaTransportControlsSession? _currentSession;
    private readonly DispatcherTimer _pollTimer;
    private bool _disposed;

    public event EventHandler? MediaStateChanged;

    public string Title { get; private set; } = "";
    public string Artist { get; private set; } = "";
    public string FullTrackText { get; private set; } = "";
    public bool IsPlaying { get; private set; }
    public bool HasMedia { get; private set; }
    public string SourceAppId { get; private set; } = "";

    public MediaService()
    {
        _pollTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2.5)
        };
        _pollTimer.Tick += async (_, _) => await RefreshCurrentSessionAsync(false);

        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        try
        {
            _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            if (_manager != null)
            {
                _manager.CurrentSessionChanged += Manager_CurrentSessionChanged;
                await RefreshCurrentSessionAsync(true);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MediaService] WinRT SMTC Init failed: {ex.Message}");
        }

        _pollTimer.Start();
    }

    private async void Manager_CurrentSessionChanged(GlobalSystemMediaTransportControlsSessionManager sender, CurrentSessionChangedEventArgs args)
    {
        await RefreshCurrentSessionAsync(true);
    }

    public async Task RefreshCurrentSessionAsync(bool force)
    {
        if (_manager == null)
        {
            try
            {
                _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
                if (_manager != null)
                {
                    _manager.CurrentSessionChanged += Manager_CurrentSessionChanged;
                }
            }
            catch
            {
                return;
            }
        }

        if (_manager == null) return;

        try
        {
            var session = _manager.GetCurrentSession();

            // Si GetCurrentSession es nulo pero hay sesiones activas (ej. Spotify pausado o Chrome en segundo plano),
            // seleccionamos la primera sesión disponible
            if (session == null)
            {
                var sessions = _manager.GetSessions();
                session = sessions?.FirstOrDefault(s => s.GetPlaybackInfo()?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
                          ?? sessions?.FirstOrDefault();
            }

            if (force || !ReferenceEquals(session, _currentSession))
            {
                DetachSessionEvents();
                _currentSession = session;
                AttachSessionEvents();
            }

            await UpdatePropertiesFromCurrentSessionAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MediaService] Refresh error: {ex.Message}");
        }
    }

    private void AttachSessionEvents()
    {
        if (_currentSession == null) return;

        try
        {
            _currentSession.MediaPropertiesChanged += CurrentSession_MediaPropertiesChanged;
            _currentSession.PlaybackInfoChanged += CurrentSession_PlaybackInfoChanged;
        }
        catch { }
    }

    private void DetachSessionEvents()
    {
        if (_currentSession == null) return;

        try
        {
            _currentSession.MediaPropertiesChanged -= CurrentSession_MediaPropertiesChanged;
            _currentSession.PlaybackInfoChanged -= CurrentSession_PlaybackInfoChanged;
        }
        catch { }
    }

    private async void CurrentSession_PlaybackInfoChanged(GlobalSystemMediaTransportControlsSession sender, PlaybackInfoChangedEventArgs args)
    {
        await UpdatePropertiesFromCurrentSessionAsync();
    }

    private async void CurrentSession_MediaPropertiesChanged(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs args)
    {
        await UpdatePropertiesFromCurrentSessionAsync();
    }

    private async Task UpdatePropertiesFromCurrentSessionAsync()
    {
        string newTitle = "";
        string newArtist = "";
        bool newIsPlaying = false;
        string newAppId = "";

        if (_currentSession != null)
        {
            try
            {
                newAppId = _currentSession.SourceAppUserModelId ?? "";
                var playbackInfo = _currentSession.GetPlaybackInfo();
                if (playbackInfo != null)
                {
                    newIsPlaying = playbackInfo.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
                }

                var mediaProperties = await _currentSession.TryGetMediaPropertiesAsync();
                if (mediaProperties != null)
                {
                    newTitle = mediaProperties.Title?.Trim() ?? "";
                    newArtist = mediaProperties.Artist?.Trim() ?? "";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MediaService] Get properties failed: {ex.Message}");
            }
        }

        string fullText;
        if (!string.IsNullOrWhiteSpace(newTitle) && !string.IsNullOrWhiteSpace(newArtist))
        {
            fullText = $"{newTitle} • {newArtist}";
        }
        else if (!string.IsNullOrWhiteSpace(newTitle))
        {
            fullText = newTitle;
        }
        else if (!string.IsNullOrWhiteSpace(newArtist))
        {
            fullText = newArtist;
        }
        else
        {
            fullText = "";
        }

        bool hasMedia = !string.IsNullOrWhiteSpace(fullText);

        if (Title != newTitle ||
            Artist != newArtist ||
            FullTrackText != fullText ||
            IsPlaying != newIsPlaying ||
            HasMedia != hasMedia ||
            SourceAppId != newAppId)
        {
            Title = newTitle;
            Artist = newArtist;
            FullTrackText = fullText;
            IsPlaying = newIsPlaying;
            HasMedia = hasMedia;
            SourceAppId = newAppId;

            RaiseMediaStateChanged();
        }
    }

    public async Task TogglePlayPauseAsync()
    {
        if (_currentSession != null)
        {
            try
            {
                if (await _currentSession.TryTogglePlayPauseAsync())
                {
                    await Task.Delay(100);
                    await UpdatePropertiesFromCurrentSessionAsync();
                    return;
                }
            }
            catch { }
        }

        // Respaldo por teclas multimedia virtuales
        SendMediaKey(VK_MEDIA_PLAY_PAUSE);
        await Task.Delay(150);
        await RefreshCurrentSessionAsync(true);
    }

    public async Task SkipNextAsync()
    {
        if (_currentSession != null)
        {
            try
            {
                if (await _currentSession.TrySkipNextAsync())
                {
                    await Task.Delay(150);
                    await UpdatePropertiesFromCurrentSessionAsync();
                    return;
                }
            }
            catch { }
        }

        SendMediaKey(VK_MEDIA_NEXT_TRACK);
        await Task.Delay(150);
        await RefreshCurrentSessionAsync(true);
    }

    public async Task SkipPreviousAsync()
    {
        if (_currentSession != null)
        {
            try
            {
                if (await _currentSession.TrySkipPreviousAsync())
                {
                    await Task.Delay(150);
                    await UpdatePropertiesFromCurrentSessionAsync();
                    return;
                }
            }
            catch { }
        }

        SendMediaKey(VK_MEDIA_PREV_TRACK);
        await Task.Delay(150);
        await RefreshCurrentSessionAsync(true);
    }

    private static void SendMediaKey(byte vkCode)
    {
        try
        {
            keybd_event(vkCode, 0, 0, UIntPtr.Zero);
            keybd_event(vkCode, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }
        catch { }
    }

    private void RaiseMediaStateChanged()
    {
        var app = System.Windows.Application.Current;
        if (app != null && !app.Dispatcher.CheckAccess())
        {
            app.Dispatcher.BeginInvoke(new Action(() => MediaStateChanged?.Invoke(this, EventArgs.Empty)));
        }
        else
        {
            MediaStateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _pollTimer.Stop();
            DetachSessionEvents();
            if (_manager != null)
            {
                _manager.CurrentSessionChanged -= Manager_CurrentSessionChanged;
                _manager = null;
            }
        }
    }
}
