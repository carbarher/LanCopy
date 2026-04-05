using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Serilog;
using System;
using System.IO;
using System.Threading.Tasks;

namespace SlskDownImportBiblioteca;

public partial class ImportApp : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        try
        {
            var logDir = Path.Combine(AppContext.BaseDirectory, "Logs");
            Directory.CreateDirectory(logDir);
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.File(Path.Combine(logDir, "import_biblioteca.log"))
                .CreateLogger();
            Log.Information("=== SlskDownImportBiblioteca iniciando ===");
        }
        catch { /* sin logger */ }

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            var ex = e.ExceptionObject as Exception;
            AppendImportCrashLog($"UNHANDLED (terminating={e.IsTerminating}): {ex?.GetType().Name}: {ex?.Message}\n{ex?.StackTrace}\n");
            try { Log.Error(ex, "Import app: unhandled"); } catch { }
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            e.SetObserved();
            AppendImportCrashLog($"UNOBSERVED: {e.Exception?.Message}\n{e.Exception}\n");
            try { Log.Error(e.Exception, "Import app: unobserved task"); } catch { }
        };

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
            desktop.MainWindow = new ImportBibliotecaWindow();

            desktop.ShutdownRequested += (_, _) =>
            {
                try
                {
                    Log.Information("=== SlskDownImportBiblioteca cerrando ===");
                    Log.CloseAndFlush();
                }
                catch { /* ignore */ }
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void AppendImportCrashLog(string text)
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "import_crash.log");
            File.AppendAllText(path, $"[{DateTime.Now:O}] {text}\n");
        }
        catch { /* ignore */ }
    }
}
