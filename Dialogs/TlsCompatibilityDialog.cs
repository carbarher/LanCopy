using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using LanCopy.Localization;

namespace LanCopy;

internal sealed class TlsCompatibilityDialog : Window
{
    private readonly TaskCompletionSource<bool> _tcs = new();

    public TlsCompatibilityDialog(string peer)
    {
        Title = Loc.Instance["dlg.tlsCompatibility.title"];
        Width = 520;
        Height = 205;
        CanResize = false;
        Background = SolidColorBrush.Parse("#2D2D30");
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Topmost = true;

        var panel = new StackPanel { Margin = new Thickness(20), Spacing = 16 };
        panel.Children.Add(new TextBlock
        {
            Text = Loc.Instance.Format("dlg.tlsCompatibility.body", peer),
            Foreground = SolidColorBrush.Parse("#E6E6E6"),
            TextWrapping = TextWrapping.Wrap
        });

        var btns = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8
        };

        var btnCancel = MakeBtn(Loc.Instance["dlg.tlsCompatibility.cancel"], "#3E3E42");
        var btnContinue = MakeBtn(Loc.Instance["dlg.tlsCompatibility.continue"], "#B85C00");

        btnCancel.Click += (_, _) => { _tcs.TrySetResult(false); Close(false); };
        btnContinue.Click += (_, _) => { _tcs.TrySetResult(true); Close(true); };

        btns.Children.Add(btnCancel);
        btns.Children.Add(btnContinue);
        panel.Children.Add(btns);
        Content = panel;

        Closing += (_, _) => _tcs.TrySetResult(false);
    }

    public Task<bool> GetResultAsync() => _tcs.Task;

    private static Button MakeBtn(string label, string bg) => new()
    {
        Content = label,
        Background = SolidColorBrush.Parse(bg),
        Foreground = Brushes.White,
        Padding = new Thickness(12, 6)
    };
}