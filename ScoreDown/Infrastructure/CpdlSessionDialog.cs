using Microsoft.Web.WebView2.Wpf;
using Microsoft.Web.WebView2.Core;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using WpfButton = System.Windows.Controls.Button;

namespace ScoreDown.Infrastructure;

public sealed class CpdlSessionDialog : Window
{
    private readonly WebView2 _webView;
    private readonly WpfButton _btnUseSession;
    private readonly TextBlock _txtStatus;
    private readonly string _siteLabel;
    private readonly string _startUrl;
    private readonly IReadOnlyList<string> _cookieScopeUrls;
    private readonly HashSet<string> _requiredCookieNames;

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
        IReadOnlyList<string>? requiredCookieNames = null)
    {
        _siteLabel = string.IsNullOrWhiteSpace(siteLabel) ? "Sitio" : siteLabel.Trim();
        _startUrl = startUrl;
        _cookieScopeUrls = cookieScopeUrls?.Count > 0 ? cookieScopeUrls : [startUrl];
        _requiredCookieNames = new HashSet<string>(requiredCookieNames ?? [], StringComparer.OrdinalIgnoreCase);

        Title = $"{_siteLabel} sesión interactiva";
        Width = 1024;
        Height = 720;
        MinWidth = 860;
        MinHeight = 560;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = new TextBlock
        {
            Margin = new Thickness(10, 8, 10, 6),
            TextWrapping = TextWrapping.Wrap,
            Text = $"1) Completa el challenge/login en {_siteLabel}. 2) Pulsa 'Usar esta sesión'."
        };
        Grid.SetRow(header, 0);
        root.Children.Add(header);

        _webView = new WebView2();
        Grid.SetRow(_webView, 1);
        root.Children.Add(_webView);

        var footer = new DockPanel { LastChildFill = true, Margin = new Thickness(10, 8, 10, 10) };
        _txtStatus = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            Text = $"Abriendo {_siteLabel}..."
        };
        DockPanel.SetDock(_txtStatus, Dock.Left);
        footer.Children.Add(_txtStatus);

        var buttons = new StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right
        };
        var btnCancel = new WpfButton
        {
            Content = "Cancelar",
            Padding = new Thickness(10, 6, 10, 6),
            Margin = new Thickness(0, 0, 6, 0)
        };
        btnCancel.Click += (_, _) => Close();

        _btnUseSession = new WpfButton
        {
            Content = "Usar esta sesión",
            Padding = new Thickness(12, 6, 12, 6),
            IsEnabled = false
        };
        _btnUseSession.Click += BtnUseSession_Click;

        buttons.Children.Add(btnCancel);
        buttons.Children.Add(_btnUseSession);
        DockPanel.SetDock(buttons, Dock.Right);
        footer.Children.Add(buttons);

        Grid.SetRow(footer, 2);
        root.Children.Add(footer);

        Content = root;
        Loaded += OnLoaded;
    }

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
            _txtStatus.Text = "Completa el challenge/login en la web y pulsa 'Usar esta sesión'.";
        }
        catch (Exception ex)
        {
            _txtStatus.Text = $"Error WebView2: {ex.Message}";
            _btnUseSession.IsEnabled = false;
        }
    }

    private async void BtnUseSession_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (_webView.CoreWebView2 is null)
            {
                _txtStatus.Text = "WebView2 no inicializado.";
                return;
            }

            var allCookies = new List<CoreWebView2Cookie>();
            foreach (var scope in _cookieScopeUrls)
            {
                try
                {
                    var scoped = await _webView.CoreWebView2.CookieManager.GetCookiesAsync(scope);
                    allCookies.AddRange(scoped);
                }
                catch
                {
                    // Ignorar scope inválido y continuar con los demás.
                }
            }

            var cookies = allCookies
                .Where(c => !string.IsNullOrWhiteSpace(c.Name) && !string.IsNullOrWhiteSpace(c.Value))
                .GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();

            var pairs = cookies
                .Select(c => $"{c.Name}={c.Value}")
                .ToList();

            if (pairs.Count == 0)
            {
                _txtStatus.Text = "Sin cookies de sesión aún. Completa challenge y reintenta.";
                return;
            }

            if (_requiredCookieNames.Count > 0)
            {
                var names = cookies.Select(c => c.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var hasRequired = _requiredCookieNames.Any(names.Contains);
                if (!hasRequired)
                {
                    _txtStatus.Text = $"Sesión incompleta: falta cookie requerida ({string.Join(", ", _requiredCookieNames)}). Completa challenge y reintenta.";
                    return;
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
    }
}
