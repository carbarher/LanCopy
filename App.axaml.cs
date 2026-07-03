using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using LanCopy.Localization;

namespace LanCopy;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        // C12-FIX: registrar handlers globales de excepciones para diagnóstico.
        // Sin esto, las excepciones de background tasks (fire-and-forget) eran silenciosamente
        // ignoradas y nunca llegaban al log de diagnóstico — ocultan bugs en Task.Run sin await.
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Services.Log.Warn("app", "unobserved-task-exception", new { error = args.Exception?.Message, inner = args.Exception?.InnerException?.Message });
            args.SetObserved(); // Evita el crash en .NET 4.x y previene logs redundantes de .NET runtime
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            var ex = args.ExceptionObject as Exception;
            Services.Log.Warn("app", "unhandled-domain-exception", new { error = ex?.Message, isTerminating = args.IsTerminating });
        };

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var splash = new SplashScreen();
            splash.Show();

            _ = Task.Run(async () =>
            {
                try
                {
                    await Dispatcher.UIThread.InvokeAsync(() => splash.SetProgress(30, Loc.Instance["splash.loading"])); // U3: era hardcoded en español
                    await Task.Delay(100);
                    await Dispatcher.UIThread.InvokeAsync(() => splash.SetProgress(60, Loc.Instance["splash.server"]));
                    await Task.Delay(100);
                    await Dispatcher.UIThread.InvokeAsync(() => splash.SetProgress(90, Loc.Instance["splash.ui"]));
                    await Task.Delay(150);
                    await Dispatcher.UIThread.InvokeAsync(() => splash.SetProgress(100, Loc.Instance["splash.ready"]));
                    await Task.Delay(100);

                    await Dispatcher.UIThread.InvokeAsync(async () =>
                    {
                        var main = new MainWindow(desktop.Args);
                        desktop.MainWindow = main;
                        main.Show();
                        await splash.FadeOutAsync();
                        splash.Close();
                    });
                }
                catch (Exception ex)
                {
                    // U1: mostrar el error real en la splash antes de cerrar
                    await Dispatcher.UIThread.InvokeAsync(() => splash.ShowError(ex.Message));
                    await Task.Delay(4000); // 4s para que el usuario lea el error
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        splash.Close();
                        desktop.Shutdown(1);
                    });
                }
            });
        }
        base.OnFrameworkInitializationCompleted();
    }
}

