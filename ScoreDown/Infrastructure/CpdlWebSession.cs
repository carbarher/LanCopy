using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using WpfApp = System.Windows.Application;

namespace ScoreDown.Infrastructure;

/// <summary>
/// Maintains a hidden WebView2 window that shares the same cookie/TLS profile used
/// to pass the Cloudflare challenge. All CPDL HTTP fetches are routed through this
/// WebView2 so the TLS fingerprint matches the browser session that solved the challenge.
/// </summary>
public sealed class CpdlWebSession : IDisposable
{
    private Window? _hiddenWindow;
    private WebView2? _webView;
    private bool _initialized;
    private int _disposed;
    // Serialise navigations — only one at a time per WebView2 control.
    private readonly SemaphoreSlim _navLock = new(1, 1);

    public async Task<string?> FetchAsync(string url, CancellationToken ct)
    {
        if (_disposed != 0) return null;

        // WebView2 must run on the WPF dispatcher.
        return await WpfApp.Current.Dispatcher.InvokeAsync<Task<string?>>(async () =>
    {
        try
        {
            await EnsureInitializedAsync();
            ct.ThrowIfCancellationRequested();

            await _navLock.WaitAsync(ct);
            try
            {
                return await NavigateAndExtractAsync(url, ct);
            }
            finally
            {
                _navLock.Release();
            }
        }
        catch (OperationCanceledException) { return null; }
        catch { return null; }
    }).Task.Unwrap();
    }

    /// <summary>
    /// Descarga un archivo binario navegando el WebView2 a la URL. IMSLP interstitials (bot-check,
    /// wait-page) se manejan nativamente; el PDF llega vía evento DownloadStarting del motor.
    /// </summary>
    public async Task<(bool success, string? error)> DownloadFileAsync(
        string fileUrl,
        string outputPath,
        CancellationToken ct)
    {
        if (_disposed != 0)
            return (false, "WebView2 session disposed");

        return await WpfApp.Current.Dispatcher.InvokeAsync<Task<(bool, string?)>>(async () =>
        {
            try
            {
                await EnsureInitializedAsync();
                ct.ThrowIfCancellationRequested();

                await _navLock.WaitAsync(ct);
                try
                {
                    return await DownloadViaNavigationAsync(fileUrl, outputPath, ct);
                }
                finally
                {
                    _navLock.Release();
                }
            }
            catch (OperationCanceledException)
            {
                return (false, "cancelled");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }).Task.Unwrap();
    }

    private async Task<(bool, string?)> DownloadViaNavigationAsync(string fileUrl, string outputPath, CancellationToken ct)
    {
        fileUrl = NormalizeImlspToHttps(fileUrl);

        // Hasta 2 intentos: normal, y tras resolver bot-check
        for (int pass = 0; pass < 2; pass++)
        {
            var result = await TrySingleDownloadPassAsync(fileUrl, outputPath, ct);
            if (result.success || result.error != "bot-check")
                return result;

            // Bot-check: ventana visible para que el usuario resuelva MTCaptcha
            await ShowForBotCheckAsync(fileUrl, ct);
        }
        return (false, "bot-check unresolved");
    }

    private async Task<(bool success, string? error)> TrySingleDownloadPassAsync(
        string fileUrl, string outputPath, CancellationToken ct)
    {
        var tcs = new TaskCompletionSource<(bool, string?)>(TaskCreationOptions.RunContinuationsAsynchronously);

        void OnDownloadStarting(object? s, CoreWebView2DownloadStartingEventArgs e)
        {
            // Redirigir descarga a nuestro path y suprimir diálogo del navegador
            e.ResultFilePath = outputPath;
            e.Handled = true;
            var op = e.DownloadOperation;
            op.StateChanged += (_, _) =>
            {
                if (op.State == CoreWebView2DownloadState.Completed)
                    tcs.TrySetResult((true, null));
                else if (op.State == CoreWebView2DownloadState.Interrupted)
                    tcs.TrySetResult((false, $"download-interrupted: {op.InterruptReason}"));
            };
            if (op.State == CoreWebView2DownloadState.Completed)
                tcs.TrySetResult((true, null));
        }

        _webView!.CoreWebView2.DownloadStarting += OnDownloadStarting;
        _webView.Source = new Uri(fileUrl);
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromMinutes(4));
            timeoutCts.Token.Register(() => tcs.TrySetCanceled());

            // Monitorear bot-check en background (mismo dispatcher)
            _ = MonitorBotCheckAsync(tcs, timeoutCts.Token);

            return await tcs.Task;
        }
        catch (OperationCanceledException) { return (false, "timeout"); }
        finally
        {
            _webView.CoreWebView2.DownloadStarting -= OnDownloadStarting;
        }
    }

    /// <summary>
    /// Sondea el título de la página activa. Si detecta "Bot Check" lo reporta via TCS
    /// para que el llamador muestre la ventana y el usuario resuelva el CAPTCHA.
    /// </summary>
    private async Task MonitorBotCheckAsync(
        TaskCompletionSource<(bool, string?)> tcs, CancellationToken ct)
    {
        try
        {
            await Task.Delay(2500, ct); // esperar que la navegación se asiente
            while (!ct.IsCancellationRequested && !tcs.Task.IsCompleted)
            {
                var titleJson = await _webView!.CoreWebView2.ExecuteScriptAsync("document.title");
                var title = System.Text.Json.JsonSerializer.Deserialize<string>(titleJson) ?? "";
                if (title.Contains("Bot Check", StringComparison.OrdinalIgnoreCase))
                {
                    tcs.TrySetResult((false, "bot-check"));
                    return;
                }
                await Task.Delay(2000, ct);
            }
        }
        catch (OperationCanceledException) { }
        catch { } // errores de monitoreo no deben matar el flujo principal
    }

    private async Task EnsureInitializedAsync()
    {
        if (_initialized) return;

        _webView = new WebView2();
        _hiddenWindow = new Window
        {
            Width = 1,
            Height = 1,
            Left = -32000,
            Top = -32000,
            WindowStyle = WindowStyle.None,
            ShowInTaskbar = false,
            ShowActivated = false,
            Opacity = 0,
            Content = _webView
        };
        _hiddenWindow.Show();

        await _webView.EnsureCoreWebView2Async();
        // Suplantar UA de Chrome — igual que en CpdlSessionDialog.
        const string chromeUA = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36";
        _webView.CoreWebView2.Settings.UserAgent = chromeUA;
        // Silenciar errores de WebView2 en barra de tareas/devtools.
        _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
        _webView.CoreWebView2.Settings.AreDevToolsEnabled = false;

        _initialized = true;
    }

    private async Task<string?> NavigateAndExtractAsync(string url, CancellationToken ct)
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        void OnCompleted(object? s, CoreWebView2NavigationCompletedEventArgs e) =>
            tcs.TrySetResult(e.IsSuccess);

        _webView!.CoreWebView2.NavigationCompleted += OnCompleted;
        try
        {
            _webView.Source = new Uri(url);

            using var reg = ct.Register(() => tcs.TrySetCanceled());
            var success = await tcs.Task;
            if (!success) return null;

            // outerHTML devuelve el HTML completo como JSON-string.
            var json = await _webView.CoreWebView2.ExecuteScriptAsync("document.documentElement.outerHTML");
            return JsonSerializer.Deserialize<string>(json);
        }
        finally
        {
            _webView.CoreWebView2.NavigationCompleted -= OnCompleted;
        }
    }

    private async Task<(bool success, string? error)> FetchBinaryViaJsAsync_UNUSED(string url, string outputPath, CancellationToken ct, int depth = 0)
    {
        url = NormalizeImlspToHttps(url);
        var escapedUrl = url.Replace("\\", "\\\\").Replace("'", "\\'");
        var script = """
            (function() {
                try {
                    const xhr = new XMLHttpRequest();
                    xhr.open('GET', '__URL__', false); // sync: ExecuteScriptAsync recibe valor final, no Promise
                    xhr.withCredentials = true;
                    xhr.overrideMimeType('text/plain; charset=x-user-defined');
                    xhr.send(null);

                    if (xhr.status < 200 || xhr.status >= 300)
                        return JSON.stringify({ e: 'HTTP ' + xhr.status });

                    const contentType = (xhr.getResponseHeader('content-type') || '').toLowerCase();
                    if (contentType.includes('text/html')) {
                        const html = xhr.responseText || '';
                        const candidates = [];
                        const addCandidate = (u) => {
                            if (!u || typeof u !== 'string') return;
                            const s = u.trim();
                            if (!s) return;
                            if (candidates.includes(s)) return;
                            candidates.push(s);
                        };

                        // 1) URLs absolutas directas a archivos/handlers.
                        const absRe = /https?:\/\/[^\"'\s<>]+/gi;
                        for (const m of html.matchAll(absRe)) {
                            const u = m[0];
                            if (/(?:imslp\.org|imslp\.eu)/i.test(u)
                                && (u.includes('/images/') || u.includes('/wiki/Special:')))
                                addCandidate(u);
                        }

                        // 2) Atributos href/src/action (incluye rutas relativas).
                        const attrRe = /(?:href|src|action)\s*=\s*[\"']([^\"']+)[\"']/gi;
                        for (const m of html.matchAll(attrRe)) {
                            const u = m[1];
                            if (/\/images\//i.test(u)
                                || /\/wiki\/Special:/i.test(u)
                                || /IMSLPImageHandler/i.test(u)
                                || /ImagefromIndex/i.test(u))
                                addCandidate(u);
                        }

                        // 3) Meta refresh.
                        const metaRefresh = html.match(/<meta[^>]*http-equiv=[\"']?refresh[\"']?[^>]*content=[\"'][^\"']*url=([^\"'>]+)/i);
                        if (metaRefresh && metaRefresh[1])
                            addCandidate(metaRefresh[1]);

                        if (candidates.length > 0)
                            return JSON.stringify({ r: candidates[0] });

                        const titleMatch = html.match(/<title[^>]*>([^<]{0,200})<\/title>/i);
                        return JSON.stringify({ e: 'html-recovery-missed', t: titleMatch ? titleMatch[1] : '' });
                    }

                    const text = xhr.responseText || '';
                    if (text.length === 0)
                        return JSON.stringify({ e: 'empty' });

                    // x-user-defined: cada charCode conserva byte en low 8 bits.
                    let binary = '';
                    const chunk = 0x8000;
                    for (let i = 0; i < text.length; i += chunk) {
                        let part = '';
                        const end = Math.min(i + chunk, text.length);
                        for (let j = i; j < end; j++) {
                            part += String.fromCharCode(text.charCodeAt(j) & 0xFF);
                        }
                        binary += part;
                    }

                    return JSON.stringify({ d: btoa(binary), n: text.length });
                } catch (err) {
                    return JSON.stringify({ e: String(err) });
                }
            })()
            """.Replace("__URL__", escapedUrl);

        var json = await _webView!.CoreWebView2.ExecuteScriptAsync(script);
        using var outer = JsonDocument.Parse(json);

        JsonElement root;
        JsonDocument? innerDoc = null;
        try
        {
            if (outer.RootElement.ValueKind == JsonValueKind.String)
            {
                var payload = outer.RootElement.GetString();
                if (string.IsNullOrWhiteSpace(payload))
                    return (false, $"empty JS payload: {json}");

                innerDoc = JsonDocument.Parse(payload);
                root = innerDoc.RootElement;
            }
            else if (outer.RootElement.ValueKind == JsonValueKind.Object)
            {
                root = outer.RootElement;
            }
            else
            {
                return (false, $"unexpected JS kind: {outer.RootElement.ValueKind} raw={json}");
            }

            if (root.TryGetProperty("e", out var errProp))
            {
                var err = errProp.GetString() ?? "unknown JS error";
                if (root.TryGetProperty("t", out var titleProp))
                {
                    var title = titleProp.GetString();
                    if (!string.IsNullOrWhiteSpace(title))
                        err += $" (title: {title})";
                }
                return (false, err);
            }

            if (root.TryGetProperty("r", out var recProp))
            {
                var recovered = recProp.GetString();
                if (!string.IsNullOrWhiteSpace(recovered) && depth < 1)
                {
                    if (Uri.TryCreate(new Uri(url), recovered, out var abs))
                        recovered = abs.ToString();
                    recovered = NormalizeImlspToHttps(recovered);
                    return await FetchBinaryViaJsAsync_UNUSED(recovered, outputPath, ct, depth + 1);
                }
                return (false, "html-recovery-missed");
            }

            if (!root.TryGetProperty("d", out var dataProp))
                return (false, $"unexpected JS response: {json}");

            var base64 = dataProp.GetString();
            if (string.IsNullOrEmpty(base64))
                return (false, "empty response");

            var bytes = Convert.FromBase64String(base64);
            await System.IO.File.WriteAllBytesAsync(outputPath, bytes, ct);
            return (true, null);
        }
        finally
        {
            innerDoc?.Dispose();
        }
    }

    /// <summary>
    /// Hace visible el WebView2 oculto para que el usuario resuelva el bot-check de IMSLP (MTCaptcha).
    /// Espera hasta que la cookie BOT_DETECT_CLEARED aparezca o se agote el tiempo límite.
    /// </summary>
    private async Task ShowForBotCheckAsync(string fileUrl, CancellationToken ct)
    {
        // Mostrar ventana al usuario
        _hiddenWindow!.Width = 820;
        _hiddenWindow.Height = 640;
        _hiddenWindow.Left = 120;
        _hiddenWindow.Top = 120;
        _hiddenWindow.Opacity = 1;
        _hiddenWindow.WindowStyle = WindowStyle.SingleBorderWindow;
        _hiddenWindow.ShowInTaskbar = true;
        _hiddenWindow.Title = "IMSLP - Resuelve el CAPTCHA para continuar la descarga";

        // Navegar a la URL del archivo (carga la página de bot-check con MTCaptcha)
        var navTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnNav(object? s, CoreWebView2NavigationCompletedEventArgs e) => navTcs.TrySetResult(true);
        _webView!.CoreWebView2.NavigationCompleted += OnNav;
        try
        {
            _webView.Source = new Uri(fileUrl);
            using var reg = ct.Register(() => navTcs.TrySetCanceled());
            await navTcs.Task;
        }
        finally
        {
            _webView.CoreWebView2.NavigationCompleted -= OnNav;
        }
        // Sondear cookie BOT_DETECT_CLEARED hasta 5 minutos
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromMinutes(5));
        try
        {
            while (!timeout.Token.IsCancellationRequested)
            {
                await Task.Delay(1500, timeout.Token);
                var cookies = await _webView.CoreWebView2.CookieManager.GetCookiesAsync("https://imslp.org");
                if (cookies.Any(c => c.Name == "BOT_DETECT_CLEARED"))
                    break;
            }
        }
        catch (OperationCanceledException) { /* timeout o CT externo */ }

        // Volver a ocultar
        _hiddenWindow.Width = 1;
        _hiddenWindow.Height = 1;
        _hiddenWindow.Left = -32000;
        _hiddenWindow.Top = -32000;
        _hiddenWindow.Opacity = 0;
        _hiddenWindow.WindowStyle = WindowStyle.None;
        _hiddenWindow.ShowInTaskbar = false;
    }

    private static string NormalizeImlspToHttps(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return url;

        if (Uri.TryCreate(url, UriKind.Absolute, out var u)
            && string.Equals(u.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            && (u.Host.Contains("imslp.org", StringComparison.OrdinalIgnoreCase)
                || u.Host.Contains("imslp.eu", StringComparison.OrdinalIgnoreCase)))
        {
            var b = new UriBuilder(u) { Scheme = Uri.UriSchemeHttps, Port = -1 };
            return b.Uri.ToString();
        }

        return url;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        WpfApp.Current?.Dispatcher.Invoke(() =>
    {
        try { _hiddenWindow?.Close(); } catch { }
        _hiddenWindow = null;
        _webView = null;
    });
    }
}
