using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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
    public ImageSource? Thumbnail { get; private set; }
    public bool HasThumbnail => Thumbnail != null;
    public bool IsVideo { get; private set; }

    public TimeSpan Position { get; private set; } = TimeSpan.Zero;
    public TimeSpan Duration { get; private set; } = TimeSpan.Zero;
    public double PositionSeconds => Position.TotalSeconds;
    public double DurationSeconds => Duration.TotalSeconds;
    public string PositionText => FormatTime(Position);
    public string DurationText => Duration > TimeSpan.Zero && Duration < TimeSpan.FromHours(24) && Duration != TimeSpan.MaxValue ? FormatTime(Duration) : "--:--";
    public bool CanSeek => _canSeek && Duration > TimeSpan.Zero && Duration < TimeSpan.FromHours(24) && Duration != TimeSpan.MaxValue;

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

    private bool _pollingEnabled = true;

    public void SetPollingEnabled(bool enabled)
    {
        _pollingEnabled = enabled;
        var app = System.Windows.Application.Current;
        if (app == null) return;

        if (!app.Dispatcher.CheckAccess())
        {
            app.Dispatcher.BeginInvoke(new Action(() => SetPollingEnabled(enabled)));
            return;
        }

        if (enabled)
        {
            if (!_pollTimer.IsEnabled) _pollTimer.Start();
        }
        else
        {
            if (_pollTimer.IsEnabled) _pollTimer.Stop();
        }
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

        if (_pollingEnabled)
        {
            _pollTimer.Start();
        }
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
            GlobalSystemMediaTransportControlsSession? session = null;

            if (sessions != null)
            {
                // 1. Buscamos primero alguna sesión en reproducción activa que NO sea una preview
                foreach (var s in sessions)
                {
                    if (s.GetPlaybackInfo()?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
                    {
                        if (!await IsPreviewSessionAsync(s))
                        {
                            session = s;
                            break;
                        }
                    }
                }

                // 2. Si no hay sesión activa (o todas eran previews), probamos GetCurrentSession() si no es preview
                if (session == null)
                {
                    var current = _manager.GetCurrentSession();
                    if (current != null && !await IsPreviewSessionAsync(current))
                    {
                        session = current;
                    }
                }

                // 3. Fallback: cualquier otra sesión existente que no sea preview
                if (session == null)
                {
                    foreach (var s in sessions)
                    {
                        if (!await IsPreviewSessionAsync(s))
                        {
                            session = s;
                            break;
                        }
                    }
                }
            }

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

    private static bool IsBrowserApp(string? appId)
    {
        if (string.IsNullOrWhiteSpace(appId)) return false;
        string lower = appId.ToLowerInvariant();
        return lower.Contains("chrome") ||
               lower.Contains("edge") ||
               lower.Contains("firefox") ||
               lower.Contains("brave") ||
               lower.Contains("opera") ||
               lower.Contains("vivaldi") ||
               lower.Contains("arc") ||
               lower.Contains("zen") ||
               lower.Contains("waterfox") ||
               lower.Contains("yandex");
    }

    private async Task<bool> IsPreviewSessionAsync(GlobalSystemMediaTransportControlsSession? session)
    {
        if (session == null) return false;

        try
        {
            string appId = session.SourceAppUserModelId ?? "";
            if (!IsBrowserApp(appId)) return false;

            var timeline = session.GetTimelineProperties();
            bool isInfiniteDuration = timeline != null &&
                (timeline.EndTime >= TimeSpan.FromHours(24) || timeline.EndTime == TimeSpan.MaxValue);
            bool isZeroDuration = timeline == null || timeline.EndTime <= TimeSpan.Zero;

            // Si tiene duración finita y válida (> 0 y < 24h), no es una preview con duración infinita
            if (!isInfiniteDuration && !isZeroDuration)
            {
                return false;
            }

            var props = await session.TryGetMediaPropertiesAsync();
            if (props != null)
            {
                // Si tiene artista (como el canal de YouTube en un video o stream real), es legítimo
                if (!string.IsNullOrWhiteSpace(props.Artist))
                {
                    return false;
                }

                // En navegador, sin artista y con duración infinita o nula => Es una preview de video
                return true;
            }

            return isInfiniteDuration;
        }
        catch
        {
            return false;
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
        ImageSource? newThumbnail = null;
        bool newIsVideo = false;

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

                // Detección de previews de video (por ej. hover en YouTube en navegadores).
                // Se caracterizan por: ser de un navegador, no tener artista (el canal solo se asigna al abrir el video)
                // y tener duración infinita (TimeSpan.MaxValue / >= 24h) o no especificada.
                bool isBrowser = IsBrowserApp(newAppId);
                bool isInfiniteDuration = (timeline != null && (timeline.EndTime >= TimeSpan.FromHours(24) || timeline.EndTime == TimeSpan.MaxValue)) ||
                                          (_endTime >= TimeSpan.FromHours(24) || _endTime == TimeSpan.MaxValue);
                bool isZeroDuration = (timeline == null || timeline.EndTime <= TimeSpan.Zero) && (_endTime <= TimeSpan.Zero || _endTime >= TimeSpan.FromHours(24));
                bool isPreview = isBrowser && string.IsNullOrWhiteSpace(newArtist) && (isInfiniteDuration || isZeroDuration);

                if (isPreview)
                {
                    newTitle = "";
                    newArtist = "";
                    newIsPlaying = false;
                    newThumbnail = null;
                    newIsVideo = false;
                    _basePosition = TimeSpan.Zero;
                    _endTime = TimeSpan.Zero;
                    Position = TimeSpan.Zero;
                    Duration = TimeSpan.Zero;
                    _canSeek = false;

                    // Desconectamos para no quedar anclados al preview y buscamos otra sesión válida si existe
                    DetachSessionEvents();
                    _currentSession = null;
                    _ = RefreshCurrentSessionAsync(false);
                }
                else
                {
                    if (trackChanged || Thumbnail == null)
                    {
                        var (thumb, isVid) = await LoadThumbnailAsync(mediaProperties);
                        newThumbnail = thumb;
                        newIsVideo = isVid;
                    }
                    else
                    {
                        newThumbnail = Thumbnail;
                        newIsVideo = IsVideo;
                    }

                    if (timeline != null)
                {
                    bool recentlySeeked = (DateTimeOffset.UtcNow - _lastSeekTime).TotalSeconds < 2.5;

                    // Preserve existing duration if GSMTC temporarily reports zero during buffering/ads,
                    // pero filtramos duraciones infinitas o anómalas
                    if (timeline.EndTime > TimeSpan.Zero && timeline.EndTime < TimeSpan.FromHours(24) && timeline.EndTime != TimeSpan.MaxValue)
                    {
                        _endTime = timeline.EndTime;
                        Duration = _endTime;
                    }
                    else if (timeline.MaxSeekTime > TimeSpan.Zero && timeline.MaxSeekTime < TimeSpan.FromHours(24) && timeline.MaxSeekTime != TimeSpan.MaxValue)
                    {
                        _endTime = timeline.MaxSeekTime;
                        Duration = _endTime;
                    }
                    else if (_endTime > TimeSpan.Zero && _endTime < TimeSpan.FromHours(24) && _endTime != TimeSpan.MaxValue)
                    {
                        Duration = _endTime;
                    }
                    else
                    {
                        _endTime = TimeSpan.Zero;
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

                    _canSeek = Duration > TimeSpan.FromSeconds(1) && Duration < TimeSpan.FromHours(24);
                }
                else
                {
                    if (_endTime > TimeSpan.Zero && _endTime < TimeSpan.FromHours(24) && _endTime != TimeSpan.MaxValue)
                    {
                        Duration = _endTime;
                    }
                    else
                    {
                        _endTime = TimeSpan.Zero;
                        Duration = TimeSpan.Zero;
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

                    _canSeek = Duration > TimeSpan.FromSeconds(1) && Duration < TimeSpan.FromHours(24);
                }
                }

                // Si cambió de pista o está reproduciendo pero aún no se tiene la duración (común en YouTube/Spotify al inicio),
                // programamos reintentos rápidos para obtener la duración tan pronto esté lista
                if (!isPreview && newIsPlaying && Duration == TimeSpan.Zero)
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
                            SourceAppId != newAppId ||
                            !ReferenceEquals(Thumbnail, newThumbnail) ||
                            IsVideo != newIsVideo;

        Title = newTitle;
        Artist = newArtist;
        FullTrackText = fullText;
        IsPlaying = newIsPlaying;
        HasMedia = hasMedia;
        SourceAppId = newAppId;
        Thumbnail = newThumbnail;
        IsVideo = newIsVideo;

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
            if (_disposed || !IsPlaying || (Duration > TimeSpan.Zero && Duration < TimeSpan.FromHours(24))) return;

            try
            {
                if (_currentSession != null)
                {
                    var tl = _currentSession.GetTimelineProperties();
                    if (tl != null && tl.EndTime > TimeSpan.Zero && tl.EndTime < TimeSpan.FromHours(24) && tl.EndTime != TimeSpan.MaxValue)
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
        if (_currentSession == null || seconds < 0 || !_canSeek || Duration <= TimeSpan.Zero) return false;

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
        if (time >= TimeSpan.FromHours(24) || time == TimeSpan.MaxValue)
        {
            return "--:--";
        }
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

    private static async Task<(ImageSource? Image, bool IsVideo)> LoadThumbnailAsync(GlobalSystemMediaTransportControlsSessionMediaProperties? props)
    {
        if (props?.Thumbnail == null) return (null, false);

        try
        {
            using var ras = await props.Thumbnail.OpenReadAsync();
            if (ras == null || ras.Size == 0) return (null, false);

            using var stream = System.IO.WindowsRuntimeStreamExtensions.AsStreamForRead(ras);
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            ms.Position = 0;

            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.StreamSource = ms;
            bmp.EndInit();
            bmp.Freeze();

            bool isVideo = props.PlaybackType == Windows.Media.MediaPlaybackType.Video ||
                           (bmp.PixelWidth > 0 && bmp.PixelHeight > 0 && (double)bmp.PixelWidth / bmp.PixelHeight > 1.25);

            return (bmp, isVideo);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MediaService] Failed to load thumbnail: {ex.Message}");
            return (null, false);
        }
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
