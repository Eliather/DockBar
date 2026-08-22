using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace DockBar.Services;

public sealed class UpdateInfo
{
    public Version Version { get; }
    public string Tag { get; }
    public string Title { get; }
    public string Changelog { get; }
    public string? InstallerUrl { get; }
    public string? ZipUrl { get; }
    public string? HtmlUrl { get; }

    public UpdateInfo(Version version, string tag, string title, string changelog, string? installerUrl, string? zipUrl, string? htmlUrl)
    {
        Version = version;
        Tag = tag;
        Title = title;
        Changelog = changelog;
        InstallerUrl = installerUrl;
        ZipUrl = zipUrl;
        HtmlUrl = htmlUrl;
    }
}

public static class UpdateService
{
    // Zero-dependency async GitHub release checking with stream deserialization & strict 8s timeout
    private const string LatestReleaseUrl = "https://api.github.com/repos/Eliather/DockBar/releases/latest";
    private static readonly HttpClient Client = CreateClient();

    public static Version GetCurrentVersion()
    {
        var info = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (TryParseVersion(info, out var version))
        {
            return version;
        }

        return Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0, 0);
    }

    public static async Task<UpdateInfo?> GetLatestReleaseAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseUrl);
            using var response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var release = await JsonSerializer.DeserializeAsync<GitHubRelease>(stream, JsonOptions, cancellationToken);
            if (release == null || release.Draft || release.PreRelease)
            {
                return null;
            }

            if (!TryParseVersion(release.TagName, out var latestVersion))
            {
                return null;
            }

            var installerUrl = release.Assets
                ?.FirstOrDefault(a => string.Equals(a.Name, "DockBarSetup.exe", StringComparison.OrdinalIgnoreCase))
                ?.DownloadUrl;
            if (string.IsNullOrWhiteSpace(installerUrl))
            {
                installerUrl = release.Assets
                    ?.FirstOrDefault(a => a.Name?.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) == true)
                    ?.DownloadUrl;
            }

            var zipUrl = release.Assets
                ?.FirstOrDefault(a => a.Name?.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) == true)
                ?.DownloadUrl;

            var title = !string.IsNullOrWhiteSpace(release.Name) ? release.Name : (release.TagName ?? string.Empty);
            var changelog = release.Body?.Trim() ?? string.Empty;

            return new UpdateInfo(latestVersion, release.TagName ?? string.Empty, title, changelog, installerUrl, zipUrl, release.HtmlUrl);
        }
        catch
        {
            return null;
        }
    }

    public static async Task<bool> DownloadFileAsync(string url, string destinationPath, CancellationToken cancellationToken, IProgress<double>? progress = null)
    {
        try
        {
            using var response = await Client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var target = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);

            var buffer = new byte[81920];
            long totalRead = 0;
            int bytesRead;

            while ((bytesRead = await source.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await target.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                totalRead += bytesRead;

                if (totalBytes > 0 && progress != null)
                {
                    var percentage = Math.Min(100.0, Math.Max(0.0, (double)totalRead / totalBytes * 100.0));
                    progress.Report(percentage);
                }
            }

            progress?.Report(100.0);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryParseVersion(string? value, out Version version)
    {
        version = new Version(1, 0, 0, 0);
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var raw = value.Trim();
        if (raw.StartsWith("v", StringComparison.OrdinalIgnoreCase))
        {
            raw = raw[1..];
        }

        var cut = raw.IndexOfAny(['-', '+']);
        if (cut >= 0)
        {
            raw = raw[..cut];
        }

        if (Version.TryParse(raw, out var parsed) && parsed != null)
        {
            version = parsed;
            return true;
        }

        return false;
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(8)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("DockBar");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private sealed class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("body")]
        public string? Body { get; set; }

        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; set; }

        [JsonPropertyName("draft")]
        public bool Draft { get; set; }

        [JsonPropertyName("prerelease")]
        public bool PreRelease { get; set; }

        [JsonPropertyName("assets")]
        public GitHubAsset[]? Assets { get; set; }
    }

    private sealed class GitHubAsset
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("browser_download_url")]
        public string? DownloadUrl { get; set; }
    }
}
