using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using System.Collections.Generic;
using LanCopy.Localization;

namespace LanCopy;

// Asistente de bienvenida para usuarios sin conocimientos de redes.
// Muestra 3 pasos sencillos con lenguaje llano. Se abre en el primer uso
// y tambien desde el boton de ayuda.
internal sealed class WelcomeDialog : Window
{
    private int _step;
    private readonly List<(string Title, string Body)> _steps;
    private readonly TextBlock _title = new() { Foreground = SolidColorBrush.Parse("#FFD700"), FontWeight = FontWeight.Bold, FontSize = 18, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _body = new() { Foreground = SolidColorBrush.Parse("#E6E6E6"), FontSize = 14, TextWrapping = TextWrapping.Wrap, MinHeight = 120 };
    private readonly TextBlock _dots = new() { Foreground = SolidColorBrush.Parse("#C2C2C2"), HorizontalAlignment = HorizontalAlignment.Center };
    private readonly Button _back;
    private readonly Button _next;

    public WelcomeDialog()
    {
        Title = Loc.Instance["wizard.title"];
        Width = 520;
        Height = 320;
        CanResize = false;
        Background = SolidColorBrush.Parse("#2D2D30");
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _steps = new()
        {
            (Loc.Instance["wizard.s1.title"], Loc.Instance["wizard.s1.body"]),
            (Loc.Instance["wizard.s2.title"], Loc.Instance["wizard.s2.body"]),
            (Loc.Instance["wizard.s3.title"], Loc.Instance["wizard.s3.body"]),
        };

        _back = new Button { Content = Loc.Instance["wizard.back"], Background = SolidColorBrush.Parse("#3C3C3C"), Foreground = Brushes.White, Padding = new Thickness(14, 6) };
        _next = new Button { Content = Loc.Instance["wizard.next"], Background = SolidColorBrush.Parse("#007ACC"), Foreground = Brushes.White, Padding = new Thickness(14, 6) };
        _back.Click += (_, _) => { if (_step > 0) { _step--; Render(); } };
        _next.Click += (_, _) => { if (_step < _steps.Count - 1) { _step++; Render(); } else Close(); };

        var nav = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8 };
        nav.Children.Add(_back);
        nav.Children.Add(_next);

        var panel = new StackPanel { Margin = new Thickness(24), Spacing = 16 };
        panel.Children.Add(_title);
        panel.Children.Add(_body);
        panel.Children.Add(_dots);
        panel.Children.Add(nav);
        Content = panel;

        Render();
    }

    private void Render()
    {
        _title.Text = _steps[_step].Title;
        _body.Text = _steps[_step].Body;
        _dots.Text = string.Concat(System.Linq.Enumerable.Range(0, _steps.Count).Select(i => i == _step ? "\u25CF " : "\u25CB "));
        _back.IsEnabled = _step > 0;
        _next.Content = _step == _steps.Count - 1 ? Loc.Instance["wizard.finish"] : Loc.Instance["wizard.next"];
    }
}
