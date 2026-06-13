using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using LanCopy.Localization;

namespace LanCopy;

// Diálogo modal de confirmación de sobreescritura (#10)
internal sealed class ConfirmDialog : Window
{
    public enum OverwriteAction { OverwriteAll, SkipAll, Rename, Cancel }

    private readonly TaskCompletionSource<OverwriteAction> _tcs = new();

    public ConfirmDialog(int count, string firstName)
    {
        Title = Loc.Instance["dlg.overwrite.title"];
        Width = 470;
        Height = 160;
        CanResize = false;
        Background = SolidColorBrush.Parse("#2D2D30");
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var msg = count == 1
            ? Loc.Instance.Format("dlg.overwrite.one", firstName)
            : Loc.Instance.Format("dlg.overwrite.many", count);

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

        var btnCancel    = MakeBtn(Loc.Instance["dlg.overwrite.cancel"], "#3E3E42");
        var btnSkip      = MakeBtn(Loc.Instance["dlg.overwrite.skip"], "#555555");
        var btnRename    = MakeBtn(Loc.Instance["dlg.overwrite.rename"], "#007ACC");
        var btnOverwrite = MakeBtn(Loc.Instance["dlg.overwrite.overwrite"], "#C0392B");

        btnCancel.Click    += (_, _) => { _tcs.TrySetResult(OverwriteAction.Cancel); Close(); };
        btnSkip.Click      += (_, _) => { _tcs.TrySetResult(OverwriteAction.SkipAll); Close(); };
        btnRename.Click    += (_, _) => { _tcs.TrySetResult(OverwriteAction.Rename); Close(); };
        btnOverwrite.Click += (_, _) => { _tcs.TrySetResult(OverwriteAction.OverwriteAll); Close(); };

        btns.Children.Add(btnCancel);
        btns.Children.Add(btnSkip);
        btns.Children.Add(btnRename);
        btns.Children.Add(btnOverwrite);
        panel.Children.Add(btns);
        Content = panel;

        Closing += (_, _) => _tcs.TrySetResult(OverwriteAction.Cancel);
    }

    public Task<OverwriteAction> GetResultAsync() => _tcs.Task;

    private static Button MakeBtn(string label, string bg) => new()
    {
        Content = label,
        Background = SolidColorBrush.Parse(bg),
        Foreground = Brushes.White,
        Padding = new Thickness(10, 5)
    };
}
