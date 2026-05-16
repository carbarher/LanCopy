using Microsoft.Web.WebView2.Wpf;
using Microsoft.Web.WebView2.Core;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using WpfButton = System.Windows.Controls.Button;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfColor = System.Windows.Media.Color;
using WpfColorConverter = System.Windows.Media.ColorConverter;
using WpfSolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace ScoreDown.Infrastructure;

public sealed class CpdlSessionDialog : Window
{
    private readonly WebView2 _webView;
    private readonly WpfButton _btnUseSession;
    private readonly WpfButton _btnCheckCookies;
    private readonly TextBlock _txtStatus;
    private readonly string _siteLabel;
    private readonly string _startUrl;
    private readonly IReadOnlyList<string> _cookieScopeUrls;
    private readonly HashSet<string> _requiredCookieNames;
    private readonly bool _allowFallbackWithoutRequiredCookies;

    public string? CookieHeader { get; private set; }
    public string? UserAgent { get; private set; }

    public CpdlSessionDialog()
        : this("CPDL", "https://www.cpdl.org/wiki/index.php/Main_Page", ["https://www.cpdl.org/"], ["cf_clearance"])
    {
    }

    public CpdlSessionDialog(
        string siteLabel,
        string startUrl,
        IReadOnlyList<string> cookieScopeUrls,
        IReadOnlyList<string>? requiredCookieNames = null,
        bool allowFallbackWithoutRequiredCookies = true)
    {
        _siteLabel = string.IsNullOrWhiteSpace(siteLabel) ? "Sitio" : siteLabel.Trim();
        _startUrl = startUrl;
        _cookieScopeUrls = cookieScopeUrls?.Count > 0 ? cookieScopeUrls : [startUrl];
        _requiredCookieNames = new HashSet<string>(requiredCookieNames ?? [], StringComparer.OrdinalIgnoreCase);
        _allowFallbackWithoutRequiredCookies = allowFallbackWithoutRequiredCookies;

        Title = $"{_siteLabel} sesión interactiva";
        Width = 1024;
        Height = 720;
        MinWidth = 860;
        MinHeight = 560;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = CreateBrush("#1E1E2E");
        Foreground = WpfBrushes.White;

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = new TextBlock
        {
            Margin = new Thickness(10, 8, 10, 6),
            TextWrapping = TextWrapping.Wrap,
            Text = $"1) Completa el challenge/login en {_siteLabel}. 2) Pulsa 'Usar esta sesión'.",
            Foreground = CreateBrush("#C7D2FE")
        };
        Grid.SetRow(header, 0);
        root.Children.Add(header);

        _webView = new WebView2();
        Grid.SetRow(_webView, 1);
        root.Children.Add(_webView);

        var footer = new DockPanel
        {
            LastChildFill = true,
            Margin = new Thickness(10, 8, 10, 10),
            Background = CreateBrush("#1E1E2E")
        };
        _txtStatus = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            Text = $"Abriendo {_siteLabel}...",
            Foreground = CreateBrush("#94A3B8")
        };
        DockPanel.SetDock(_txtStatus, Dock.Left);
        footer.Children.Add(_txtStatus);

        var buttons = new StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right
        };
        _btnCheckCookies = new WpfButton
        {
            Content = "Diagnóstico cookies",
            Padding = new Thickness(10, 6, 10, 6),
            Margin = new Thickness(0, 0, 6, 0),
            IsEnabled = false,
            Background = CreateBrush("#2D2D44"),
            Foreground = WpfBrushes.White,
            BorderBrush = CreateBrush("#555555"),
            BorderThickness = new Thickness(1)
        };
        _btnCheckCookies.Click += BtnCheckCookies_Click;

        var btnCancel = new WpfButton
        {
            Content = "Cancelar",
            Padding = new Thickness(10, 6, 10, 6),
            Margin = new Thickness(0, 0, 6, 0),
            Background = CreateBrush("#374151"),
            Foreground = WpfBrushes.White,
            BorderBrush = CreateBrush("#555555"),
            BorderThickness = new Thickness(1)
        };
        btnCancel.Click += (_, _) => Close();

        _btnUseSession = new WpfButton
        {
            Content = "Usar esta sesión",
            Padding = new Thickness(12, 6, 12, 6),
            IsEnabled = false,
            Background = CreateBrush("#7C3AED"),
            Foreground = WpfBrushes.White,
            BorderBrush = CreateBrush("#555555"),
            BorderThickness = new Thickness(1)
        };
        _btnUseSession.Click += BtnUseSession_Click;

        buttons.Children.Add(_btnCheckCookies);
        buttons.Children.Add(btnCancel);
        buttons.Children.Add(_btnUseSession);
        DockPanel.SetDock(buttons, Dock.Right);
        footer.Children.Add(buttons);

        Grid.SetRow(footer, 2);
        root.Children.Add(footer);

        Content = root;
        Loaded += OnLoaded;
    }

    private static WpfSolidColorBrush CreateBrush(string hex)
        => new((WpfColor)WpfColorConverter.ConvertFromString(hex)!);

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await _webView.EnsureCoreWebView2Async();

            // Suplantar UA de Chrome para evitar detección de WebView2 por Cloudflare
            const string chromeUA = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36";
            _webView.CoreWebView2.Settings.UserAgent = chromeUA;

            _webView.Source = new Uri(_startUrl);
            _btnUseSession.IsEnabled = true;
            _btnCheckCookies.IsEnabled = true;
            _txtStatus.Text = "Completa el challenge/login en la web y pulsa 'Usar esta sesión'.";
        }
        catch (Exception ex)
        {
            _txtStatus.Text = $"Error WebView2: {ex.Message}";
            _btnUseSession.IsEnabled = false;
            _btnCheckCookies.IsEnabled = false;
        }
    }

    private async void BtnUseSession_Click(object? sender, RoutedEventArgs e)
    {
        _btnUseSession.IsEnabled = false;
        _txtStatus.Text = "Leyendo cookies de sesión...";
        try
        {
            if (_webView.CoreWebView2 is null)
            {
                _txtStatus.Text = "WebView2 no inicializado.";
                return;
            }

            var (cookiePairs, cookiesFromManagerFailed, cookieManagerError) =
                await CollectCookiesAsync();

            var pairs = cookiePairs
                .Select(kv => $"{kv.Key}={kv.Value}")
                .ToList();

            if (pairs.Count == 0)
            {
                _txtStatus.Text = cookiesFromManagerFailed && !string.IsNullOrWhiteSpace(cookieManagerError)
                    ? $"Sin cookies de sesión (WebView2): {cookieManagerError}"
                    : "Sin cookies de sesión aún. Completa challenge y reintenta.";
                return;
            }

            if (_requiredCookieNames.Count > 0)
            {
                var names = cookiePairs.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
                var hasRequired = _requiredCookieNames.Any(names.Contains);
                if (!hasRequired)
                {
                    var title = await TryGetDocumentTitleAsync().WaitAsync(TimeSpan.FromSeconds(2));
                    if (!_allowFallbackWithoutRequiredCookies || IsLikelyChallengeTitle(title))
                    {
                        var requiredState = BuildRequiredCookieState(cookiePairs);
                        _txtStatus.Text = $"Sesión incompleta: falta cookie requerida ({requiredState}). Completa challenge y reintenta.";
                        return;
                    }

                    // Si no estamos en challenge y sí hay cookies, permitir continuar.
                    if (cookiePairs.Count == 0)
                    {
                        _txtStatus.Text = $"Sesión incompleta: no se pudieron leer cookies válidas (title: {title}).";
                        return;
                    }
                }
            }

            CookieHeader = string.Join("; ", pairs);

            var uaJson = await _webView.CoreWebView2.ExecuteScriptAsync("navigator.userAgent");
            try
            {
                UserAgent = JsonSerializer.Deserialize<string>(uaJson);
            }
            catch
            {
                UserAgent = null;
            }

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            _txtStatus.Text = $"No se pudo leer sesión: {ex.Message}";
        }
        finally
        {
            _btnUseSession.IsEnabled = true;
        }
    }

    private async void BtnCheckCookies_Click(object? sender, RoutedEventArgs e)
    {
        _btnCheckCookies.IsEnabled = false;
        try
        {
            if (_webView.CoreWebView2 is null)
            {
                _txtStatus.Text = "WebView2 no inicializado.";
                return;
            }

            var (cookiePairs, _, _) = await CollectCookiesAsync();
            var title = await TryGetDocumentTitleAsync().WaitAsync(TimeSpan.FromSeconds(2));
            var requiredState = BuildRequiredCookieState(cookiePairs);
            var hasRequired = _requiredCookieNames.Count == 0
                || _requiredCookieNames.Any(req => cookiePairs.ContainsKey(req));
            var inChallenge = IsLikelyChallengeTitle(title);

            if (hasRequired && !inChallenge)
                _txtStatus.Text = $"Sesion lista. Cookies: {cookiePairs.Count}. Requeridas: {requiredState}. Titulo: {title}. Pulsa 'Usar esta sesion'.";
            else if (!hasRequired && inChallenge)
                _txtStatus.Text = $"Sesion incompleta (challenge). Cookies: {cookiePairs.Count}. Requeridas: {requiredState}. Titulo: {title}.";
            else
                _txtStatus.Text = $"Cookies: {cookiePairs.Count}. Requeridas: {requiredState}. Titulo: {title}";
        }
        catch (Exception ex)
        {
            _txtStatus.Text = $"Error diagnóstico cookies: {ex.Message}";
        }
        finally
        {
            _btnCheckCookies.IsEnabled = true;
        }
    }

    private async Task<(Dictionary<string, string> cookies, bool managerFailed, string? managerError)> CollectCookiesAsync()
    {
        var allCookies = new List<CoreWebView2Cookie>();
        var cookiesFromManagerFailed = false;
        string? cookieManagerError = null;
        foreach (var scope in _cookieScopeUrls)
        {
            try
            {
                var scoped = await _webView.CoreWebView2!.CookieManager
                    .GetCookiesAsync(scope)
                    .WaitAsync(TimeSpan.FromSeconds(8));
                allCookies.AddRange(scoped);
            }
            catch (Exception ex)
            {
                // Algunos runtimes WebView2 viejos fallan al materializar CoreWebView2Cookie.
                // Seguimos con fallback por DevTools para mantener compatibilidad.
                cookiesFromManagerFailed = true;
                cookieManagerError = ex.Message;
            }
        }

        var cookiePairs = allCookies
            .Where(c => !string.IsNullOrWhiteSpace(c.Name) && !string.IsNullOrWhiteSpace(c.Value))
            .GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Value, StringComparer.OrdinalIgnoreCase);

        var devToolsCookies = await TryGetCookiesViaDevToolsAsync(_cookieScopeUrls).WaitAsync(TimeSpan.FromSeconds(8));
        foreach (var (name, value) in devToolsCookies)
            cookiePairs[name] = value;

        // Fallback extra: cookies no-HttpOnly visibles en document.cookie.
        var documentCookies = await TryGetCookiesFromDocumentAsync().WaitAsync(TimeSpan.FromSeconds(3));
        foreach (var (name, value) in documentCookies)
            cookiePairs[name] = value;

        return (cookiePairs, cookiesFromManagerFailed, cookieManagerError);
    }

    private string BuildRequiredCookieState(IReadOnlyDictionary<string, string> cookiePairs)
    {
        if (_requiredCookieNames.Count == 0)
            return "(sin requeridas)";

        var found = new List<string>();
        var missing = new List<string>();
        foreach (var req in _requiredCookieNames)
        {
            if (cookiePairs.ContainsKey(req)) found.Add(req);
            else missing.Add(req);
        }

        return $"ok:[{string.Join(", ", found)}] faltan:[{string.Join(", ", missing)}]";
    }

    private async Task<List<(string Name, string Value)>> TryGetCookiesFromDocumentAsync()
    {
        var result = new List<(string Name, string Value)>();
        if (_webView.CoreWebView2 is null)
            return result;

        try
        {
            var cookieJson = await _webView.CoreWebView2.ExecuteScriptAsync("document.cookie");
            var cookieStr = JsonSerializer.Deserialize<string>(cookieJson) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(cookieStr))
                return result;

            var segments = cookieStr.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var segment in segments)
            {
                var idx = segment.IndexOf('=');
                if (idx <= 0)
                    continue;

                var name = segment[..idx].Trim();
                var value = segment[(idx + 1)..].Trim();
                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(value))
                    continue;

                result.Add((name, value));
            }
        }
        catch
        {
            // Best-effort.
        }

        return result;
    }

    private async Task<string> TryGetDocumentTitleAsync()
    {
        try
        {
            if (_webView.CoreWebView2 is null)
                return string.Empty;

            var titleJson = await _webView.CoreWebView2.ExecuteScriptAsync("document.title");
            return JsonSerializer.Deserialize<string>(titleJson) ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static bool IsLikelyChallengeTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return true;

        return title.Contains("Bot Check", StringComparison.OrdinalIgnoreCase)
            || title.Contains("Just a moment", StringComparison.OrdinalIgnoreCase)
            || title.Contains("Attention Required", StringComparison.OrdinalIgnoreCase)
            || title.Contains("Access denied", StringComparison.OrdinalIgnoreCase)
            || title.Contains("Checking your browser", StringComparison.OrdinalIgnoreCase)
            || title.Contains("Captcha", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<List<(string Name, string Value)>> TryGetCookiesViaDevToolsAsync(IEnumerable<string> scopeUrls)
    {
        var result = new List<(string Name, string Value)>();
        if (_webView.CoreWebView2 is null)
            return result;

        try
        {
            var urls = scopeUrls
                .Where(u => Uri.TryCreate(u, UriKind.Absolute, out _))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (urls.Length == 0)
                return result;

            var payload = JsonSerializer.Serialize(new { urls });
            var json = await _webView.CoreWebView2.CallDevToolsProtocolMethodAsync("Network.getCookies", payload);
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("cookies", out var cookies) || cookies.ValueKind != JsonValueKind.Array)
                return result;

            foreach (var cookie in cookies.EnumerateArray())
            {
                if (!cookie.TryGetProperty("name", out var nameProp) || !cookie.TryGetProperty("value", out var valueProp))
                    continue;

                var name = nameProp.GetString();
                var value = valueProp.GetString();
                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(value))
                    continue;

                result.Add((name, value));
            }
        }
        catch
        {
            // Fallback best-effort.
        }

        return result;
    }
}
