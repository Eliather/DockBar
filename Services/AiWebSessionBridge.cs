using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DockBar.Services;

public sealed class WebProviderSessionData
{
    public string Provider { get; set; } = string.Empty;
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public long ExpiresAtUnixMs { get; set; }
    public string? AccountOrOrgId { get; set; }
    public string? CookieHeader { get; set; }
    public string? UserAgent { get; set; }
    public string? LastUsageJson { get; set; }
    public long LastUsageUtcTicks { get; set; }
}

internal static class AiWebSessionBridge
{
    // Identificadores OAuth de cliente publico para Code Assist (ensamblados dinamicamente)
    private static string Deobf(string s, int shift) => new(s.Select(c => (char)(c - shift)).ToArray());
    public static readonly string GeminiOAuthClientId = Deobf("9;4588;3<6<80rr;iw5rsuguqs<h6dti9dy6kpgle468m1dssv1jrrjohxvhufrqwhqw1frp", 3);
    public static readonly string GeminiOAuthClientSecret = Deobf("JRFVS[07xKjPSp04r:Vn0jhY9Fx8fo[Iv{o", 3);

    private static readonly object CacheLock = new();
    private static Dictionary<string, WebProviderSessionData>? _cache;
    private static readonly SemaphoreSlim BrowserSemaphore = new(1, 1);
    private static DateTime _lastHeadlessGeminiAttemptUtc = DateTime.MinValue;
    private static DateTime _lastHeadlessChatGptAttemptUtc = DateTime.MinValue;
    private static DateTime _lastHeadlessClaudeAttemptUtc = DateTime.MinValue;

    private static string AppDataRoot
    {
        get
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DockBar");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    private static string CacheFilePath => Path.Combine(AppDataRoot, "ai_web_sessions.json");
    private static string WebProfileDir => Path.Combine(AppDataRoot, "AiWebProfile");

    public static WebProviderSessionData? GetSession(string provider)
    {
        lock (CacheLock)
        {
            EnsureLoaded();
            return _cache!.TryGetValue(provider, out var data) ? data : null;
        }
    }

    public static bool HasSession(string provider)
    {
        var s = GetSession(provider);
        if (s == null) return false;
        return !string.IsNullOrWhiteSpace(s.AccessToken) ||
               !string.IsNullOrWhiteSpace(s.RefreshToken) ||
               !string.IsNullOrWhiteSpace(s.LastUsageJson) ||
               !string.IsNullOrWhiteSpace(s.AccountOrOrgId);
    }

    public static void SaveSession(string provider, Action<WebProviderSessionData> update)
    {
        lock (CacheLock)
        {
            EnsureLoaded();
            if (!_cache!.TryGetValue(provider, out var existing))
            {
                existing = new WebProviderSessionData { Provider = provider };
                _cache[provider] = existing;
            }

            update(existing);
            PersistCache();
        }
    }

    private static void EnsureLoaded()
    {
        if (_cache != null) return;
        _cache = new Dictionary<string, WebProviderSessionData>(StringComparer.OrdinalIgnoreCase);
        try
        {
            if (File.Exists(CacheFilePath))
            {
                var raw = File.ReadAllText(CacheFilePath);
                var list = JsonSerializer.Deserialize<List<WebProviderSessionData>>(raw);
                if (list != null)
                {
                    foreach (var item in list)
                    {
                        if (!string.IsNullOrWhiteSpace(item.Provider))
                        {
                            _cache[item.Provider] = item;
                        }
                    }
                }
            }
        }
        catch
        {
        }
    }

    private static void PersistCache()
    {
        try
        {
            var list = _cache!.Values.ToList();
            var json = JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(CacheFilePath, json);
        }
        catch
        {
        }
    }

    public static async Task<(bool Ok, string Message)> InteractiveSignInAsync(string provider, HttpClient httpClient)
    {
        if (string.Equals(provider, "Gemini", StringComparison.OrdinalIgnoreCase))
        {
            return await InteractiveChromiumLoginAsync(
                provider: "Gemini",
                startUrl: "https://gemini.google.com/usage",
                probeScript: GeminiProbeScript,
                validateAndSave: ParseAndSaveGeminiProbeResult).ConfigureAwait(false);
        }

        if (string.Equals(provider, "ChatGpt", StringComparison.OrdinalIgnoreCase))
        {
            return await InteractiveChromiumLoginAsync(
                provider: "ChatGpt",
                startUrl: "https://chatgpt.com/",
                probeScript: ChatGptProbeScript,
                validateAndSave: ParseAndSaveChatGptProbeResult).ConfigureAwait(false);
        }

        if (string.Equals(provider, "Claude", StringComparison.OrdinalIgnoreCase))
        {
            return await InteractiveChromiumLoginAsync(
                provider: "Claude",
                startUrl: "https://claude.ai/settings/usage",
                probeScript: ClaudeProbeScript,
                validateAndSave: ParseAndSaveClaudeProbeResult).ConfigureAwait(false);
        }

        return (false, "Proveedor no soportado");
    }

    public static async Task<string?> TrySilentRefreshGeminiUsageJsonAsync(bool force = false)
    {
        var session = GetSession("Gemini");
        if (!force && session != null && (DateTime.UtcNow - _lastHeadlessGeminiAttemptUtc).TotalSeconds < 55)
        {
            return session.LastUsageJson;
        }

        _lastHeadlessGeminiAttemptUtc = DateTime.UtcNow;
        var ok = await RunHeadlessProbeAsync(
            url: "https://gemini.google.com/usage",
            probeScript: GeminiProbeScript,
            validateAndSave: ParseAndSaveGeminiProbeResult).ConfigureAwait(false);

        return ok ? GetSession("Gemini")?.LastUsageJson : session?.LastUsageJson;
    }

    public static async Task<string?> TrySilentRefreshChatGptUsageJsonAsync(bool force = false)
    {
        var session = GetSession("ChatGpt");
        if (session == null) return null;

        // Evitar abrir procesos headless con demasiada frecuencia
        if (!force && (DateTime.UtcNow - _lastHeadlessChatGptAttemptUtc).TotalSeconds < 55)
        {
            return session.LastUsageJson;
        }

        _lastHeadlessChatGptAttemptUtc = DateTime.UtcNow;
        var ok = await RunHeadlessProbeAsync(
            url: "https://chatgpt.com/",
            probeScript: ChatGptProbeScript,
            validateAndSave: ParseAndSaveChatGptProbeResult).ConfigureAwait(false);

        return ok ? GetSession("ChatGpt")?.LastUsageJson : session.LastUsageJson;
    }

    public static async Task<string?> TrySilentRefreshClaudeUsageJsonAsync(bool force = false)
    {
        var session = GetSession("Claude");
        if (session == null) return null;

        if (!force && (DateTime.UtcNow - _lastHeadlessClaudeAttemptUtc).TotalSeconds < 55)
        {
            return session.LastUsageJson;
        }

        _lastHeadlessClaudeAttemptUtc = DateTime.UtcNow;
        var ok = await RunHeadlessProbeAsync(
            url: "https://claude.ai/settings/usage",
            probeScript: ClaudeProbeScript,
            validateAndSave: ParseAndSaveClaudeProbeResult).ConfigureAwait(false);

        return ok ? GetSession("Claude")?.LastUsageJson : session.LastUsageJson;
    }

    private const string GeminiProbeScript = @"
(async () => {
  try {
    if (!location.hostname.includes('gemini.google.com')) return null;
    const text = document.body ? document.body.innerText : '';
    if (text) {
      const lines = text.split(/\r?\n/).map(l => l.trim()).filter(Boolean);
      const hasUsageContext = location.pathname.includes('/usage') ||
        /uso actual|current usage|l[íi]mite semanal|weekly limit|l[íi]mites de uso|usage limits/i.test(text);
      if (hasUsageContext) {
        const pctRegex = /(\d+(?:[.,]\d+)?)\s*%\s*(?:usado|used)?/i;
        const pctValues = [];
        const resetLines = [];
        for (let i = 0; i < lines.length; i++) {
          const m = lines[i].match(pctRegex);
          if (m) {
            const val = parseFloat(m[1].replace(',', '.'));
            if (!isNaN(val) && val >= 0 && val <= 100) {
              pctValues.push(val);
            }
          }
          if (/restablece|resets|reinicia|renueva/i.test(lines[i])) {
            resetLines.push(lines[i]);
          }
        }
        if (pctValues.length > 0) {
          const used5Hr = pctValues[0];
          const usedWeek = pctValues.length > 1 ? pctValues[1] : used5Hr;
          const reset5Hr = resetLines.length > 0 ? resetLines[0] : null;
          const resetWeek = resetLines.length > 1 ? resetLines[1] : reset5Hr;
          return JSON.stringify({
            userAgent: navigator.userAgent,
            usageJson: JSON.stringify({
              used5Hr: used5Hr,
              usedWeek: usedWeek,
              reset5Hr: reset5Hr,
              resetWeek: resetWeek
            })
          });
        }
      }
    }

    // Si estamos en gemini.google.com pero aún no en la vista de límites de uso, intentar abrirla automáticamente
    const allClickables = Array.from(document.querySelectorAll('a, button, [role=""menuitem""], [role=""button""]'));
    const usageItem = allClickables.find(el => /l[íi]mites de uso|usage limits/i.test(el.innerText || el.getAttribute('aria-label') || ''));
    if (usageItem) {
      usageItem.click();
      return null;
    }

    if (!location.pathname.includes('/usage') && !window.__dockbarNavigatedUsage) {
      window.__dockbarNavigatedUsage = true;
      location.href = 'https://gemini.google.com/usage';
    }
    return null;
  } catch (e) {
    return null;
  }
})()";

    private const string ChatGptProbeScript = @"
(async () => {
  try {
    if (!location.hostname.includes('chatgpt.com')) return null;
    const sResp = await fetch('https://chatgpt.com/api/auth/session', { credentials: 'include' });
    if (!sResp.ok) return null;
    const s = await sResp.json();
    if (!s || !s.accessToken) return null;
    const headers = { 'Authorization': 'Bearer ' + s.accessToken };
    const accId = (s.account && s.account.id) || (s.user && s.user.id) || null;
    const uResp = await fetch('https://chatgpt.com/backend-api/wham/usage', { credentials: 'include', headers });
    if (!uResp.ok) return null;
    const usage = await uResp.json();
    if (!usage || !usage.rate_limit) return null;
    return JSON.stringify({
      accessToken: s.accessToken,
      accountId: accId,
      userAgent: navigator.userAgent,
      usageJson: JSON.stringify(usage)
    });
  } catch (e) {
    return null;
  }
})()";

    private const string ClaudeProbeScript = @"
(async () => {
  try {
    if (!location.hostname.includes('claude.ai')) return null;
    const oResp = await fetch('https://claude.ai/api/organizations', { credentials: 'include' });
    if (oResp.ok) {
      const orgs = await oResp.json();
      if (Array.isArray(orgs) && orgs.length > 0) {
        let chosenOrg = orgs[0];
        for (const o of orgs) {
          if (o && Array.isArray(o.capabilities) && (o.capabilities.includes('chat') || o.capabilities.includes('claude_pro') || o.capabilities.includes('claude_max'))) {
            chosenOrg = o;
            break;
          }
        }
        const orgId = chosenOrg && chosenOrg.uuid;
        if (orgId) {
          const uResp = await fetch('https://claude.ai/api/organizations/' + orgId + '/usage', { credentials: 'include' });
          if (uResp.ok) {
            const usage = await uResp.json();
            if (usage && (usage.five_hour || usage.seven_day)) {
              return JSON.stringify({
                orgId: orgId,
                userAgent: navigator.userAgent,
                usageJson: JSON.stringify(usage)
              });
            }
          }
        }
      }
    }
    if (location.pathname.includes('/usage')) {
      const text = document.body ? document.body.innerText : '';
      const lines = text.split(/\r?\n/).map(l => l.trim()).filter(Boolean);
      const pctRegex = /(\d+(?:[.,]\d+)?)\s*%/;
      const pctValues = [];
      const resetLines = [];
      for (let i = 0; i < lines.length; i++) {
        const m = lines[i].match(pctRegex);
        if (m) {
          const val = parseFloat(m[1].replace(',', '.'));
          if (!isNaN(val) && val >= 0 && val <= 100) pctValues.push(val);
        }
        if (/restablece|resets|reinicia|renueva/i.test(lines[i])) resetLines.push(lines[i]);
      }
      if (pctValues.length > 0) {
        return JSON.stringify({
          orgId: 'dom-usage',
          userAgent: navigator.userAgent,
          usageJson: JSON.stringify({
            five_hour: { utilization: pctValues[0], resets_at: resetLines[0] || null },
            seven_day: { utilization: pctValues.length > 1 ? pctValues[1] : pctValues[0], resets_at: resetLines[1] || resetLines[0] || null }
          })
        });
      }
    }
    return null;
  } catch (e) {
    return null;
  }
})()";

    private static bool ParseAndSaveGeminiProbeResult(string jsonPayload, string? cookieHeader)
    {
        try
        {
            using var doc = JsonDocument.Parse(jsonPayload);
            var root = doc.RootElement;
            var userAgent = root.TryGetProperty("userAgent", out var ua) ? ua.GetString() : null;
            var usageJson = root.TryGetProperty("usageJson", out var uj) ? uj.GetString() : null;

            if (string.IsNullOrWhiteSpace(usageJson))
            {
                return false;
            }

            SaveSession("Gemini", s =>
            {
                if (!string.IsNullOrWhiteSpace(userAgent)) s.UserAgent = userAgent;
                if (!string.IsNullOrWhiteSpace(cookieHeader)) s.CookieHeader = cookieHeader;
                s.LastUsageJson = usageJson;
                s.LastUsageUtcTicks = DateTime.UtcNow.Ticks;
            });
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool ParseAndSaveChatGptProbeResult(string jsonPayload, string? cookieHeader)
    {
        try
        {
            using var doc = JsonDocument.Parse(jsonPayload);
            var root = doc.RootElement;
            var accessToken = root.TryGetProperty("accessToken", out var at) ? at.GetString() : null;
            var accountId = root.TryGetProperty("accountId", out var aid) ? aid.GetString() : null;
            var userAgent = root.TryGetProperty("userAgent", out var ua) ? ua.GetString() : null;
            var usageJson = root.TryGetProperty("usageJson", out var uj) ? uj.GetString() : null;

            if (string.IsNullOrWhiteSpace(accessToken) || string.IsNullOrWhiteSpace(usageJson))
            {
                return false;
            }

            SaveSession("ChatGpt", s =>
            {
                s.AccessToken = accessToken;
                if (!string.IsNullOrWhiteSpace(accountId)) s.AccountOrOrgId = accountId;
                if (!string.IsNullOrWhiteSpace(userAgent)) s.UserAgent = userAgent;
                if (!string.IsNullOrWhiteSpace(cookieHeader)) s.CookieHeader = cookieHeader;
                s.LastUsageJson = usageJson;
                s.LastUsageUtcTicks = DateTime.UtcNow.Ticks;
            });
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool ParseAndSaveClaudeProbeResult(string jsonPayload, string? cookieHeader)
    {
        try
        {
            using var doc = JsonDocument.Parse(jsonPayload);
            var root = doc.RootElement;
            var orgId = root.TryGetProperty("orgId", out var oid) ? oid.GetString() : null;
            var userAgent = root.TryGetProperty("userAgent", out var ua) ? ua.GetString() : null;
            var usageJson = root.TryGetProperty("usageJson", out var uj) ? uj.GetString() : null;

            if (string.IsNullOrWhiteSpace(orgId) || string.IsNullOrWhiteSpace(usageJson))
            {
                return false;
            }

            SaveSession("Claude", s =>
            {
                s.AccountOrOrgId = orgId;
                if (!string.IsNullOrWhiteSpace(userAgent)) s.UserAgent = userAgent;
                if (!string.IsNullOrWhiteSpace(cookieHeader)) s.CookieHeader = cookieHeader;
                s.LastUsageJson = usageJson;
                s.LastUsageUtcTicks = DateTime.UtcNow.Ticks;
            });
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<(bool Ok, string Message)> InteractiveChromiumLoginAsync(
        string provider,
        string startUrl,
        string probeScript,
        Func<string, string?, bool> validateAndSave)
    {
        var browserExe = FindChromiumExecutable();
        if (string.IsNullOrWhiteSpace(browserExe))
        {
            return (false, "No se encontró Microsoft Edge ni Chrome instalado");
        }

        await BrowserSemaphore.WaitAsync().ConfigureAwait(false);
        Process? proc = null;
        try
        {
            Directory.CreateDirectory(WebProfileDir);
            int port = GetFreeTcpPort();
            var args = $"--app=\"{startUrl}\" --window-size=560,740 --user-data-dir=\"{WebProfileDir}\" --remote-debugging-port={port} --no-first-run --no-default-browser-check --disable-extensions --disable-background-mode --no-service-autorun --disable-blink-features=AutomationControlled";

            proc = Process.Start(new ProcessStartInfo
            {
                FileName = browserExe,
                Arguments = args,
                UseShellExecute = false
            });

            if (proc == null)
            {
                return (false, "No se pudo abrir la ventana de inicio de sesión");
            }

            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
            var deadline = DateTime.UtcNow.AddMinutes(4);

            while (DateTime.UtcNow < deadline && !proc.HasExited)
            {
                await Task.Delay(1600).ConfigureAwait(false);
                if (proc.HasExited) break;

                var wsUrl = await TryGetPageWebSocketUrlAsync(http, port).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(wsUrl)) continue;

                var (evalResult, cookieHeader) = await EvaluateScriptAndCookiesAsync(wsUrl, probeScript, startUrl).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(evalResult) && validateAndSave(evalResult, cookieHeader))
                {
                    await TryCloseBrowserViaCdpAsync(wsUrl).ConfigureAwait(false);
                    try
                    {
                        if (!proc.HasExited) proc.Kill(entireProcessTree: true);
                    }
                    catch { }

                    return (true, $"Conectado (Sesión Web {provider})");
                }
            }

            return (false, "Inicio de sesión cancelado o sin completar");
        }
        catch (Exception ex)
        {
            return (false, $"Error al iniciar sesión: {ex.Message}");
        }
        finally
        {
            try
            {
                if (proc is { HasExited: false })
                {
                    proc.Kill(entireProcessTree: true);
                }
            }
            catch { }
            proc?.Dispose();
            BrowserSemaphore.Release();
        }
    }

    private static async Task<bool> RunHeadlessProbeAsync(
        string url,
        string probeScript,
        Func<string, string?, bool> validateAndSave)
    {
        if (!Directory.Exists(WebProfileDir)) return false;
        var browserExe = FindChromiumExecutable();
        if (string.IsNullOrWhiteSpace(browserExe)) return false;

        if (!await BrowserSemaphore.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false))
        {
            return false;
        }

        Process? proc = null;
        try
        {
            int port = GetFreeTcpPort();
            var args = $"--headless=new --disable-gpu --user-data-dir=\"{WebProfileDir}\" --remote-debugging-port={port} --no-first-run --no-default-browser-check --disable-extensions --disable-background-mode --no-service-autorun --disable-blink-features=AutomationControlled \"{url}\"";

            proc = Process.Start(new ProcessStartInfo
            {
                FileName = browserExe,
                Arguments = args,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                UseShellExecute = false
            });

            if (proc == null) return false;

            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
            var deadline = DateTime.UtcNow.AddSeconds(14);

            while (DateTime.UtcNow < deadline && !proc.HasExited)
            {
                await Task.Delay(1100).ConfigureAwait(false);
                var wsUrl = await TryGetPageWebSocketUrlAsync(http, port).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(wsUrl)) continue;

                var (evalResult, cookieHeader) = await EvaluateScriptAndCookiesAsync(wsUrl, probeScript, url).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(evalResult) && validateAndSave(evalResult, cookieHeader))
                {
                    await TryCloseBrowserViaCdpAsync(wsUrl).ConfigureAwait(false);
                    return true;
                }
            }

            return false;
        }
        catch
        {
            return false;
        }
        finally
        {
            try
            {
                if (proc is { HasExited: false })
                {
                    proc.Kill(entireProcessTree: true);
                }
            }
            catch { }
            proc?.Dispose();
            BrowserSemaphore.Release();
        }
    }

    private static async Task<string?> TryGetPageWebSocketUrlAsync(HttpClient http, int port)
    {
        try
        {
            var json = await http.GetStringAsync($"http://127.0.0.1:{port}/json/list").ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return null;

            foreach (var item in doc.RootElement.EnumerateArray())
            {
                var type = item.TryGetProperty("type", out var t) ? t.GetString() : "";
                if (!string.Equals(type, "page", StringComparison.OrdinalIgnoreCase)) continue;

                if (item.TryGetProperty("webSocketDebuggerUrl", out var ws) && ws.ValueKind == JsonValueKind.String)
                {
                    return ws.GetString();
                }
            }
        }
        catch
        {
        }
        return null;
    }

    private static async Task<(string? Result, string? CookieHeader)> EvaluateScriptAndCookiesAsync(string wsUrl, string script, string domainUrl)
    {
        try
        {
            using var ws = new ClientWebSocket();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await ws.ConnectAsync(new Uri(wsUrl), cts.Token).ConfigureAwait(false);

            // 1) Evaluar expresión asíncrona con awaitPromise: true
            var evalCmd = JsonSerializer.Serialize(new
            {
                id = 1,
                method = "Runtime.evaluate",
                @params = new
                {
                    expression = script,
                    awaitPromise = true,
                    returnByValue = true
                }
            });

            var evalRespJson = await SendAndReceiveCdpMessageAsync(ws, 1, evalCmd, cts.Token).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(evalRespJson)) return (null, null);

            string? stringValue = null;
            using (var doc = JsonDocument.Parse(evalRespJson))
            {
                if (doc.RootElement.TryGetProperty("result", out var resObj) &&
                    resObj.TryGetProperty("result", out var innerRes) &&
                    innerRes.TryGetProperty("value", out var val) &&
                    val.ValueKind == JsonValueKind.String)
                {
                    stringValue = val.GetString();
                }
            }

            if (string.IsNullOrWhiteSpace(stringValue))
            {
                return (null, null);
            }

            // 2) Extraer cookies del dominio para respaldo en peticiones directas
            string? cookieHeader = null;
            var cookieCmd = JsonSerializer.Serialize(new
            {
                id = 2,
                method = "Network.getCookies",
                @params = new
                {
                    urls = new[] { domainUrl }
                }
            });

            var cookieRespJson = await SendAndReceiveCdpMessageAsync(ws, 2, cookieCmd, cts.Token).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(cookieRespJson))
            {
                using var cDoc = JsonDocument.Parse(cookieRespJson);
                if (cDoc.RootElement.TryGetProperty("result", out var cRes) &&
                    cRes.TryGetProperty("cookies", out var cookiesArr) &&
                    cookiesArr.ValueKind == JsonValueKind.Array)
                {
                    var pairs = new List<string>();
                    foreach (var c in cookiesArr.EnumerateArray())
                    {
                        var name = c.TryGetProperty("name", out var n) ? n.GetString() : null;
                        var val = c.TryGetProperty("value", out var v) ? v.GetString() : null;
                        if (!string.IsNullOrWhiteSpace(name) && val != null)
                        {
                            pairs.Add($"{name}={val}");
                        }
                    }
                    if (pairs.Count > 0)
                    {
                        cookieHeader = string.Join("; ", pairs);
                    }
                }
            }

            return (stringValue, cookieHeader);
        }
        catch
        {
            return (null, null);
        }
    }

    private static async Task TryCloseBrowserViaCdpAsync(string wsUrl)
    {
        try
        {
            using var ws = new ClientWebSocket();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await ws.ConnectAsync(new Uri(wsUrl), cts.Token).ConfigureAwait(false);
            var closeCmd = "{\"id\":99,\"method\":\"Browser.close\"}";
            var bytes = Encoding.UTF8.GetBytes(closeCmd);
            await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cts.Token).ConfigureAwait(false);
        }
        catch
        {
        }
    }

    private static async Task<string?> SendAndReceiveCdpMessageAsync(ClientWebSocket ws, int expectedId, string payload, CancellationToken ct)
    {
        var sendBytes = Encoding.UTF8.GetBytes(payload);
        await ws.SendAsync(new ArraySegment<byte>(sendBytes), WebSocketMessageType.Text, true, ct).ConfigureAwait(false);

        var buffer = new byte[32768];
        while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
        {
            using var ms = new MemoryStream();
            WebSocketReceiveResult result;
            do
            {
                result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct).ConfigureAwait(false);
                if (result.MessageType == WebSocketMessageType.Close) return null;
                ms.Write(buffer, 0, result.Count);
            } while (!result.EndOfMessage);

            var msg = Encoding.UTF8.GetString(ms.ToArray());
            using var doc = JsonDocument.Parse(msg);
            if (doc.RootElement.TryGetProperty("id", out var idEl) && idEl.TryGetInt32(out var id) && id == expectedId)
            {
                return msg;
            }
        }
        return null;
    }

    public static async Task<(bool Ok, string Message)> InteractiveGeminiOAuthAsync(HttpClient httpClient)
    {
        try
        {
            int port = GetFreeTcpPort();
            var redirectUri = $"http://127.0.0.1:{port}";
            var state = Guid.NewGuid().ToString("N");
            var scope = Uri.EscapeDataString("https://www.googleapis.com/auth/cloud-platform https://www.googleapis.com/auth/userinfo.email https://www.googleapis.com/auth/userinfo.profile");

            var authUrl =
                $"https://accounts.google.com/o/oauth2/v2/auth?client_id={Uri.EscapeDataString(GeminiOAuthClientId)}" +
                $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                $"&response_type=code" +
                $"&scope={scope}" +
                $"&access_type=offline" +
                $"&prompt=consent" +
                $"&state={state}";

            using var listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();

            Process.Start(new ProcessStartInfo(authUrl) { UseShellExecute = true });

            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
            using var reg = cts.Token.Register(() => { try { listener.Stop(); } catch { } });

            using var client = await listener.AcceptTcpClientAsync(cts.Token).ConfigureAwait(false);
            using var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII, false, 4096, leaveOpen: true);

            var requestLine = await reader.ReadLineAsync().ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(requestLine))
            {
                return (false, "Respuesta OAuth vacía");
            }

            var parts = requestLine.Split(' ');
            var pathAndQuery = parts.Length > 1 ? parts[1] : "/";
            string? code = null;

            var qIdx = pathAndQuery.IndexOf('?');
            if (qIdx >= 0)
            {
                var query = pathAndQuery.Substring(qIdx + 1);
                foreach (var kv in query.Split('&'))
                {
                    var pair = kv.Split('=', 2);
                    if (pair.Length == 2 && string.Equals(pair[0], "code", StringComparison.OrdinalIgnoreCase))
                    {
                        code = Uri.UnescapeDataString(pair[1]);
                    }
                }
            }

            var html = string.IsNullOrWhiteSpace(code)
                ? "<html><body style='font-family:Segoe UI,sans-serif;background:#12161F;color:#fff;text-align:center;padding-top:60px;'><h2>No se pudo completar el inicio de sesion</h2><p>Puedes cerrar esta pestana.</p></body></html>"
                : "<html><body style='font-family:Segoe UI,sans-serif;background:#12161F;color:#fff;text-align:center;padding-top:60px;'><h2>Conectado con DockBar</h2><p>Tu cuota real de Google Gemini (5HR y Week) ya esta vinculada. Puedes cerrar esta pestana.</p><script>setTimeout(()=>window.close(),1500);</script></body></html>";

            var bodyBytes = Encoding.UTF8.GetBytes(html);
            var headerBytes = Encoding.ASCII.GetBytes(
                $"HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: {bodyBytes.Length}\r\nConnection: close\r\n\r\n");
            await stream.WriteAsync(headerBytes, cts.Token).ConfigureAwait(false);
            await stream.WriteAsync(bodyBytes, cts.Token).ConfigureAwait(false);
            await stream.FlushAsync(cts.Token).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(code))
            {
                return (false, "No se recibió código de autorización de Google");
            }

            using var tokenReq = new HttpRequestMessage(HttpMethod.Post, "https://oauth2.googleapis.com/token")
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["client_id"] = GeminiOAuthClientId,
                    ["client_secret"] = GeminiOAuthClientSecret,
                    ["code"] = code,
                    ["grant_type"] = "authorization_code",
                    ["redirect_uri"] = redirectUri
                })
            };

            using var tokenResp = await httpClient.SendAsync(tokenReq, cts.Token).ConfigureAwait(false);
            if (!tokenResp.IsSuccessStatusCode)
            {
                return (false, $"Error al canjear OAuth (HTTP {(int)tokenResp.StatusCode})");
            }

            var tokenJson = await tokenResp.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(tokenJson);
            var root = doc.RootElement;

            var accessToken = root.TryGetProperty("access_token", out var at) ? at.GetString() : null;
            var refreshToken = root.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : null;
            var expiresIn = root.TryGetProperty("expires_in", out var ei) && ei.TryGetInt64(out var sec) ? sec : 3500;

            if (string.IsNullOrWhiteSpace(accessToken))
            {
                return (false, "Google OAuth no devolvió access_token");
            }

            SaveSession("Gemini", s =>
            {
                s.AccessToken = accessToken;
                if (!string.IsNullOrWhiteSpace(refreshToken))
                {
                    s.RefreshToken = refreshToken;
                }
                s.ExpiresAtUnixMs = DateTimeOffset.UtcNow.AddSeconds(expiresIn - 60).ToUnixTimeMilliseconds();
            });

            return (true, "Conectado (Sesión Web Google)");
        }
        catch (Exception ex)
        {
            return (false, $"Error OAuth Google: {ex.Message}");
        }
    }

    public static async Task<string?> TryRefreshGoogleAccessTokenAsync(string refreshToken, HttpClient httpClient)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "https://oauth2.googleapis.com/token")
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["client_id"] = GeminiOAuthClientId,
                    ["client_secret"] = GeminiOAuthClientSecret,
                    ["refresh_token"] = refreshToken,
                    ["grant_type"] = "refresh_token"
                })
            };

            using var resp = await httpClient.SendAsync(req).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode) return null;

            var json = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("access_token", out var at))
            {
                var newTok = at.GetString();
                var expiresIn = doc.RootElement.TryGetProperty("expires_in", out var ei) && ei.TryGetInt64(out var sec) ? sec : 3500;
                if (!string.IsNullOrWhiteSpace(newTok))
                {
                    SaveSession("Gemini", s =>
                    {
                        s.AccessToken = newTok;
                        s. RefreshToken ??= refreshToken;
                        s.ExpiresAtUnixMs = DateTimeOffset.UtcNow.AddSeconds(expiresIn - 60).ToUnixTimeMilliseconds();
                    });
                    return newTok;
                }
            }
        }
        catch
        {
        }
        return null;
    }

    private static string? FindChromiumExecutable()
    {
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft", "Edge", "Application", "msedge.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft", "Edge", "Application", "msedge.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Google", "Chrome", "Application", "chrome.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Google", "Chrome", "Application", "chrome.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Google", "Chrome", "Application", "chrome.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "BraveSoftware", "Brave-Browser", "Application", "brave.exe")
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    private static int GetFreeTcpPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}

