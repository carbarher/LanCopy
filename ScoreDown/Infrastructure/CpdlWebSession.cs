using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
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
    // Cookies to inject on first WebView2 init (e.g. from CpdlSessionDialog).
    private string? _pendingCookieHeader;
    private string? _pendingCookieDomain;

    /// <summary>
    /// Almacena cookies (formato "name=value; name2=value2") para inyectarlas en el WebView2
    /// la primera vez que se inicialice. Debe llamarse antes del primer uso.
    /// </summary>
    public void SetCookiesForInjection(string cookieHeader, string domain)
    {
        _pendingCookieHeader = cookieHeader;
        _pendingCookieDomain = domain;
    }

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

        // Inyectar cookies de sesión capturadas en CpdlSessionDialog.
        if (!string.IsNullOrWhiteSpace(_pendingCookieHeader) && !string.IsNullOrWhiteSpace(_pendingCookieDomain))
        {
            try
            {
                var manager = _webView.CoreWebView2.CookieManager;
                foreach (var segment in _pendingCookieHeader.Split(';'))
                {
                    var idx = segment.IndexOf('=');
                    if (idx <= 0) continue;
                    var name = segment[..idx].Trim();
                    var value = segment[(idx + 1)..].Trim();
                    if (string.IsNullOrEmpty(name)) continue;
                    // Inyectar en dominio apex Y con punto (cubre MediaWiki + Cloudflare).
                    foreach (var cookieDomain in new[] { _pendingCookieDomain, "." + _pendingCookieDomain })
                    {
                        var cookie = manager.CreateCookie(name, value, cookieDomain, "/");
                        cookie.IsSecure = true;
                        manager.AddOrUpdateCookie(cookie);
                    }
                }
            }
            catch { /* inyección de cookies es best-effort */ }
            _pendingCookieHeader = null;
            _pendingCookieDomain = null;
        }
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
