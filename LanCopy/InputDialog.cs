using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using LanCopy.Localization;

namespace LanCopy;

/// <summary>
/// Diálogo modal de entrada de texto (p.ej. para renombrar archivos).
/// </summary>
internal sealed class InputDialog : Window
{
    private readonly TaskCompletionSource<string?> _tcs = new();

    public InputDialog(string title, string prompt, string initialValue = "")
    {
        Title = title;
        Width = 420;
        Height = 150;
        CanResize = false;
        Background = SolidColorBrush.Parse("#2D2D30");
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var panel = new StackPanel { Margin = new Thickness(20), Spacing = 12 };

        panel.Children.Add(new TextBlock
        {
            Text = prompt,
            Foreground = SolidColorBrush.Parse("#CCCCCC"),
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12
        });

        var txt = new TextBox
        {
            Text = initialValue,
            Background = SolidColorBrush.Parse("#1E1E1E"),
            Foreground = SolidColorBrush.Parse("#CCCCCC"),
            BorderBrush = SolidColorBrush.Parse("#3F3F46"),
            CaretBrush = SolidColorBrush.Parse("#CCCCCC"),
            Padding = new Thickness(6, 4),
            FontSize = 12,
            SelectionStart = 0,
            SelectionEnd = initialValue.Length
        };

        var btns = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8
        };

        var btnOk = MakeBtn(Loc.Instance["dlg.input.ok"], "#007ACC");
        var btnCancel = MakeBtn(Loc.Instance["dlg.input.cancel"], "#3E3E42");

        btnOk.Click += (_, _) =>
        {
            _tcs.TrySetResult(txt.Text?.Trim());
            Close();
        };
        btnCancel.Click += (_, _) => { _tcs.TrySetResult(null); Close(); };

        // Enter confirma, Escape cancela
        txt.KeyDown += (_, e) =>
        {
            if (e.Key == Avalonia.Input.Key.Enter) { _tcs.TrySetResult(txt.Text?.Trim()); Close(); }
            if (e.Key == Avalonia.Input.Key.Escape) { _tcs.TrySetResult(null); Close(); }
        };

        btns.Children.Add(btnCancel);
        btns.Children.Add(btnOk);

        panel.Children.Add(txt);
        panel.Children.Add(btns);
        Content = panel;

        Opened += (_, _) => txt.Focus();
        Closing += (_, _) => _tcs.TrySetResult(null);
    }

    public Task<string?> GetResultAsync() => _tcs.Task;

    private static Button MakeBtn(string label, string bg) => new()
    {
        Content = label,
        Background = SolidColorBrush.Parse(bg),
        Foreground = Brushes.White,
        Padding = new Thickness(10, 5)
    };
}
