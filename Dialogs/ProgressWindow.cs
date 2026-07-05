using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using System;
using System.Diagnostics;
using System.Threading;
using LanCopy.Localization;
using LanCopy.Services;

namespace LanCopy;

// Ventana de progreso minimizable para procesos largos (copias, borrados...).
// Muestra titulo, elemento actual, barra de progreso, detalle y un boton para
// cancelar/cerrar. Es no-modal: el usuario puede minimizarla y seguir trabajando.
internal sealed class ProgressWindow : Window
{
    private readonly ProgressBar _bar;
    private readonly TextBlock _line;
    private readonly TextBlock _detail;
    private readonly TextBlock _sparkline; // F1: mini-grafica de velocidad
    private readonly TextBlock _eta;       // F1: tiempo restante estimado
    private readonly Button _action;
        private readonly Button _btnOpenFolder; // F5: abrir carpeta destino
    private readonly Button _btnOpenFile;   // F5b: abrir archivo unico descargado
    private string? _destFolder;            // F5: guardado al iniciar descarga
    private string? _destFile;             // F5b: ruta del archivo unico
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
        // F1: sparkline + ETA inline
        _sparkline = new TextBlock
        {
            Foreground = SolidColorBrush.Parse("#00BCD4"),
            FontSize = 13,
            FontFamily = new FontFamily("Consolas,Courier New,monospace"),
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };
        _eta = new TextBlock
        {
            Foreground = SolidColorBrush.Parse("#888888"),
            FontSize = 12,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Margin = new Thickness(10, 0, 0, 0)
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
            try { _cts?.Cancel(); }
            catch (ObjectDisposedException ex) { Log.Debug("progress", "cancel-cts-disposed", new { error = ex.Message }); }
            catch (Exception ex) { Log.Warn("progress", "cancel-cts-failed", new { error = ex.Message }); }
            _action.IsEnabled = false;
            _line.Text = Loc.Instance["prog.cancelling"];
        };
        _btnOpenFolder = new Button
        {
            Content = Loc.Instance["prog.openFolder"],
            Background = SolidColorBrush.Parse("#007ACC"),
            Foreground = Brushes.White,
            Padding = new Thickness(14, 5),
            HorizontalAlignment = HorizontalAlignment.Right,
            IsVisible = false // F5: solo visible tras descarga completa
        };
        _btnOpenFolder.Click += (_, _) =>
        {
            if (_destFolder != null && System.IO.Directory.Exists(_destFolder))
                try { Process.Start(new ProcessStartInfo { FileName = _destFolder, UseShellExecute = true })?.Dispose(); }
                catch (Exception ex) { Log.Warn("progress", "open-folder-failed", new { path = _destFolder, error = ex.Message }); }
        };
        _btnOpenFile = new Button
        {
            Content = Loc.Instance["prog.openFile"],
            Background = SolidColorBrush.Parse("#28A745"),
            Foreground = Brushes.White,
            Padding = new Thickness(14, 5),
            HorizontalAlignment = HorizontalAlignment.Right,
            IsVisible = false
        };
        _btnOpenFile.Click += (_, _) =>
        {
            if (_destFile != null && System.IO.File.Exists(_destFile))
                try { Process.Start(new ProcessStartInfo { FileName = _destFile, UseShellExecute = true })?.Dispose(); }
                catch (Exception ex) { Log.Warn("progress", "open-file-failed", new { path = _destFile, error = ex.Message }); }
        };

        var panel = new StackPanel { Margin = new Thickness(20), Spacing = 12 };
        panel.Children.Add(titleBlock);
        panel.Children.Add(_line);
        panel.Children.Add(_bar);
        panel.Children.Add(_detail);
        // F1: fila con sparkline + ETA
        var sparkRow = new Avalonia.Controls.StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal };
        sparkRow.Children.Add(_sparkline);
        sparkRow.Children.Add(_eta);
        panel.Children.Add(sparkRow);
        panel.Children.Add(_action);
        panel.Children.Add(_btnOpenFolder); // F5
        panel.Children.Add(_btnOpenFile);   // F5b
        Content = panel;

        // Cerrar la ventana = cancelar el proceso si sigue activo.
        Closing += (_, _) =>
        {
            if (!_finished)
            {
                try { _cts?.Cancel(); }
                catch (ObjectDisposedException ex) { Log.Debug("progress", "cancel-cts-on-close-disposed", new { error = ex.Message }); }
                catch (Exception ex) { Log.Warn("progress", "cancel-cts-on-close-failed", new { error = ex.Message }); }
            }
        };
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

    // F5: Guardar carpeta destino para el boton "Abrir carpeta" (llamar antes de iniciar descarga)
    public void SetDestFolder(string? folder) { _destFolder = folder; }
    // F5b: Guardar archivo unico para el boton "Abrir archivo"
    public void SetDestFile(string? file) { _destFile = file; }

    // F1: Actualiza sparkline + ETA desde TransferStatus (hilo seguro).
    public void SetSpeedDetail(string sparkline, string eta)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_finished) return;
            _sparkline.Text = sparkline;
            _eta.Text = string.IsNullOrEmpty(eta) ? "" : $"ETA {eta}";
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
            _sparkline.Text = ""; // F1: limpiar al terminar
            _eta.Text = "";
            _action.Content = Loc.Instance["prog.close"];
            _action.Background = SolidColorBrush.Parse("#007ACC");
            _action.IsEnabled = true;

            if (!isError)
            {
                // F5: mostrar boton de carpeta si hay destino
                if (!string.IsNullOrEmpty(_destFolder))
                    _btnOpenFolder.IsVisible = true;
                if (!string.IsNullOrEmpty(_destFile) && System.IO.File.Exists(_destFile))
                    _btnOpenFile.IsVisible = true;

                // Auto-cerrar solo cuando ningún botón de acción esté visible:
                // si "Abrir Carpeta" o "Abrir Archivo" están visibles el usuario necesita
                // tiempo para verlos y hacer clic (cerrar a los 2s sería un bug UX).
                if (!_btnOpenFolder.IsVisible && !_btnOpenFile.IsVisible)
                {
                    var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
                    t.Tick += (_, _) =>
                    {
                        t.Stop();
                        try { Close(); }
                        catch (Exception ex) { Log.Debug("progress", "auto-close-failed", new { error = ex.Message }); }
                    };
                    t.Start();
                }
            }
        });
    }
}
