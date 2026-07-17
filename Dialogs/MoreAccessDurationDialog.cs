using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using LanCopy.Localization;

namespace LanCopy;

internal sealed class MoreAccessDurationDialog : Window
{
    public enum MoreAccessDuration { Cancel, TenMinutes, ThirtyMinutes, UntilClose }

    private readonly TaskCompletionSource<MoreAccessDuration> _tcs = new();

    public MoreAccessDurationDialog()
    {
        Title = Loc.Instance["dlg.moreAccess.title"];
        Width = 460;
        Height = 190;
        CanResize = false;
        Background = SolidColorBrush.Parse("#2D2D30");
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var panel = new StackPanel { Margin = new Thickness(20), Spacing = 14 };
        panel.Children.Add(new TextBlock
        {
            Text = Loc.Instance["dlg.moreAccess.body"],
            Foreground = SolidColorBrush.Parse("#E6E6E6"),
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12
        });

        var btns = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8
        };

        var btnCancel = MakeBtn(Loc.Instance["dlg.moreAccess.cancel"], "#C62828");
        var btn10 = MakeBtn(Loc.Instance["dlg.moreAccess.10m"], "#007ACC");
        var btn30 = MakeBtn(Loc.Instance["dlg.moreAccess.30m"], "#007ACC");
        var btnClose = MakeBtn(Loc.Instance["dlg.moreAccess.untilClose"], "#8E44AD");

        btnCancel.Click += (_, _) => Finish(MoreAccessDuration.Cancel);
        btn10.Click += (_, _) => Finish(MoreAccessDuration.TenMinutes);
        btn30.Click += (_, _) => Finish(MoreAccessDuration.ThirtyMinutes);
        btnClose.Click += (_, _) => Finish(MoreAccessDuration.UntilClose);

        btns.Children.Add(btnCancel);
        btns.Children.Add(btn10);
        btns.Children.Add(btn30);
        btns.Children.Add(btnClose);
        panel.Children.Add(btns);
        Content = panel;

        Closing += (_, _) => _tcs.TrySetResult(MoreAccessDuration.Cancel);
    }

    public Task<MoreAccessDuration> GetResultAsync() => _tcs.Task;

    private void Finish(MoreAccessDuration result)
    {
        _tcs.TrySetResult(result);
        Close();
    }

    private static Button MakeBtn(string label, string bg) => new()
    {
        Content = label,
        Background = SolidColorBrush.Parse(bg),
        Foreground = Brushes.White,
        Padding = new Thickness(10, 5)
    };
}

