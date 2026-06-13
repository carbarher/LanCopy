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
            Foreground = SolidColorBrush.Parse("#CCCCCC"),
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
        Closing += (_, _) => _tcs.TrySetResult(false);
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