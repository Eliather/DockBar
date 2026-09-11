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
    private readonly DispatcherTimer _timelineTimer;
    private bool _disposed;

    public event EventHandler? MediaStateChanged;
    public event EventHandler? TimelineChanged;

    public string Title { get; private set; } = "";
    public string Artist { get; private set; } = "";
    public string FullTrackText { get; private set; } = "";
    public bool IsPlaying { get; private set; }
    public bool HasMedia { get; private set; }
    public string SourceAppId { get; private set; } = "";

    public TimeSpan Position { get; private set; } = TimeSpan.Zero;
    public TimeSpan Duration { get; private set; } = TimeSpan.Zero;
    public double PositionSeconds => Position.TotalSeconds;
    public double DurationSeconds => Duration.TotalSeconds;
    public string PositionText => FormatTime(Position);
    public string DurationText => Duration > TimeSpan.Zero ? FormatTime(Duration) : "--:--";
    public bool CanSeek => _canSeek;

    private TimeSpan _basePosition = TimeSpan.Zero;
    private TimeSpan _endTime = TimeSpan.Zero;
    private DateTimeOffset _lastTimelineUpdated;
    private DateTimeOffset _lastSeekTime = DateTimeOffset.MinValue;
    private bool _canSeek;

    public MediaService()
    {
        _pollTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2.5)
        };
        _pollTimer.Tick += async (_, _) => await RefreshCurrentSessionAsync(false);

        _timelineTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _timelineTimer.Tick += (_, _) => UpdateLivePosition();

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
            var sessions = _manager.GetSessions();
            // Si hay alguna sesión reproduciendo activamente, le damos prioridad sobre la última seleccionada por Windows
            var playingSession = sessions?.FirstOrDefault(s => s.GetPlaybackInfo()?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing);
            var session = playingSession ?? _manager.GetCurrentSession() ?? sessions?.FirstOrDefault();

            bool isDifferentSession = _currentSession == null || session == null ||
                                      !ReferenceEquals(_currentSession, session) ||
                                      _currentSession.SourceAppUserModelId != session.SourceAppUserModelId;
            if (force || isDifferentSession)
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
            _currentSession.TimelinePropertiesChanged += CurrentSession_TimelinePropertiesChanged;
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
            _currentSession.TimelinePropertiesChanged -= CurrentSession_TimelinePropertiesChanged;
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

    private async void CurrentSession_TimelinePropertiesChanged(GlobalSystemMediaTransportControlsSession sender, TimelinePropertiesChangedEventArgs args)
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

                // If the track actually changed, reset cached timeline
                bool trackChanged = (!string.IsNullOrEmpty(Title) && !string.IsNullOrEmpty(newTitle) && !Title.Equals(newTitle, StringComparison.OrdinalIgnoreCase)) ||
                                    (!string.IsNullOrEmpty(Artist) && !string.IsNullOrEmpty(newArtist) && !Artist.Equals(newArtist, StringComparison.OrdinalIgnoreCase));
                if (trackChanged)
                {
                    _basePosition = TimeSpan.Zero;
                    _endTime = TimeSpan.Zero;
                    Position = TimeSpan.Zero;
                    Duration = TimeSpan.Zero;
                    _lastTimelineUpdated = DateTimeOffset.UtcNow;
                }

                var timeline = _currentSession.GetTimelineProperties();
                if (timeline != null)
                {
                    bool recentlySeeked = (DateTimeOffset.UtcNow - _lastSeekTime).TotalSeconds < 2.5;

                    // Preserve existing duration if GSMTC temporarily reports zero during buffering/ads
                    if (timeline.EndTime > TimeSpan.Zero)
                    {
                        _endTime = timeline.EndTime;
                        Duration = _endTime;
                    }
                    else if (timeline.MaxSeekTime > TimeSpan.Zero)
                    {
                        _endTime = timeline.MaxSeekTime;
                        Duration = _endTime;
                    }
                    else if (_endTime > TimeSpan.Zero)
                    {
                        Duration = _endTime;
                    }
                    else
                    {
                        Duration = TimeSpan.Zero;
                    }

                    // Protect against browsers temporarily reporting 0:00 right after a seek
                    if (recentlySeeked && timeline.Position == TimeSpan.Zero && _basePosition > TimeSpan.Zero)
                    {
                        Position = _basePosition;
                    }
                    else
                    {
                        bool posChanged = timeline.Position != _basePosition;
                        _basePosition = timeline.Position;

                        if (timeline.LastUpdatedTime != default && timeline.LastUpdatedTime <= DateTimeOffset.UtcNow)
                        {
                            _lastTimelineUpdated = timeline.LastUpdatedTime;
                        }
                        else if (posChanged || _lastTimelineUpdated == default)
                        {
                            _lastTimelineUpdated = DateTimeOffset.UtcNow;
                        }

                        if (newIsPlaying)
                        {
                            var elapsed = DateTimeOffset.UtcNow - _lastTimelineUpdated;
                            if (elapsed < TimeSpan.Zero) elapsed = TimeSpan.Zero;
                            var current = _basePosition + elapsed;
                            if (Duration > TimeSpan.Zero && current > Duration) current = Duration;
                            Position = current;
                        }
                        else
                        {
                            Position = _basePosition;
                        }
                    }

                    _canSeek = Duration > TimeSpan.FromSeconds(1);
                }
                else
                {
                    if (_endTime > TimeSpan.Zero)
                    {
                        Duration = _endTime;
                    }

                    if (newIsPlaying)
                    {
                        var elapsed = DateTimeOffset.UtcNow - _lastTimelineUpdated;
                        if (elapsed < TimeSpan.Zero) elapsed = TimeSpan.Zero;
                        var current = _basePosition + elapsed;
                        if (Duration > TimeSpan.Zero && current > Duration) current = Duration;
                        Position = current;
                    }
                    else
                    {
                        Position = _basePosition;
                    }
                }

                // Si cambió de pista o está reproduciendo pero aún no se tiene la duración (común en YouTube/Spotify al inicio),
                // programamos reintentos rápidos para obtener la duración tan pronto esté lista
                if (newIsPlaying && Duration == TimeSpan.Zero)
                {
                    _ = ScheduleTimelineRetryAsync();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MediaService] Get properties failed: {ex.Message}");
            }
        }
        else
        {
            _basePosition = TimeSpan.Zero;
            _endTime = TimeSpan.Zero;
            _lastTimelineUpdated = default;
            _canSeek = false;
            Position = TimeSpan.Zero;
            Duration = TimeSpan.Zero;
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

        bool stateChanged = Title != newTitle ||
                            Artist != newArtist ||
                            FullTrackText != fullText ||
                            IsPlaying != newIsPlaying ||
                            HasMedia != hasMedia ||
                            SourceAppId != newAppId;

        Title = newTitle;
        Artist = newArtist;
        FullTrackText = fullText;
        IsPlaying = newIsPlaying;
        HasMedia = hasMedia;
        SourceAppId = newAppId;

        if (IsPlaying && HasMedia)
        {
            SetTimelineTimerEnabled(true);
        }
        else
        {
            SetTimelineTimerEnabled(false);
        }

        if (stateChanged)
        {
            RaiseMediaStateChanged();
        }
        RaiseTimelineChanged();
    }

    private async Task ScheduleTimelineRetryAsync()
    {
        int[] delays = { 300, 600, 1200 };
        foreach (var delay in delays)
        {
            await Task.Delay(delay);
            if (_disposed || !IsPlaying || Duration > TimeSpan.Zero) return;

            try
            {
                if (_currentSession != null)
                {
                    var tl = _currentSession.GetTimelineProperties();
                    if (tl != null && tl.EndTime > TimeSpan.Zero)
                    {
                        await UpdatePropertiesFromCurrentSessionAsync();
                        return;
                    }
                }
            }
            catch { }
        }
    }

    private void SetTimelineTimerEnabled(bool enable)
    {
        var app = System.Windows.Application.Current;
        if (app == null) return;

        if (!app.Dispatcher.CheckAccess())
        {
            app.Dispatcher.BeginInvoke(new Action(() => SetTimelineTimerEnabled(enable)));
            return;
        }

        if (enable)
        {
            if (!_timelineTimer.IsEnabled) _timelineTimer.Start();
        }
        else
        {
            if (_timelineTimer.IsEnabled) _timelineTimer.Stop();
        }
    }

    private void UpdateLivePosition()
    {
        if (!IsPlaying || !HasMedia)
        {
            SetTimelineTimerEnabled(false);
            return;
        }

        bool recentlySeeked = (DateTimeOffset.UtcNow - _lastSeekTime).TotalSeconds < 1.0;
        if (recentlySeeked)
        {
            Position = _basePosition;
            RaiseTimelineChanged();
            return;
        }

        var elapsed = DateTimeOffset.UtcNow - _lastTimelineUpdated;
        if (elapsed < TimeSpan.Zero) elapsed = TimeSpan.Zero;
        var current = _basePosition + elapsed;
        if (Duration > TimeSpan.Zero && current > Duration) current = Duration;

        Position = current;
        RaiseTimelineChanged();
    }

    public async Task<bool> SeekAsync(double seconds)
    {
        if (_currentSession == null || seconds < 0) return false;

        try
        {
            var target = TimeSpan.FromSeconds(seconds);
            if (_endTime > TimeSpan.Zero && target > _endTime)
                target = _endTime;

            _lastSeekTime = DateTimeOffset.UtcNow;
            _basePosition = target;
            _lastTimelineUpdated = DateTimeOffset.UtcNow;
            Position = target;
            RaiseTimelineChanged();

            long ticks = target.Ticks;
            bool success = await _currentSession.TryChangePlaybackPositionAsync(ticks);
            return success;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MediaService] Seek failed: {ex.Message}");
            return false;
        }
    }

    public static string FormatTime(TimeSpan time)
    {
        if (time < TimeSpan.Zero) time = TimeSpan.Zero;
        if (time.TotalHours >= 1)
        {
            return $"{(int)time.TotalHours}:{time.Minutes:D2}:{time.Seconds:D2}";
        }
        return $"{time.Minutes}:{time.Seconds:D2}";
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

    private void RaiseTimelineChanged()
    {
        var app = System.Windows.Application.Current;
        if (app != null && !app.Dispatcher.CheckAccess())
        {
            app.Dispatcher.BeginInvoke(new Action(() => TimelineChanged?.Invoke(this, EventArgs.Empty)));
        }
        else
        {
            TimelineChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _pollTimer.Stop();
            _timelineTimer.Stop();
            DetachSessionEvents();
            if (_manager != null)
            {
                _manager.CurrentSessionChanged -= Manager_CurrentSessionChanged;
                _manager = null;
            }
        }
    }
}
