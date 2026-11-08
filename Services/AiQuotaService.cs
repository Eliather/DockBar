using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using DockBar.Models;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;

namespace DockBar.Services;

public class AiQuotaBarItem : INotifyPropertyChanged
{
    private string _label = string.Empty;
    private double _availablePercent = 100;
    private string _percentText = "--%";
    private string _detailText = string.Empty;
    private Brush _barBrush = CreateFrozenBrush(56, 189, 248);

    public string Key { get; init; } = string.Empty;

    public string Label
    {
        get => _label;
        set
        {
            if (_label != value)
            {
                _label = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ProviderShortLabel));
                OnPropertyChanged(nameof(WindowShortLabel));
            }
        }
    }

    public string ProviderShortLabel
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_label)) return string.Empty;
            var parts = _label.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length > 0 ? parts[0] : _label;
        }
    }

    public string WindowShortLabel
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_label)) return string.Empty;
            var parts = _label.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length > 1 ? parts[1] : string.Empty;
        }
    }

    public double AvailablePercent
    {
        get => _availablePercent;
        set { if (Math.Abs(_availablePercent - value) > 0.05) { _availablePercent = value; OnPropertyChanged(); } }
    }

    public string PercentText
    {
        get => _percentText;
        set { if (_percentText != value) { _percentText = value; OnPropertyChanged(); } }
    }

    public string DetailText
    {
        get => _detailText;
        set
        {
            if (_detailText != value)
            {
                _detailText = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TooltipText));
            }
        }
    }

    public string TooltipText => _detailText;

    public Brush BarBrush
    {
        get => _barBrush;
        set { if (!Equals(_barBrush, value)) { _barBrush = value; OnPropertyChanged(); } }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    internal static SolidColorBrush CreateFrozenBrush(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}

public sealed class AiQuotaService : INotifyPropertyChanged
{
    public static AiQuotaService Instance { get; } = new();

    private static readonly Lazy<HttpClient> LazySharedHttpClient = new(() => CreateHttpClient(ignoreCertErrors: false));
    private static readonly Lazy<HttpClient> LazyLocalHttpClient = new(() => CreateHttpClient(ignoreCertErrors: true));
    private static HttpClient SharedHttpClient => LazySharedHttpClient.Value;
    private static HttpClient LocalHttpClient => LazyLocalHttpClient.Value;

    private static readonly SolidColorBrush AgyBrush = AiQuotaBarItem.CreateFrozenBrush(56, 189, 248);    // Cyan-Blue
    private static readonly SolidColorBrush GeminiBrush = AiQuotaBarItem.CreateFrozenBrush(99, 102, 241); // Indigo-Blue
    private static readonly SolidColorBrush ClaudeBrush = AiQuotaBarItem.CreateFrozenBrush(249, 115, 22); // Coral-Orange
    private static readonly SolidColorBrush GptBrush = AiQuotaBarItem.CreateFrozenBrush(16, 185, 129);    // Emerald-Green
    private static readonly SolidColorBrush WarnBrush = AiQuotaBarItem.CreateFrozenBrush(245, 158, 11);   // Amber
    private static readonly SolidColorBrush LowBrush = AiQuotaBarItem.CreateFrozenBrush(239, 68, 68);     // Red

    private int _cachedAgyPid;
    private int _cachedAgyPort;
    private string? _cachedAgyCsrf;
    private bool _cachedAgyHttps = true;
    private DateTime _lastAgyDiscoveryUtc = DateTime.MinValue;
    private List<(int Pid, string Csrf, int Port)> _cachedAgyCandidates = new();

    private string _antigravityStatus = "Sin verificar";
    private string _geminiStatus = "Sin verificar";
    private string _claudeStatus = "Sin verificar";
    private string _chatGptStatus = "Sin verificar";
    private string _combinedTooltip = "Cuota de IA";
    private bool _isRefreshing;
    private AiConfig? _pendingRefreshConfig;

    public ObservableCollection<AiQuotaBarItem> Bars { get; } = new();

    public string AntigravityStatus
    {
        get => _antigravityStatus;
        private set { if (_antigravityStatus != value) { _antigravityStatus = value; OnPropertyChanged(); } }
    }

    public string GeminiStatus
    {
        get => _geminiStatus;
        private set { if (_geminiStatus != value) { _geminiStatus = value; OnPropertyChanged(); } }
    }

    public string ClaudeStatus
    {
        get => _claudeStatus;
        private set { if (_claudeStatus != value) { _claudeStatus = value; OnPropertyChanged(); } }
    }

    public string ChatGptStatus
    {
        get => _chatGptStatus;
        private set { if (_chatGptStatus != value) { _chatGptStatus = value; OnPropertyChanged(); } }
    }

    public string CombinedTooltip
    {
        get => _combinedTooltip;
        private set { if (_combinedTooltip != value) { _combinedTooltip = value; OnPropertyChanged(); } }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private AiQuotaService() { }

    private static HttpClient CreateHttpClient(bool ignoreCertErrors)
    {
        var handler = new HttpClientHandler();
        if (ignoreCertErrors)
        {
            handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;
        }
        var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(8)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) DockBar/1.8");
        return client;
    }

    public void SyncStructure(AiConfig? aiConfig)
    {
        aiConfig ??= new AiConfig();
        var desired = new List<(string Key, string Label, SolidColorBrush BaseBrush)>();

        if (aiConfig.Antigravity.Enabled)
        {
            if (aiConfig.Antigravity.Show5Hr) desired.Add(("AGY_5H", "AGY 5H", AgyBrush));
            if (aiConfig.Antigravity.ShowWeek) desired.Add(("AGY_WK", "AGY WK", AgyBrush));
        }
        if (aiConfig.Gemini.Enabled)
        {
            if (aiConfig.Gemini.Show5Hr) desired.Add(("GEM_5H", "GEM 5H", GeminiBrush));
            if (aiConfig.Gemini.ShowWeek) desired.Add(("GEM_WK", "GEM WK", GeminiBrush));
        }
        if (aiConfig.Claude.Enabled)
        {
            if (aiConfig.Claude.Show5Hr) desired.Add(("CLD_5H", "CLD 5H", ClaudeBrush));
            if (aiConfig.Claude.ShowWeek) desired.Add(("CLD_WK", "CLD WK", ClaudeBrush));
        }
        if (aiConfig.ChatGpt.Enabled)
        {
            if (aiConfig.ChatGpt.Show5Hr) desired.Add(("GPT_5H", "GPT 5H", GptBrush));
            if (aiConfig.ChatGpt.ShowWeek) desired.Add(("GPT_WK", "GPT WK", GptBrush));
        }

        void ApplyOnUi()
        {
            var existingByKey = Bars.ToDictionary(b => b.Key, StringComparer.OrdinalIgnoreCase);
            bool sameOrder = Bars.Count == desired.Count &&
                             Bars.Select(b => b.Key).SequenceEqual(desired.Select(d => d.Key), StringComparer.OrdinalIgnoreCase);
            if (sameOrder)
            {
                return;
            }

            Bars.Clear();
            foreach (var d in desired)
            {
                if (existingByKey.TryGetValue(d.Key, out var existing))
                {
                    existing.Label = d.Label;
                    Bars.Add(existing);
                }
                else
                {
                    Bars.Add(new AiQuotaBarItem
                    {
                        Key = d.Key,
                        Label = d.Label,
                        AvailablePercent = 0,
                        PercentText = "--%",
                        BarBrush = d.BaseBrush,
                        DetailText = $"{d.Label}: Sin datos aún"
                    });
                }
            }
        }

        if (System.Windows.Application.Current?.Dispatcher != null &&
            !System.Windows.Application.Current.Dispatcher.CheckAccess())
        {
            System.Windows.Application.Current.Dispatcher.Invoke(ApplyOnUi);
        }
        else
        {
            ApplyOnUi();
        }
    }

    public async Task RefreshAllAsync(AiConfig? aiConfig)
    {
        if (aiConfig == null)
        {
            return;
        }

        if (_isRefreshing)
        {
            _pendingRefreshConfig = aiConfig;
            return;
        }

        _isRefreshing = true;
        try
        {
            SyncStructure(aiConfig);

            await Task.Run(async () =>
            {
                AntigravityQuotaResult? agyResult = null;
                if (aiConfig.Antigravity.Enabled || (aiConfig.Gemini.Enabled && aiConfig.Gemini.UseAutoDetect))
                {
                    agyResult = await FetchAntigravityQuotaAsync().ConfigureAwait(false);
                }

                if (aiConfig.Antigravity.Enabled)
                {
                    if (agyResult is { Connected: true })
                    {
                        AntigravityStatus = $"Conectado ({agyResult.SummaryLabel})";
                        UpdateBar("AGY_5H", agyResult.Available5Hr, agyResult.Reset5HrText, AgyBrush, "Antigravity (5HR)");
                        UpdateBar("AGY_WK", agyResult.AvailableWeek, agyResult.ResetWeekText, AgyBrush, "Antigravity (Week)");
                    }
                    else
                    {
                        AntigravityStatus = agyResult?.ErrorMessage ?? "Antigravity no detectado en ejecución";
                        UpdateBarOffline("AGY_5H", "Antigravity (5HR): No detectado");
                        UpdateBarOffline("AGY_WK", "Antigravity (Week): No detectado");
                    }
                }

                if (aiConfig.Gemini.Enabled || !string.IsNullOrWhiteSpace(aiConfig.Gemini.WebToken) || AiWebSessionBridge.HasSession("Gemini"))
                {
                    var gemResult = await FetchGeminiQuotaAsync(aiConfig.Gemini, agyResult).ConfigureAwait(false);
                    GeminiStatus = gemResult.StatusText;
                    if (aiConfig.Gemini.Enabled)
                    {
                        if (gemResult.Connected)
                        {
                            UpdateBar("GEM_5H", gemResult.Available5Hr, gemResult.Reset5HrText, GeminiBrush, "Gemini (5HR)");
                            UpdateBar("GEM_WK", gemResult.AvailableWeek, gemResult.ResetWeekText, GeminiBrush, "Gemini (Week)");
                        }
                        else
                        {
                            UpdateBarOffline("GEM_5H", $"Gemini (5HR): {gemResult.StatusText}");
                            UpdateBarOffline("GEM_WK", $"Gemini (Week): {gemResult.StatusText}");
                        }
                    }
                }

                if (aiConfig.Claude.Enabled || !string.IsNullOrWhiteSpace(aiConfig.Claude.WebToken) || AiWebSessionBridge.HasSession("Claude"))
                {
                    var cldResult = await FetchClaudeQuotaAsync(aiConfig.Claude, agyResult).ConfigureAwait(false);
                    ClaudeStatus = cldResult.StatusText;
                    if (aiConfig.Claude.Enabled)
                    {
                        if (cldResult.Connected)
                        {
                            UpdateBar("CLD_5H", cldResult.Available5Hr, cldResult.Reset5HrText, ClaudeBrush, "Claude (5HR)");
                            UpdateBar("CLD_WK", cldResult.AvailableWeek, cldResult.ResetWeekText, ClaudeBrush, "Claude (Week)");
                        }
                        else
                        {
                            UpdateBarOffline("CLD_5H", $"Claude (5HR): {cldResult.StatusText}");
                            UpdateBarOffline("CLD_WK", $"Claude (Week): {cldResult.StatusText}");
                        }
                    }
                }

                if (aiConfig.ChatGpt.Enabled || !string.IsNullOrWhiteSpace(aiConfig.ChatGpt.WebToken) || AiWebSessionBridge.HasSession("ChatGpt"))
                {
                    var gptResult = await FetchChatGptQuotaAsync(aiConfig.ChatGpt).ConfigureAwait(false);
                    ChatGptStatus = gptResult.StatusText;
                    if (aiConfig.ChatGpt.Enabled)
                    {
                        if (gptResult.Connected)
                        {
                            UpdateBar("GPT_5H", gptResult.Available5Hr, gptResult.Reset5HrText, GptBrush, "ChatGPT (5HR)");
                            UpdateBar("GPT_WK", gptResult.AvailableWeek, gptResult.ResetWeekText, GptBrush, "ChatGPT (Week)");
                        }
                        else
                        {
                            UpdateBarOffline("GPT_5H", $"ChatGPT (5HR): {gptResult.StatusText}");
                            UpdateBarOffline("GPT_WK", $"ChatGPT (Week): {gptResult.StatusText}");
                        }
                    }
                }

                RebuildCombinedTooltip();
            }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"AiQuotaService refresh error: {ex.Message}");
        }
        finally
        {
            _isRefreshing = false;
            var next = Interlocked.Exchange(ref _pendingRefreshConfig, null);
            if (next != null)
            {
                _ = RefreshAllAsync(next);
            }
        }
    }

    private void UpdateBar(string key, double availablePct, string? resetText, SolidColorBrush baseBrush, string title)
    {
        var clamped = Math.Clamp(Math.Round(availablePct), 0, 100);
        Brush chosenBrush = baseBrush;
        if (clamped <= 10) chosenBrush = LowBrush;
        else if (clamped <= 25) chosenBrush = WarnBrush;

        var detail = string.IsNullOrWhiteSpace(resetText)
            ? $"• {title}: {clamped:F0}% disponible"
            : $"• {title}: {clamped:F0}% disponible ({resetText})";

        void Apply()
        {
            var bar = Bars.FirstOrDefault(b => string.Equals(b.Key, key, StringComparison.OrdinalIgnoreCase));
            if (bar != null)
            {
                bar.AvailablePercent = clamped;
                bar.PercentText = $"{clamped:F0}%";
                bar.BarBrush = chosenBrush;
                bar.DetailText = detail;
            }
        }

        if (System.Windows.Application.Current?.Dispatcher != null &&
            !System.Windows.Application.Current.Dispatcher.CheckAccess())
        {
            System.Windows.Application.Current.Dispatcher.BeginInvoke((Action)Apply);
        }
        else
        {
            Apply();
        }
    }

    private void UpdateBarOffline(string key, string detail)
    {
        void Apply()
        {
            var bar = Bars.FirstOrDefault(b => string.Equals(b.Key, key, StringComparison.OrdinalIgnoreCase));
            if (bar != null)
            {
                bar.AvailablePercent = 0;
                bar.PercentText = "--%";
                bar.DetailText = $"• {detail}";
            }
        }

        if (System.Windows.Application.Current?.Dispatcher != null &&
            !System.Windows.Application.Current.Dispatcher.CheckAccess())
        {
            System.Windows.Application.Current.Dispatcher.BeginInvoke((Action)Apply);
        }
        else
        {
            Apply();
        }
    }

    private void RebuildCombinedTooltip()
    {
        void Apply()
        {
            var sb = new StringBuilder();
            sb.AppendLine(LocalizationService.Get("Dock_AiQuotaTooltipTitle"));
            if (Bars.Count > 0)
            {
                sb.AppendLine();
                foreach (var bar in Bars)
                {
                    sb.AppendLine(bar.DetailText);
                }
            }
            CombinedTooltip = sb.ToString().TrimEnd();
        }

        if (System.Windows.Application.Current?.Dispatcher != null &&
            !System.Windows.Application.Current.Dispatcher.CheckAccess())
        {
            System.Windows.Application.Current.Dispatcher.BeginInvoke((Action)Apply);
        }
        else
        {
            Apply();
        }
    }

    private sealed class ProviderQuotaResult
    {
        public bool Connected { get; init; }
        public double Available5Hr { get; init; } = 100;
        public double AvailableWeek { get; init; } = 100;
        public string? Reset5HrText { get; init; }
        public string? ResetWeekText { get; init; }
        public string StatusText { get; init; } = string.Empty;
    }

    private sealed class AntigravityQuotaResult
    {
        public bool Connected { get; init; }
        public double Available5Hr { get; init; } = 100;
        public double AvailableWeek { get; init; } = 100;
        public string? Reset5HrText { get; init; }
        public string? ResetWeekText { get; init; }
        public double? GeminiAvailable5Hr { get; init; }
        public string? GeminiResetText { get; init; }
        public double? ClaudeAvailable5Hr { get; init; }
        public string? ClaudeResetText { get; init; }
        public string SummaryLabel { get; init; } = "Local";
        public string? ErrorMessage { get; init; }
    }

    private async Task<AntigravityQuotaResult> FetchAntigravityQuotaAsync()
    {
        try
        {
            if (_cachedAgyPid > 0 && _cachedAgyPort > 0 && !string.IsNullOrEmpty(_cachedAgyCsrf))
            {
                var cachedRes = await TryQueryAntigravityPortAsync(_cachedAgyPort, _cachedAgyCsrf, _cachedAgyHttps).ConfigureAwait(false);
                if (cachedRes != null)
                {
                    return cachedRes;
                }
            }

            var candidates = DiscoverAntigravityProcesses();
            foreach (var (pid, csrf, cmdPort) in candidates)
            {
                var ports = GetListeningPortsForPid(pid);
                if (cmdPort > 0 && !ports.Contains(cmdPort))
                {
                    ports.Insert(0, cmdPort);
                }

                foreach (var port in ports)
                {
                    var httpsRes = await TryQueryAntigravityPortAsync(port, csrf, useHttps: true).ConfigureAwait(false);
                    if (httpsRes != null)
                    {
                        _cachedAgyPid = pid;
                        _cachedAgyPort = port;
                        _cachedAgyCsrf = csrf;
                        _cachedAgyHttps = true;
                        return httpsRes;
                    }

                    var httpRes = await TryQueryAntigravityPortAsync(port, csrf, useHttps: false).ConfigureAwait(false);
                    if (httpRes != null)
                    {
                        _cachedAgyPid = pid;
                        _cachedAgyPort = port;
                        _cachedAgyCsrf = csrf;
                        _cachedAgyHttps = false;
                        return httpRes;
                    }
                }
            }

            return new AntigravityQuotaResult
            {
                Connected = false,
                ErrorMessage = "Antigravity IDE no detectado en ejecución"
            };
        }
        catch (Exception ex)
        {
            return new AntigravityQuotaResult
            {
                Connected = false,
                ErrorMessage = $"Error local: {ex.Message}"
            };
        }
    }

    private static async Task<AntigravityQuotaResult?> TryQueryAntigravityPortAsync(int port, string csrf, bool useHttps)
    {
        try
        {
            var scheme = useHttps ? "https" : "http";
            var url = $"{scheme}://127.0.0.1:{port}/exa.language_server_pb.LanguageServerService/GetUserStatus";
            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Headers.Add("X-Codeium-Csrf-Token", csrf);
            req.Headers.Add("Connect-Protocol-Version", "1");
            req.Content = new StringContent(
                "{\"metadata\":{\"ideName\":\"antigravity\",\"extensionName\":\"antigravity\",\"locale\":\"es\"}}",
                Encoding.UTF8,
                "application/json");

            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(1800));
            using var resp = await LocalHttpClient.SendAsync(req, cts.Token).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
            {
                return null;
            }

            var json = await resp.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("userStatus", out var userStatus))
            {
                return null;
            }

            var fractions = new List<double>();
            var geminiFractions = new List<double>();
            var claudeFractions = new List<double>();
            string? primaryReset = null;
            string? geminiReset = null;
            string? claudeReset = null;

            if (userStatus.TryGetProperty("cascadeModelConfigData", out var cascade) &&
                cascade.TryGetProperty("clientModelConfigs", out var configs) &&
                configs.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in configs.EnumerateArray())
                {
                    var label = item.TryGetProperty("label", out var l) ? (l.GetString() ?? "") : "";
                    double frac = 1.0;
                    string? resetIso = null;

                    if (item.TryGetProperty("quotaInfo", out var qi) && qi.ValueKind == JsonValueKind.Object)
                    {
                        if (qi.TryGetProperty("remainingFraction", out var rf) && rf.TryGetDouble(out var d))
                        {
                            frac = Math.Clamp(d, 0.0, 1.0);
                        }
                        else if (qi.TryGetProperty("resetTime", out _))
                        {
                            frac = 0.0;
                        }

                        if (qi.TryGetProperty("resetTime", out var rt))
                        {
                            resetIso = rt.GetString();
                        }
                    }

                    fractions.Add(frac * 100.0);
                    primaryReset ??= FormatResetTime(resetIso);

                    if (label.Contains("Gemini", StringComparison.OrdinalIgnoreCase))
                    {
                        geminiFractions.Add(frac * 100.0);
                        geminiReset ??= FormatResetTime(resetIso);
                    }
                    else if (label.Contains("Claude", StringComparison.OrdinalIgnoreCase) ||
                             label.Contains("Sonnet", StringComparison.OrdinalIgnoreCase) ||
                             label.Contains("Opus", StringComparison.OrdinalIgnoreCase))
                    {
                        claudeFractions.Add(frac * 100.0);
                        claudeReset ??= FormatResetTime(resetIso);
                    }
                }
            }

            var avgAll = fractions.Count > 0 ? fractions.Min() : 100.0;
            var avgWeek = fractions.Count > 0 ? fractions.Average() : 100.0;

            return new AntigravityQuotaResult
            {
                Connected = true,
                Available5Hr = avgAll,
                AvailableWeek = avgWeek,
                Reset5HrText = primaryReset,
                ResetWeekText = primaryReset,
                GeminiAvailable5Hr = geminiFractions.Count > 0 ? geminiFractions.Min() : null,
                GeminiResetText = geminiReset,
                ClaudeAvailable5Hr = claudeFractions.Count > 0 ? claudeFractions.Min() : null,
                ClaudeResetText = claudeReset,
                SummaryLabel = $"{fractions.Count} modelos"
            };
        }
        catch
        {
            return null;
        }
    }

    private List<(int Pid, string Csrf, int Port)> DiscoverAntigravityProcesses()
    {
        if ((DateTime.UtcNow - _lastAgyDiscoveryUtc).TotalSeconds < 20)
        {
            return _cachedAgyCandidates;
        }

        var results = new List<(int Pid, string Csrf, int Port)>();
        try
        {
            bool anyLanguageServerRunning = false;
            foreach (var p in Process.GetProcesses())
            {
                try
                {
                    if (p.ProcessName.StartsWith("language_server", StringComparison.OrdinalIgnoreCase))
                    {
                        anyLanguageServerRunning = true;
                    }
                }
                catch { }
                finally
                {
                    p.Dispose();
                }
            }

            if (!anyLanguageServerRunning)
            {
                _lastAgyDiscoveryUtc = DateTime.UtcNow;
                _cachedAgyCandidates = results;
                return results;
            }

            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -NonInteractive -WindowStyle Hidden -Command \"Get-CimInstance Win32_Process -Filter \\\"Name LIKE 'language_server%'\\\" | Select-Object ProcessId,CommandLine | ConvertTo-Json -Compress\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true
            };
            using var proc = Process.Start(psi);
            if (proc == null) return results;
            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(2500);

            if (!string.IsNullOrWhiteSpace(output))
            {
                using var doc = JsonDocument.Parse(output);
                var root = doc.RootElement;
                if (root.ValueKind == JsonValueKind.Array)
                {
                    foreach (var elem in root.EnumerateArray())
                    {
                        TryParseProcessElement(elem, results);
                    }
                }
                else if (root.ValueKind == JsonValueKind.Object)
                {
                    TryParseProcessElement(root, results);
                }
            }
        }
        catch
        {
        }

        _lastAgyDiscoveryUtc = DateTime.UtcNow;
        _cachedAgyCandidates = results;
        return results;
    }

    private static void TryParseProcessElement(JsonElement elem, List<(int Pid, string Csrf, int Port)> results)
    {
        if (!elem.TryGetProperty("ProcessId", out var pidElem) || !pidElem.TryGetInt32(out var pid))
        {
            return;
        }
        var cmd = elem.TryGetProperty("CommandLine", out var cmdElem) ? (cmdElem.GetString() ?? "") : "";
        if (string.IsNullOrWhiteSpace(cmd))
        {
            return;
        }

        var csrfMatch = Regex.Match(cmd, @"--csrf_token[=\s]+([^\s""]+)", RegexOptions.IgnoreCase);
        if (!csrfMatch.Success)
        {
            return;
        }

        int port = 0;
        var portMatch = Regex.Match(cmd, @"--extension_server_port[=\s]+(\d+)", RegexOptions.IgnoreCase);
        if (portMatch.Success)
        {
            int.TryParse(portMatch.Groups[1].Value, out port);
        }

        results.Add((pid, csrfMatch.Groups[1].Value, port));
    }

    private static List<int> GetListeningPortsForPid(int targetPid)
    {
        var ports = new List<int>();
        try
        {
            uint size = 0;
            GetExtendedTcpTable(IntPtr.Zero, ref size, false, 2, TcpTableClass.TCP_TABLE_OWNER_PID_LISTENER, 0);
            if (size == 0) return ports;

            IntPtr buffer = Marshal.AllocHGlobal((int)size);
            try
            {
                if (GetExtendedTcpTable(buffer, ref size, false, 2, TcpTableClass.TCP_TABLE_OWNER_PID_LISTENER, 0) == 0)
                {
                    int rowCount = Marshal.ReadInt32(buffer);
                    IntPtr rowPtr = IntPtr.Add(buffer, 4);
                    int rowSize = Marshal.SizeOf<MIB_TCPROW_OWNER_PID>();
                    for (int i = 0; i < rowCount; i++)
                    {
                        var row = Marshal.PtrToStructure<MIB_TCPROW_OWNER_PID>(rowPtr);
                        if (row.owningPid == targetPid)
                        {
                            int port = (int)(((row.localPort & 0xFF) << 8) | ((row.localPort >> 8) & 0xFF));
                            if (port > 0 && !ports.Contains(port))
                            {
                                ports.Add(port);
                            }
                        }
                        rowPtr = IntPtr.Add(rowPtr, rowSize);
                    }
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        catch
        {
        }
        return ports;
    }

    public async Task SignInProviderWebAsync(string provider, AiConfig aiConfig)
    {
        if (string.Equals(provider, "Gemini", StringComparison.OrdinalIgnoreCase))
        {
            GeminiStatus = "Inicia sesión en la ventana de Gemini...";
        }
        else if (string.Equals(provider, "Claude", StringComparison.OrdinalIgnoreCase))
        {
            ClaudeStatus = "Inicia sesión en la ventana de Claude...";
        }
        else if (string.Equals(provider, "ChatGpt", StringComparison.OrdinalIgnoreCase))
        {
            ChatGptStatus = "Inicia sesión en la ventana de ChatGPT...";
        }

        var (ok, msg) = await Task.Run(() => AiWebSessionBridge.InteractiveSignInAsync(provider, SharedHttpClient)).ConfigureAwait(false);

        if (string.Equals(provider, "Gemini", StringComparison.OrdinalIgnoreCase))
        {
            GeminiStatus = msg;
        }
        else if (string.Equals(provider, "Claude", StringComparison.OrdinalIgnoreCase))
        {
            ClaudeStatus = msg;
        }
        else if (string.Equals(provider, "ChatGpt", StringComparison.OrdinalIgnoreCase))
        {
            ChatGptStatus = msg;
        }

        if (ok)
        {
            await RefreshAllAsync(aiConfig).ConfigureAwait(false);
        }
    }

    private async Task<ProviderQuotaResult> FetchChatGptQuotaAsync(AiProviderConfig cfg)
    {
        try
        {
            // 0) Si existe una sesión Web real iniciada con el botón "Iniciar sesión Web"
            var webSession = AiWebSessionBridge.GetSession("ChatGpt");
            if (webSession != null)
            {
                // Si la lectura desde Chromium es reciente (< 55s), usarla directamente sin golpear Cloudflare
                if (!string.IsNullOrWhiteSpace(webSession.LastUsageJson) &&
                    (DateTime.UtcNow - new DateTime(webSession.LastUsageUtcTicks, DateTimeKind.Utc)).TotalSeconds < 55)
                {
                    return ParseChatGptWhamUsageJson(webSession.LastUsageJson, "Sesión Web ChatGPT");
                }

                // Intentar primero con el accessToken + Cookie + User-Agent reales de la sesión
                if (!string.IsNullOrWhiteSpace(webSession.AccessToken))
                {
                    using var webReq = new HttpRequestMessage(HttpMethod.Get, "https://chatgpt.com/backend-api/wham/usage");
                    webReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", webSession.AccessToken);
                    if (!string.IsNullOrWhiteSpace(webSession.AccountOrOrgId))
                    {
                        webReq.Headers.Add("ChatGPT-Account-Id", webSession.AccountOrOrgId);
                    }
                    if (!string.IsNullOrWhiteSpace(webSession.CookieHeader))
                    {
                        webReq.Headers.TryAddWithoutValidation("Cookie", webSession.CookieHeader);
                    }
                    if (!string.IsNullOrWhiteSpace(webSession.UserAgent))
                    {
                        webReq.Headers.TryAddWithoutValidation("User-Agent", webSession.UserAgent);
                    }

                    using var webResp = await SharedHttpClient.SendAsync(webReq).ConfigureAwait(false);
                    if (webResp.IsSuccessStatusCode)
                    {
                        var webJson = await webResp.Content.ReadAsStringAsync().ConfigureAwait(false);
                        AiWebSessionBridge.SaveSession("ChatGpt", s =>
                        {
                            s.LastUsageJson = webJson;
                            s.LastUsageUtcTicks = DateTime.UtcNow.Ticks;
                        });
                        return ParseChatGptWhamUsageJson(webJson, "Sesión Web ChatGPT");
                    }
                }

                // Si el token expiró o Cloudflare pidió validación, refrescar silenciosamente desde el perfil Chromium
                var refreshedJson = await AiWebSessionBridge.TrySilentRefreshChatGptUsageJsonAsync().ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(refreshedJson))
                {
                    return ParseChatGptWhamUsageJson(refreshedJson, "Sesión Web ChatGPT");
                }
            }

            var candidates = new List<(string Token, string? AccountId, string SourceLabel)>();

            if (!string.IsNullOrWhiteSpace(cfg.WebToken))
            {
                var webTok = ExtractBearerOrSessionJsonToken(cfg.WebToken);
                if (!string.IsNullOrWhiteSpace(webTok))
                {
                    candidates.Add((webTok, null, webTok.StartsWith("sk-", StringComparison.OrdinalIgnoreCase) ? "API Key OpenAI" : "Token Web"));
                }
            }

            if (cfg.UseAutoDetect)
            {
                var codexAuthPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex", "auth.json");
                if (File.Exists(codexAuthPath))
                {
                    var raw = await File.ReadAllTextAsync(codexAuthPath).ConfigureAwait(false);
                    using var doc = JsonDocument.Parse(raw);
                    var root = doc.RootElement;
                    string? localToken = null;
                    string? localAccId = null;
                    if (root.TryGetProperty("tokens", out var tokensObj) && tokensObj.ValueKind == JsonValueKind.Object)
                    {
                        if (tokensObj.TryGetProperty("access_token", out var at)) localToken = at.GetString();
                        if (tokensObj.TryGetProperty("account_id", out var aid)) localAccId = aid.GetString();
                    }
                    else if (root.TryGetProperty("access_token", out var atRoot))
                    {
                        localToken = atRoot.GetString();
                    }
                    else if (root.TryGetProperty("OPENAI_API_KEY", out var apiKeyProp))
                    {
                        localToken = apiKeyProp.GetString();
                    }

                    if (!string.IsNullOrWhiteSpace(localToken))
                    {
                        candidates.Add((localToken, localAccId, "Auto (~/.codex)"));
                    }
                }
            }

            if (candidates.Count == 0)
            {
                return new ProviderQuotaResult
                {
                    Connected = false,
                    StatusText = "Pulsa «Iniciar sesión Web» para vincular tu cuota real de ChatGPT"
                };
            }

            string? lastError = null;
            foreach (var (token, accountId, sourceLabel) in candidates)
            {
                // 1) Si es una API Key de OpenAI Platform (sk-..., sk-proj-..., sk-svcacct-...)
                if (token.StartsWith("sk-", StringComparison.OrdinalIgnoreCase))
                {
                    using var apiReq = new HttpRequestMessage(HttpMethod.Get, "https://api.openai.com/v1/models");
                    apiReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                    using var apiResp = await SharedHttpClient.SendAsync(apiReq).ConfigureAwait(false);
                    if (apiResp.IsSuccessStatusCode)
                    {
                        var (reqPct, tokPct, resetReq, resetTok) = ParseRateLimitHeaders(apiResp.Headers, "x-ratelimit");
                        return new ProviderQuotaResult
                        {
                            Connected = true,
                            Available5Hr = reqPct ?? 100.0,
                            AvailableWeek = tokPct ?? reqPct ?? 100.0,
                            Reset5HrText = resetReq ?? "API Key (sin cuota Web 5HR)",
                            ResetWeekText = resetTok ?? "API Key (sin cuota Web Week)",
                            StatusText = $"Conectado ({sourceLabel} — usa «Iniciar sesión Web» para % real)"
                        };
                    }
                    if ((int)apiResp.StatusCode == 429)
                    {
                        return new ProviderQuotaResult
                        {
                            Connected = true,
                            Available5Hr = 0,
                            AvailableWeek = 0,
                            Reset5HrText = "Límite alcanzado (429)",
                            ResetWeekText = "Cuota agotada (429)",
                            StatusText = $"Cuota agotada 429 ({sourceLabel})"
                        };
                    }
                    lastError = $"API Key OpenAI inválida (HTTP {(int)apiResp.StatusCode})";
                    continue;
                }

                // 2) Token de sesión Web / Codex OAuth (chatgpt.com)
                using var req = new HttpRequestMessage(HttpMethod.Get, "https://chatgpt.com/backend-api/wham/usage");
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                if (!string.IsNullOrWhiteSpace(accountId))
                {
                    req.Headers.Add("ChatGPT-Account-Id", accountId);
                }

                using var resp = await SharedHttpClient.SendAsync(req).ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode)
                {
                    lastError = $"HTTP {(int)resp.StatusCode} en {sourceLabel} (pulsa «Iniciar sesión Web»)";
                    continue;
                }

                var json = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                return ParseChatGptWhamUsageJson(json, sourceLabel);
            }

            return new ProviderQuotaResult
            {
                Connected = false,
                StatusText = lastError ?? "No se pudo autenticar con ChatGPT (pulsa «Iniciar sesión Web»)"
            };
        }
        catch (Exception ex)
        {
            return new ProviderQuotaResult
            {
                Connected = false,
                StatusText = $"Error: {ex.Message}"
            };
        }
    }

    private static ProviderQuotaResult ParseChatGptWhamUsageJson(string json, string sourceLabel)
    {
        using var usageDoc = JsonDocument.Parse(json);
        var rootEl = usageDoc.RootElement;

        double avail5Hr = 100;
        double availWeek = 100;
        string? reset5Hr = null;
        string? resetWeek = null;

        if (rootEl.TryGetProperty("rate_limit", out var rl) && rl.ValueKind == JsonValueKind.Object)
        {
            bool assigned5Hr = false;
            bool assignedWeek = false;

            void ProcessWindow(JsonElement win, bool isPrimaryFallback)
            {
                if (win.ValueKind != JsonValueKind.Object) return;
                double used = 0;
                if (win.TryGetProperty("used_percent", out var up) && up.TryGetDouble(out var u))
                {
                    used = u;
                }
                var avail = Math.Clamp(100.0 - used, 0.0, 100.0);
                var reset = ExtractUnixOrSecondsReset(win);

                long windowSec = 0;
                if (win.TryGetProperty("limit_window_seconds", out var lws) && lws.TryGetInt64(out var sec))
                {
                    windowSec = sec;
                }

                if (windowSec > 0 && windowSec <= 28800)
                {
                    avail5Hr = avail;
                    reset5Hr = reset;
                    assigned5Hr = true;
                }
                else if (windowSec > 28800)
                {
                    availWeek = avail;
                    resetWeek = reset;
                    assignedWeek = true;
                }
                else if (isPrimaryFallback && !assigned5Hr)
                {
                    avail5Hr = avail;
                    reset5Hr = reset;
                    assigned5Hr = true;
                }
                else if (!isPrimaryFallback && !assignedWeek)
                {
                    availWeek = avail;
                    resetWeek = reset;
                    assignedWeek = true;
                }
            }

            if (rl.TryGetProperty("primary_window", out var pw))
            {
                ProcessWindow(pw, isPrimaryFallback: true);
            }

            if (rl.TryGetProperty("secondary_window", out var sw))
            {
                ProcessWindow(sw, isPrimaryFallback: false);
            }
        }

        return new ProviderQuotaResult
        {
            Connected = true,
            Available5Hr = avail5Hr,
            AvailableWeek = availWeek,
            Reset5HrText = reset5Hr,
            ResetWeekText = resetWeek,
            StatusText = $"Conectado ({sourceLabel})"
        };
    }

    private async Task<ProviderQuotaResult> FetchClaudeQuotaAsync(AiProviderConfig cfg, AntigravityQuotaResult? agyFallback)
    {
        try
        {
            // 0) Si existe una sesión Web real de claude.ai iniciada con "Iniciar sesión Web"
            var webSession = AiWebSessionBridge.GetSession("Claude");
            if (webSession != null)
            {
                if (!string.IsNullOrWhiteSpace(webSession.LastUsageJson) &&
                    (DateTime.UtcNow - new DateTime(webSession.LastUsageUtcTicks, DateTimeKind.Utc)).TotalSeconds < 55)
                {
                    return ParseClaudeUsageJson(webSession.LastUsageJson, "Sesión Web Claude");
                }

                var refreshedJson = await AiWebSessionBridge.TrySilentRefreshClaudeUsageJsonAsync().ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(refreshedJson))
                {
                    return ParseClaudeUsageJson(refreshedJson, "Sesión Web Claude");
                }
            }

            var candidates = new List<(string Token, string SourceLabel)>();

            if (!string.IsNullOrWhiteSpace(cfg.WebToken))
            {
                candidates.Add((cfg.WebToken.Trim(), "Token / API Key"));
            }

            if (cfg.UseAutoDetect)
            {
                var claudeCredsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", ".credentials.json");
                if (File.Exists(claudeCredsPath))
                {
                    var raw = await File.ReadAllTextAsync(claudeCredsPath).ConfigureAwait(false);
                    using var doc = JsonDocument.Parse(raw);
                    if (doc.RootElement.TryGetProperty("claudeAiOauth", out var oauth) &&
                        oauth.TryGetProperty("accessToken", out var at))
                    {
                        var localTok = at.GetString();
                        if (!string.IsNullOrWhiteSpace(localTok))
                        {
                            candidates.Add((localTok, "Auto (~/.claude)"));
                        }
                    }
                }
            }

            string? lastError = null;
            foreach (var (rawToken, sourceLabel) in candidates)
            {
                var cleanToken = ExtractBearerOrSessionJsonToken(rawToken);
                if (string.IsNullOrWhiteSpace(cleanToken)) continue;

                // 1) Si es una API Key estándar de Anthropic Console (sk-ant-api03-...)
                if (cleanToken.StartsWith("sk-ant-api", StringComparison.OrdinalIgnoreCase))
                {
                    using var apiReq = new HttpRequestMessage(HttpMethod.Get, "https://api.anthropic.com/v1/models");
                    apiReq.Headers.Add("x-api-key", cleanToken);
                    apiReq.Headers.Add("anthropic-version", "2023-06-01");
                    using var apiResp = await SharedHttpClient.SendAsync(apiReq).ConfigureAwait(false);
                    if (apiResp.IsSuccessStatusCode)
                    {
                        var (reqPct, tokPct, resetReq, resetTok) = ParseRateLimitHeaders(apiResp.Headers, "anthropic-ratelimit");
                        return new ProviderQuotaResult
                        {
                            Connected = true,
                            Available5Hr = reqPct ?? 100.0,
                            AvailableWeek = tokPct ?? reqPct ?? 100.0,
                            Reset5HrText = resetReq ?? "API Key activa",
                            ResetWeekText = resetTok ?? "API Key activa",
                            StatusText = "Conectado (API Key — usa «Iniciar sesión Web» para % real)"
                        };
                    }
                    if ((int)apiResp.StatusCode == 429)
                    {
                        return new ProviderQuotaResult
                        {
                            Connected = true,
                            Available5Hr = 0,
                            AvailableWeek = 0,
                            Reset5HrText = "Límite alcanzado (429)",
                            ResetWeekText = "Cuota agotada (429)",
                            StatusText = "Cuota agotada 429 (API Key Anthropic)"
                        };
                    }
                    lastError = $"API Key Anthropic inválida (HTTP {(int)apiResp.StatusCode})";
                    continue;
                }

                // 2) Si es Cookie sessionKey de claude.ai Web (sk-ant-sid01-... o sessionKey=...)
                if (cleanToken.StartsWith("sk-ant-sid", StringComparison.OrdinalIgnoreCase) ||
                    cleanToken.Contains("sessionKey=", StringComparison.OrdinalIgnoreCase))
                {
                    var sessionKey = cleanToken.Replace("sessionKey=", "", StringComparison.OrdinalIgnoreCase).Trim().Trim(';');
                    using var orgReq = new HttpRequestMessage(HttpMethod.Get, "https://claude.ai/api/organizations");
                    orgReq.Headers.Add("Cookie", $"sessionKey={sessionKey}");
                    using var orgResp = await SharedHttpClient.SendAsync(orgReq).ConfigureAwait(false);
                    if (orgResp.IsSuccessStatusCode)
                    {
                        var orgJson = await orgResp.Content.ReadAsStringAsync().ConfigureAwait(false);
                        using var orgDoc = JsonDocument.Parse(orgJson);
                        if (orgDoc.RootElement.ValueKind == JsonValueKind.Array && orgDoc.RootElement.GetArrayLength() > 0)
                        {
                            var orgUuid = orgDoc.RootElement[0].TryGetProperty("uuid", out var u) ? u.GetString() : null;
                            if (!string.IsNullOrWhiteSpace(orgUuid))
                            {
                                using var usageReq = new HttpRequestMessage(HttpMethod.Get, $"https://claude.ai/api/organizations/{orgUuid}/usage");
                                usageReq.Headers.Add("Cookie", $"sessionKey={sessionKey}");
                                using var usageResp = await SharedHttpClient.SendAsync(usageReq).ConfigureAwait(false);
                                if (usageResp.IsSuccessStatusCode)
                                {
                                    var usageJson = await usageResp.Content.ReadAsStringAsync().ConfigureAwait(false);
                                    return ParseClaudeUsageJson(usageJson, "Web (claude.ai)");
                                }
                            }
                        }
                    }
                    lastError = $"Cloudflare / SessionKey bloqueada (HTTP {(int)orgResp.StatusCode} — pulsa «Iniciar sesión Web»)";
                    continue;
                }

                // 3) Si es OAuth token de Claude Code (sk-ant-oat...)
                using var req = new HttpRequestMessage(HttpMethod.Get, "https://api.anthropic.com/api/oauth/usage");
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", cleanToken);
                req.Headers.Add("anthropic-beta", "oauth-2025-04-20");

                using var resp = await SharedHttpClient.SendAsync(req).ConfigureAwait(false);
                if (resp.IsSuccessStatusCode)
                {
                    var json = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                    return ParseClaudeUsageJson(json, sourceLabel);
                }
                lastError = $"Token Claude rechazado (HTTP {(int)resp.StatusCode})";
            }

            // Fallback a Antigravity local si tiene modelos Claude activos
            if (cfg.UseAutoDetect && agyFallback is { Connected: true, ClaudeAvailable5Hr: not null })
            {
                return new ProviderQuotaResult
                {
                    Connected = true,
                    Available5Hr = agyFallback.ClaudeAvailable5Hr.Value,
                    AvailableWeek = agyFallback.ClaudeAvailable5Hr.Value,
                    Reset5HrText = agyFallback.ClaudeResetText,
                    ResetWeekText = agyFallback.ClaudeResetText,
                    StatusText = "Conectado (vía Antigravity)"
                };
            }

            return new ProviderQuotaResult
            {
                Connected = false,
                StatusText = lastError ?? "Pulsa «Iniciar sesión Web» para vincular tu cuota real de Claude"
            };
        }
        catch (Exception ex)
        {
            return new ProviderQuotaResult
            {
                Connected = false,
                StatusText = $"Error: {ex.Message}"
            };
        }
    }

    private static ProviderQuotaResult ParseClaudeUsageJson(string json, string sourceLabel)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        double avail5Hr = 100;
        double availWeek = 100;
        string? reset5Hr = null;
        string? resetWeek = null;

        if (root.TryGetProperty("five_hour", out var fh) && fh.ValueKind == JsonValueKind.Object)
        {
            if (fh.TryGetProperty("utilization", out var util) && util.TryGetDouble(out var u))
            {
                avail5Hr = Math.Clamp(100.0 - u, 0.0, 100.0);
            }
            if (fh.TryGetProperty("resets_at", out var ra))
            {
                var rawReset = ra.GetString();
                reset5Hr = FormatResetTime(rawReset) ?? rawReset;
            }
        }

        JsonElement weekElem = default;
        bool hasWeek =
            (root.TryGetProperty("seven_day", out weekElem) && weekElem.ValueKind == JsonValueKind.Object) ||
            (root.TryGetProperty("seven_day_sonnet", out weekElem) && weekElem.ValueKind == JsonValueKind.Object) ||
            (root.TryGetProperty("seven_day_opus", out weekElem) && weekElem.ValueKind == JsonValueKind.Object);

        if (hasWeek)
        {
            if (weekElem.TryGetProperty("utilization", out var util2) && util2.TryGetDouble(out var u2))
            {
                availWeek = Math.Clamp(100.0 - u2, 0.0, 100.0);
            }
            if (weekElem.TryGetProperty("resets_at", out var ra2))
            {
                var rawReset2 = ra2.GetString();
                resetWeek = FormatResetTime(rawReset2) ?? rawReset2;
            }
        }

        return new ProviderQuotaResult
        {
            Connected = true,
            Available5Hr = avail5Hr,
            AvailableWeek = availWeek,
            Reset5HrText = reset5Hr,
            ResetWeekText = resetWeek,
            StatusText = $"Conectado ({sourceLabel})"
        };
    }

    private static ProviderQuotaResult ParseGeminiWebUsageJson(string json, string sourceLabel)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        double used5Hr = root.TryGetProperty("used5Hr", out var u5) && u5.TryGetDouble(out var v5) ? v5 : 0;
        double usedWeek = root.TryGetProperty("usedWeek", out var uw) && uw.TryGetDouble(out var vw) ? vw : used5Hr;
        string? reset5Hr = root.TryGetProperty("reset5Hr", out var r5) ? r5.GetString() : null;
        string? resetWeek = root.TryGetProperty("resetWeek", out var rw) ? rw.GetString() : reset5Hr;

        return new ProviderQuotaResult
        {
            Connected = true,
            Available5Hr = Math.Clamp(100.0 - used5Hr, 0.0, 100.0),
            AvailableWeek = Math.Clamp(100.0 - usedWeek, 0.0, 100.0),
            Reset5HrText = reset5Hr,
            ResetWeekText = resetWeek,
            StatusText = $"Conectado ({sourceLabel})"
        };
    }

    private async Task<ProviderQuotaResult> FetchGeminiQuotaAsync(AiProviderConfig cfg, AntigravityQuotaResult? agyFallback)
    {
        try
        {
            // 0) Si existe sesión Web real de gemini.google.com/usage vinculada con "Iniciar sesión Web"
            var webSession = AiWebSessionBridge.GetSession("Gemini");
            if (webSession != null && !string.IsNullOrWhiteSpace(webSession.LastUsageJson))
            {
                if ((DateTime.UtcNow - new DateTime(webSession.LastUsageUtcTicks, DateTimeKind.Utc)).TotalSeconds < 55)
                {
                    return ParseGeminiWebUsageJson(webSession.LastUsageJson, "Sesión Web Gemini");
                }

                var refreshedWebJson = await AiWebSessionBridge.TrySilentRefreshGeminiUsageJsonAsync().ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(refreshedWebJson))
                {
                    return ParseGeminiWebUsageJson(refreshedWebJson, "Sesión Web Gemini");
                }
            }

            var candidates = new List<(string Token, string? RefreshToken, string SourceLabel)>();

            if (!string.IsNullOrWhiteSpace(cfg.WebToken))
            {
                var webTok = ExtractBearerOrSessionJsonToken(cfg.WebToken);
                if (!string.IsNullOrWhiteSpace(webTok))
                {
                    candidates.Add((webTok, null, webTok.StartsWith("AIza", StringComparison.OrdinalIgnoreCase) ? "API Key AI Studio" : "Token OAuth"));
                }
            }

            if (cfg.UseAutoDetect)
            {
                var geminiCredsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".gemini", "oauth_creds.json");
                if (File.Exists(geminiCredsPath))
                {
                    var raw = await File.ReadAllTextAsync(geminiCredsPath).ConfigureAwait(false);
                    using var doc = JsonDocument.Parse(raw);
                    var localTok = doc.RootElement.TryGetProperty("access_token", out var at) ? at.GetString() : null;
                    var localRefresh = doc.RootElement.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : null;
                    long expiryMs = doc.RootElement.TryGetProperty("expiry_date", out var ed) && ed.TryGetInt64(out var ems) ? ems : 0;

                    if (expiryMs > 0 && DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() >= expiryMs && !string.IsNullOrWhiteSpace(localRefresh))
                    {
                        var refreshed = await AiWebSessionBridge.TryRefreshGoogleAccessTokenAsync(localRefresh, SharedHttpClient).ConfigureAwait(false);
                        if (!string.IsNullOrWhiteSpace(refreshed))
                        {
                            localTok = refreshed;
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(localTok))
                    {
                        candidates.Add((localTok, localRefresh, "Auto (~/.gemini)"));
                    }
                }
            }

            string? lastError = null;
            foreach (var (initialToken, refreshToken, sourceLabel) in candidates)
            {
                var token = initialToken;

                // 1) Si es una API Key de Google AI Studio (empieza con AIza...)
                if (token.StartsWith("AIza", StringComparison.OrdinalIgnoreCase))
                {
                    var apiUrl = $"https://generativelanguage.googleapis.com/v1beta/models?key={Uri.EscapeDataString(token)}&pageSize=2";
                    using var apiReq = new HttpRequestMessage(HttpMethod.Get, apiUrl);
                    using var apiResp = await SharedHttpClient.SendAsync(apiReq).ConfigureAwait(false);
                    if (apiResp.IsSuccessStatusCode)
                    {
                        var (reqPct, tokPct, resetReq, resetTok) = ParseRateLimitHeaders(apiResp.Headers, "x-ratelimit");
                        double avail5H = reqPct ?? agyFallback?.GeminiAvailable5Hr ?? 100.0;
                        double availWk = tokPct ?? (agyFallback is { Connected: true } ? agyFallback.AvailableWeek : avail5H);
                        return new ProviderQuotaResult
                        {
                            Connected = true,
                            Available5Hr = avail5H,
                            AvailableWeek = availWk,
                            Reset5HrText = resetReq ?? agyFallback?.GeminiResetText ?? "API Key activa",
                            ResetWeekText = resetTok ?? agyFallback?.GeminiResetText ?? "API Key activa",
                            StatusText = $"Conectado ({sourceLabel})"
                        };
                    }
                    if ((int)apiResp.StatusCode == 429)
                    {
                        return new ProviderQuotaResult
                        {
                            Connected = true,
                            Available5Hr = 0,
                            AvailableWeek = 0,
                            Reset5HrText = "Cuota agotada (429)",
                            ResetWeekText = "Cuota agotada (429)",
                            StatusText = $"Cuota agotada 429 ({sourceLabel})"
                        };
                    }
                    lastError = $"API Key de Gemini inválida (HTTP {(int)apiResp.StatusCode})";
                    continue;
                }

                // 2) OAuth Bearer token contra cloudcode-pa.googleapis.com (requiere User-Agent: antigravity)
                for (int attempt = 0; attempt < 2; attempt++)
                {
                    using var req = new HttpRequestMessage(HttpMethod.Post, "https://cloudcode-pa.googleapis.com/v1internal:retrieveUserQuota");
                    req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                    req.Headers.TryAddWithoutValidation("User-Agent", "antigravity");
                    req.Content = new StringContent("{}", Encoding.UTF8, "application/json");

                    using var resp = await SharedHttpClient.SendAsync(req).ConfigureAwait(false);
                    if (resp.IsSuccessStatusCode)
                    {
                        var json = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                        using var doc = JsonDocument.Parse(json);
                        var fractions = new List<double>();
                        string? resetText = null;

                        if (doc.RootElement.TryGetProperty("buckets", out var buckets) && buckets.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var b in buckets.EnumerateArray())
                            {
                                if (b.TryGetProperty("remainingFraction", out var rf) && rf.TryGetDouble(out var d))
                                {
                                    fractions.Add(Math.Clamp(d * 100.0, 0.0, 100.0));
                                }
                                if (resetText == null && b.TryGetProperty("resetTime", out var rt))
                                {
                                    resetText = FormatResetTime(rt.GetString());
                                }
                            }
                        }

                        var minPct = fractions.Count > 0 ? fractions.Min() : 100.0;
                        var avgPct = fractions.Count > 0 ? fractions.Average() : 100.0;
                        return new ProviderQuotaResult
                        {
                            Connected = true,
                            Available5Hr = minPct,
                            AvailableWeek = avgPct,
                            Reset5HrText = resetText,
                            ResetWeekText = resetText,
                            StatusText = $"Conectado ({sourceLabel})"
                        };
                    }

                    // Si dio 401 y tenemos refresh_token, renovamos automáticamente y reintentamos
                    if ((int)resp.StatusCode == 401 && attempt == 0 && !string.IsNullOrWhiteSpace(refreshToken))
                    {
                        var refreshed = await AiWebSessionBridge.TryRefreshGoogleAccessTokenAsync(refreshToken, SharedHttpClient).ConfigureAwait(false);
                        if (!string.IsNullOrWhiteSpace(refreshed))
                        {
                            token = refreshed;
                            continue;
                        }
                    }

                    lastError = $"Token OAuth Gemini expirado/inválido (HTTP {(int)resp.StatusCode})";
                    break;
                }
            }

            // Fallback directo a modelos Gemini detectados desde Antigravity
            if (cfg.UseAutoDetect && agyFallback is { Connected: true, GeminiAvailable5Hr: not null })
            {
                return new ProviderQuotaResult
                {
                    Connected = true,
                    Available5Hr = agyFallback.GeminiAvailable5Hr.Value,
                    AvailableWeek = agyFallback.AvailableWeek,
                    Reset5HrText = agyFallback.GeminiResetText,
                    ResetWeekText = agyFallback.GeminiResetText,
                    StatusText = "Conectado (vía Antigravity)"
                };
            }

            return new ProviderQuotaResult
            {
                Connected = false,
                StatusText = lastError ?? "Pulsa «Iniciar sesión Web» o abre Antigravity"
            };
        }
        catch (Exception ex)
        {
            return new ProviderQuotaResult
            {
                Connected = false,
                StatusText = $"Error: {ex.Message}"
            };
        }
    }

    private static (double? ReqPct, double? TokPct, string? ResetReq, string? ResetTok) ParseRateLimitHeaders(HttpResponseHeaders headers, string prefix)
    {
        double? reqPct = null;
        double? tokPct = null;
        string? resetReq = null;
        string? resetTok = null;

        if (TryGetHeaderDouble(headers, $"{prefix}-remaining-requests", out var remReq) &&
            TryGetHeaderDouble(headers, $"{prefix}-limit-requests", out var limReq) && limReq > 0)
        {
            reqPct = Math.Clamp((remReq / limReq) * 100.0, 0.0, 100.0);
        }
        else if (TryGetHeaderDouble(headers, $"{prefix}-requests-remaining", out var remReq2) &&
                 TryGetHeaderDouble(headers, $"{prefix}-requests-limit", out var limReq2) && limReq2 > 0)
        {
            reqPct = Math.Clamp((remReq2 / limReq2) * 100.0, 0.0, 100.0);
        }

        if (TryGetHeaderDouble(headers, $"{prefix}-remaining-tokens", out var remTok) &&
            TryGetHeaderDouble(headers, $"{prefix}-limit-tokens", out var limTok) && limTok > 0)
        {
            tokPct = Math.Clamp((remTok / limTok) * 100.0, 0.0, 100.0);
        }
        else if (TryGetHeaderDouble(headers, $"{prefix}-tokens-remaining", out var remTok2) &&
                 TryGetHeaderDouble(headers, $"{prefix}-tokens-limit", out var limTok2) && limTok2 > 0)
        {
            tokPct = Math.Clamp((remTok2 / limTok2) * 100.0, 0.0, 100.0);
        }

        if (headers.TryGetValues($"{prefix}-reset-requests", out var rr)) resetReq = rr.FirstOrDefault();
        else if (headers.TryGetValues($"{prefix}-requests-reset", out var rr2)) resetReq = FormatResetTime(rr2.FirstOrDefault()) ?? rr2.FirstOrDefault();

        if (headers.TryGetValues($"{prefix}-reset-tokens", out var rt)) resetTok = rt.FirstOrDefault();
        else if (headers.TryGetValues($"{prefix}-tokens-reset", out var rt2)) resetTok = FormatResetTime(rt2.FirstOrDefault()) ?? rt2.FirstOrDefault();

        return (reqPct, tokPct, resetReq, resetTok);
    }

    private static bool TryGetHeaderDouble(HttpResponseHeaders headers, string name, out double value)
    {
        value = 0;
        if (headers.TryGetValues(name, out var values))
        {
            var first = values.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(first) &&
                double.TryParse(first, NumberStyles.Any, CultureInfo.InvariantCulture, out value))
            {
                return true;
            }
        }
        return false;
    }

    private static string ExtractBearerOrSessionJsonToken(string raw)
    {
        var trimmed = raw.Trim();
        if (trimmed.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed.Substring(7).Trim();
        }

        if (trimmed.StartsWith("{") && trimmed.EndsWith("}"))
        {
            try
            {
                using var doc = JsonDocument.Parse(trimmed);
                if (doc.RootElement.TryGetProperty("accessToken", out var at) && at.ValueKind == JsonValueKind.String)
                {
                    return at.GetString() ?? trimmed;
                }
                if (doc.RootElement.TryGetProperty("access_token", out var at2) && at2.ValueKind == JsonValueKind.String)
                {
                    return at2.GetString() ?? trimmed;
                }
            }
            catch
            {
            }
        }

        return trimmed.Trim('"');
    }

    private static string? ExtractUnixOrSecondsReset(JsonElement windowObj)
    {
        if (windowObj.TryGetProperty("reset_after_seconds", out var ras) && ras.TryGetDouble(out var sec) && sec > 0)
        {
            return FormatTimeSpan(TimeSpan.FromSeconds(sec));
        }

        if (windowObj.TryGetProperty("reset_at", out var ra) && ra.TryGetInt64(out var unixSec) && unixSec > 0)
        {
            var target = DateTimeOffset.FromUnixTimeSeconds(unixSec);
            var diff = target - DateTimeOffset.UtcNow;
            if (diff.TotalSeconds > 0)
            {
                return FormatTimeSpan(diff);
            }
        }

        return null;
    }

    private static string? FormatResetTime(string? isoString)
    {
        if (string.IsNullOrWhiteSpace(isoString)) return null;
        if (DateTimeOffset.TryParse(isoString, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dto))
        {
            var diff = dto - DateTimeOffset.UtcNow;
            if (diff.TotalSeconds > 0)
            {
                return FormatTimeSpan(diff);
            }
        }
        return null;
    }

    private static string FormatTimeSpan(TimeSpan ts)
    {
        if (ts.TotalDays >= 1)
        {
            return $"Reinicio en {(int)ts.TotalDays}d {ts.Hours}h";
        }
        if (ts.TotalHours >= 1)
        {
            return $"Reinicio en {(int)ts.TotalHours}h {ts.Minutes}m";
        }
        return $"Reinicio en {Math.Max(1, ts.Minutes)}m";
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private enum TcpTableClass
    {
        TCP_TABLE_BASIC_LISTENER,
        TCP_TABLE_BASIC_CONNECTIONS,
        TCP_TABLE_BASIC_ALL,
        TCP_TABLE_OWNER_PID_LISTENER,
        TCP_TABLE_OWNER_PID_CONNECTIONS,
        TCP_TABLE_OWNER_PID_ALL
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MIB_TCPROW_OWNER_PID
    {
        public uint state;
        public uint localAddr;
        public uint localPort;
        public uint remoteAddr;
        public uint remotePort;
        public uint owningPid;
    }

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern uint GetExtendedTcpTable(
        IntPtr pTcpTable,
        ref uint dwOutBufLen,
        bool sort,
        int ipVersion,
        TcpTableClass tblClass,
        uint reserved);
}
