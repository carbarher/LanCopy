using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using LanCopy.Localization;

namespace LanCopy;

// Dialogo modal de confirmacion de sobreescritura (#10)
internal sealed class ConfirmDialog : Window
{
    public enum OverwriteAction { OverwriteAll, SkipAll, SkipSameSize, Rename, Cancel } // Q2: OverwriteOne/SkipOne eran dead values sin botones

    private readonly TaskCompletionSource<OverwriteAction> _tcs = new();

    public ConfirmDialog(int count, string firstName)
    {
        Title = Loc.Instance["dlg.overwrite.title"];
        Width = 580; // Aumentar ancho para alojar nuevo botón sin desbordamiento
        Height = 185; // U4: más espacio para texto localizado (alemán/ruso más largo)
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
            Foreground = SolidColorBrush.Parse("#E6E6E6"),
            TextWrapping = TextWrapping.Wrap
        });

        var btns = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8
        };

        var btnCancel       = MakeBtn(Loc.Instance["dlg.overwrite.cancel"], "#3E3E42");
        var btnSkipSameSize = MakeBtn(Loc.Instance["dlg.overwrite.skipsamesize"], "#8E44AD"); // color morado
        var btnSkip         = MakeBtn(Loc.Instance["dlg.overwrite.skip"], "#555555");
        var btnRename       = MakeBtn(Loc.Instance["dlg.overwrite.rename"], "#007ACC");
        var btnOverwrite    = MakeBtn(Loc.Instance["dlg.overwrite.overwrite"], "#C0392B");

        btnCancel.Click       += (_, _) => { _tcs.TrySetResult(OverwriteAction.Cancel); Close(); };
        btnSkipSameSize.Click += (_, _) => { _tcs.TrySetResult(OverwriteAction.SkipSameSize); Close(); };
        btnSkip.Click         += (_, _) => { _tcs.TrySetResult(OverwriteAction.SkipAll); Close(); };
        btnRename.Click       += (_, _) => { _tcs.TrySetResult(OverwriteAction.Rename); Close(); };
        btnOverwrite.Click    += (_, _) => { _tcs.TrySetResult(OverwriteAction.OverwriteAll); Close(); };

        btns.Children.Add(btnCancel);
        btns.Children.Add(btnSkipSameSize);
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
