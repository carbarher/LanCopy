using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using LanCopy.Localization;

namespace LanCopy;

// Dialogo modal de consentimiento: pregunta al receptor si acepta un fichero entrante.
internal sealed class ConsentDialog : Window
{
    private readonly TaskCompletionSource<bool> _tcs = new();

    public ConsentDialog(string ip, string fileName, long size)
    {
        Title = Loc.Instance["dlg.consent.title"];
        Width = 470;
        Height = 180;
        CanResize = false;
        Background = SolidColorBrush.Parse("#2D2D30");
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Topmost = true;

        var human = HumanSize(size);
        var msg = Loc.Instance.Format("dlg.consent.body", fileName, human, ip);

        var panel = new StackPanel { Margin = new Thickness(20), Spacing = 16 };
        panel.Children.Add(new TextBlock
        {
            Text = msg,
            Foreground = SolidColorBrush.Parse("#E6E6E6"),
            TextWrapping = TextWrapping.Wrap
        });

        var btns = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8
        };

        var btnReject = MakeBtn(Loc.Instance["dlg.consent.reject"], "#C0392B");
        var btnAccept = MakeBtn(Loc.Instance["dlg.consent.accept"], "#2E7D32");

        btnReject.Click += (_, _) => { _tcs.TrySetResult(false); Close(); };
        btnAccept.Click += (_, _) => { _tcs.TrySetResult(true); Close(); };

        btns.Children.Add(btnReject);
        btns.Children.Add(btnAccept);
        panel.Children.Add(btns);
        Content = panel;

        // Por seguridad, cerrar sin elegir = rechazar.
        Closing += (_, _) => { _tcs.TrySetResult(false); _autoRejectCts.Cancel(); _autoRejectCts.Dispose(); }; // B6: dispose CTS para liberar WaitHandle

        // U3: auto-rechazar después de 60s si el usuario no está
        // U2: mostrar countdown al usuario para que sepa cuánto tiempo tiene
        _countdownLabel = new Avalonia.Controls.TextBlock
        {
            Text = $"({AutoRejectSeconds}s)",
            FontSize = 11,
            Opacity = 0.55,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Margin = new Avalonia.Thickness(0, 4, 0, 0)
        };
        panel.Children.Add(_countdownLabel);
        _ = AutoRejectAsync();
    }

    private const int AutoRejectSeconds = 60;
    private Avalonia.Controls.TextBlock? _countdownLabel; // U2
    private readonly System.Threading.CancellationTokenSource _autoRejectCts = new(); // B8

    private async System.Threading.Tasks.Task AutoRejectAsync()
    {
        try
        {
            for (int i = AutoRejectSeconds; i > 0; i--)
            {
                if (_tcs.Task.IsCompleted) return;
                var remaining = i;
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    if (_countdownLabel != null) _countdownLabel.Text = $"({remaining}s)";
                });
                // Q3: capturar también ObjectDisposedException — ocurre si Closing dispone el CTS
            // entre iteraciones del loop (cuando el usuario acepta/rechaza mientras el delay está activo)
            try { await System.Threading.Tasks.Task.Delay(1000, _autoRejectCts.Token); }
            catch (System.OperationCanceledException) { return; }
            catch (ObjectDisposedException) { return; }
            }
            if (_tcs.TrySetResult(false))
                Avalonia.Threading.Dispatcher.UIThread.Post(Close);
        }
        catch { }
    }

    public Task<bool> GetResultAsync() => _tcs.Task;

    private static string HumanSize(long bytes)
    {
        string[] u = { "B", "KB", "MB", "GB", "TB" };
        double s = bytes; int i = 0;
        while (s >= 1024 && i < u.Length - 1) { s /= 1024; i++; }
        return $"{s:0.#} {u[i]}";
    }

    private static Button MakeBtn(string label, string bg) => new()
    {
        Content = label,
        Background = SolidColorBrush.Parse(bg),
        Foreground = Brushes.White,
        Padding = new Thickness(12, 6)
    };
}