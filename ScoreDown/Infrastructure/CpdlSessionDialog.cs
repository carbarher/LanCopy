using Microsoft.Web.WebView2.Wpf;
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

    public string? CookieHeader { get; private set; }
    public string? UserAgent { get; private set; }

    public CpdlSessionDialog()
    {
        Title = "CPDL sesión interactiva";
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
            Text = "1) Completa el challenge/login en CPDL. 2) Pulsa 'Usar esta sesión'."
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
            Text = "Abriendo CPDL..."
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
            _webView.Source = new Uri("https://www.cpdl.org/wiki/index.php/Main_Page");
            _btnUseSession.IsEnabled = true;
            _txtStatus.Text = "Completa challenge/login en la web y pulsa 'Usar esta sesión'.";
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

            var cookies = await _webView.CoreWebView2.CookieManager.GetCookiesAsync("https://www.cpdl.org/");
            var pairs = cookies
                .Where(c => !string.IsNullOrWhiteSpace(c.Name) && !string.IsNullOrWhiteSpace(c.Value))
                .Select(c => $"{c.Name}={c.Value}")
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (pairs.Count == 0)
            {
                _txtStatus.Text = "Sin cookies de sesión aún. Completa challenge y reintenta.";
                return;
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
