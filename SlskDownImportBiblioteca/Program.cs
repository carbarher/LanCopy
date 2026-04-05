using Avalonia;
using System;
using System.Threading;

namespace SlskDownImportBiblioteca;

/// <summary>Mismo mutex que históricamente usaba <c>--import-biblioteca</c>: una sola instancia de la herramienta.</summary>
internal static class ImportBibliotecaMutex
{
    internal const string Name = "SlskDownAvalonia_ImportBiblioteca_6f2a";
}

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        using var mutex = new Mutex(true, ImportBibliotecaMutex.Name, out var createdNew);
        if (!createdNew)
        {
            Environment.ExitCode = 2;
            return;
        }

        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<ImportApp>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
