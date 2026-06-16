using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using System;
using System.Threading;
using LanCopy.Localization;

namespace LanCopy;

// Ventana de progreso minimizable para procesos largos (copias, borrados...).
// Muestra titulo, elemento actual, barra de progreso, detalle y un boton para
// cancelar/cerrar. Es no-modal: el usuario puede minimizarla y seguir trabajando.
internal sealed class ProgressWindow : Window
{
    private readonly ProgressBar _bar;
    private readonly TextBlock _line;
    private readonly TextBlock _detail;
    private readonly Button _action;
    private readonly CancellationTokenSource? _cts;
    private bool _finished;

    public ProgressWindow(string title, CancellationTokenSource? cts = null)
    {
        _cts = cts;
        Title = title;
        Width = 470;
        Height = 210;
        CanResize = false;
        ShowInTaskbar = true;
        Background = SolidColorBrush.Parse("#2D2D30");
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var titleBlock = new TextBlock
        {
            Text = title,
            Foreground = SolidColorBrush.Parse("#FFD700"),
            FontWeight = FontWeight.Bold,
            FontSize = 15
        };
        _line = new TextBlock
        {
            Text = "…",
            Foreground = SolidColorBrush.Parse("#FFFFFF"),
            FontWeight = FontWeight.SemiBold,
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap
        };
        _bar = new ProgressBar
        {
            Minimum = 0, Maximum = 100, Value = 0, Height = 18,
            Foreground = SolidColorBrush.Parse("#28A745")
        };
        _detail = new TextBlock
        {
            Foreground = SolidColorBrush.Parse("#C8C8C8"),
            FontSize = 12
        };
        _action = new Button
        {
            Content = Loc.Instance["prog.cancel"],
            Background = SolidColorBrush.Parse("#C0392B"),
            Foreground = Brushes.White,
            Padding = new Thickness(16, 6),
            HorizontalAlignment = HorizontalAlignment.Right
        };
        _action.Click += (_, _) =>
        {
            if (_finished) { Close(); return; }
            try { _cts?.Cancel(); } catch { }
            _action.IsEnabled = false;
            _line.Text = Loc.Instance["prog.cancelling"];
        };

        var panel = new StackPanel { Margin = new Thickness(20), Spacing = 12 };
        panel.Children.Add(titleBlock);
        panel.Children.Add(_line);
        panel.Children.Add(_bar);
        panel.Children.Add(_detail);
        panel.Children.Add(_action);
        Content = panel;

        // Cerrar la ventana = cancelar el proceso si sigue activo.
        Closing += (_, _) => { if (!_finished) { try { _cts?.Cancel(); } catch { } } };
    }

    // Actualiza el texto del elemento en curso (hilo seguro).
    public void SetLine(string line)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_finished || string.IsNullOrEmpty(line)) return;
            _line.Text = line;
        });
    }

    // Actualiza la barra (0-100) y el detalle (p. ej. velocidad o "3/10").
    public void SetProgress(double pct, string detail = "")
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_finished) return;
            _bar.Value = Math.Clamp(pct, 0, 100);
            _detail.Text = detail;
        });
    }

    // Marca el proceso como terminado. En exito se autocierra tras 2 s;
    // en error permanece abierto para que el usuario lo lea.
    public void Finish(string summary, bool isError = false)
    {
        Dispatcher.UIThread.Post(() =>
        {
            _finished = true;
            _bar.Value = 100;
            _bar.Foreground = SolidColorBrush.Parse(isError ? "#C0392B" : "#28A745");
            _line.Text = summary;
            _detail.Text = "";
            _action.Content = Loc.Instance["prog.close"];
            _action.Background = SolidColorBrush.Parse("#007ACC");
            _action.IsEnabled = true;

            if (!isError)
            {
                var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
                t.Tick += (_, _) => { t.Stop(); try { Close(); } catch { } };
                t.Start();
            }
        });
    }
}
