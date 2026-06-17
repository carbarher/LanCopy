using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

namespace LanCopy;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var splash = new SplashScreen();
            splash.Show();

            _ = Task.Run(async () =>
            {
                await Dispatcher.UIThread.InvokeAsync(() => splash.SetProgress(30, "Cargando configuración..."));
                await Task.Delay(100);
                await Dispatcher.UIThread.InvokeAsync(() => splash.SetProgress(60, "Iniciando servidor..."));
                await Task.Delay(100);
                await Dispatcher.UIThread.InvokeAsync(() => splash.SetProgress(90, "Preparando interfaz..."));
                await Task.Delay(150);
                await Dispatcher.UIThread.InvokeAsync(() => splash.SetProgress(100, "Listo"));
                await Task.Delay(100);

                await Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    var main = new MainWindow();
                    desktop.MainWindow = main;
                    main.Show();
                    await splash.FadeOutAsync();
                    splash.Close();
                });
            });
        }
        base.OnFrameworkInitializationCompleted();
    }
}

