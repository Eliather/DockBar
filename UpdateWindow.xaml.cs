using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using DockBar.Services;

namespace DockBar;

public partial class UpdateWindow : Window, INotifyPropertyChanged
{
    private readonly UpdateInfo _updateInfo;
    private readonly Version _currentVersion;
    private bool _isDownloading;
    private double _downloadProgress;
    private bool _isIndeterminate;
    private string _statusText = string.Empty;

    public string CurrentVersionText => $"v{_currentVersion}";
    public string LatestVersionText => !string.IsNullOrWhiteSpace(_updateInfo.Tag)
        ? (_updateInfo.Tag.StartsWith("v", StringComparison.OrdinalIgnoreCase) ? _updateInfo.Tag : $"v{_updateInfo.Tag}")
        : $"v{_updateInfo.Version}";

    public string ChangelogText => !string.IsNullOrWhiteSpace(_updateInfo.Changelog)
        ? _updateInfo.Changelog
        : LocalizationService.Get("Update_NoChangelog");

    public bool HasReleaseUrl => !string.IsNullOrWhiteSpace(_updateInfo.HtmlUrl);

    public bool IsDownloading
    {
        get => _isDownloading;
        private set
        {
            if (_isDownloading != value)
            {
                _isDownloading = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanInstall));
                OnPropertyChanged(nameof(InstallButtonText));
                OnPropertyChanged(nameof(HasProgressOrStatus));
            }
        }
    }

    public double DownloadProgress
    {
        get => _downloadProgress;
        private set
        {
            if (Math.Abs(_downloadProgress - value) > 0.01)
            {
                _downloadProgress = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsIndeterminate
    {
        get => _isIndeterminate;
        private set
        {
            if (_isIndeterminate != value)
            {
                _isIndeterminate = value;
                OnPropertyChanged();
            }
        }
    }

    public string StatusText
    {
        get => _statusText;
        private set
        {
            if (_statusText != value)
            {
                _statusText = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasProgressOrStatus));
            }
        }
    }

    public bool HasProgressOrStatus => IsDownloading || !string.IsNullOrWhiteSpace(StatusText);

    public bool CanInstall => !IsDownloading && !string.IsNullOrWhiteSpace(_updateInfo.InstallerUrl);

    public string InstallButtonText => IsDownloading
        ? LocalizationService.Get("Update_StatusDownloading")
        : LocalizationService.Get("Update_InstallNow");

    public event PropertyChangedEventHandler? PropertyChanged;

    public UpdateWindow(UpdateInfo updateInfo, Version currentVersion)
    {
        _updateInfo = updateInfo;
        _currentVersion = currentVersion;
        InitializeComponent();
        DataContext = this;
        SourceInitialized += (_, _) => WindowSwitcherHelper.HideFromWindowSwitchers(this);

        StatusText = LocalizationService.Get("Update_StatusReady");
    }

    private async void Install_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_updateInfo.InstallerUrl))
        {
            StatusText = string.Format(LocalizationService.Get("Update_NoInstaller"), _updateInfo.Tag);
            return;
        }

        IsDownloading = true;
        IsIndeterminate = false;
        DownloadProgress = 0;
        StatusText = LocalizationService.Get("Update_StatusDownloading");

        var tempPath = Path.Combine(Path.GetTempPath(), $"DockBarSetup-{_updateInfo.Version}.exe");

        var progress = new Progress<double>(percent =>
        {
            DownloadProgress = percent;
            StatusText = $"{LocalizationService.Get("Update_StatusDownloading")} ({percent:F0}%)";
        });

        try
        {
            var downloaded = await UpdateService.DownloadFileAsync(
                _updateInfo.InstallerUrl,
                tempPath,
                CancellationToken.None,
                progress);

            if (!downloaded || !File.Exists(tempPath))
            {
                IsDownloading = false;
                StatusText = LocalizationService.Get("Update_DownloadFailed");
                return;
            }

            StatusText = LocalizationService.Get("Update_StatusDownloaded");
            await Task.Delay(400);

            Process.Start(new ProcessStartInfo
            {
                FileName = tempPath,
                UseShellExecute = true
            });

            System.Windows.Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            IsDownloading = false;
            StatusText = LocalizationService.Get("Update_DownloadFailed");
        }
    }

    private void OpenGitHub_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_updateInfo.HtmlUrl))
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = _updateInfo.HtmlUrl,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        if (!IsDownloading)
        {
            Close();
        }
    }

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
