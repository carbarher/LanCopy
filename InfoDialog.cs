using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using LanCopy.Localization;

namespace LanCopy;

// Dialogo informativo simple: titulo + cuerpo multilinea + boton de cierre.
// Se usa para mostrar el resultado del diagnostico de red en lenguaje sencillo.
internal sealed class InfoDialog : Window
{
    public InfoDialog(string title, string body)
    {
        Title = title;
        Width = 520;
        Height = 360;
        CanResize = true;
        Background = SolidColorBrush.Parse("#2D2D30");
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var panel = new StackPanel { Margin = new Thickness(20), Spacing = 16 };
        panel.Children.Add(new TextBlock
        {
            Text = title,
            Foreground = SolidColorBrush.Parse("#FFD700"),
            FontWeight = FontWeight.Bold,
            FontSize = 16,
            TextWrapping = TextWrapping.Wrap
        });

        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            Content = new TextBlock
            {
                Text = body,
                Foreground = SolidColorBrush.Parse("#E6E6E6"),
                TextWrapping = TextWrapping.Wrap
            }
        };
        panel.Children.Add(scroll);

        var btnOk = new Button
        {
            Content = Loc.Instance["dlg.ok"],
            Background = SolidColorBrush.Parse("#007ACC"),
            Foreground = Brushes.White,
            Padding = new Thickness(16, 6),
            HorizontalAlignment = HorizontalAlignment.Right
        };
        btnOk.Click += (_, _) => Close();
        panel.Children.Add(btnOk);

        Content = panel;
    }
}
