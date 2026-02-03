using System;
using System.IO;
using Serilog;
using Serilog.Events;

namespace SlskDown.Infrastructure
{
    /// <summary>
    /// Logger estructurado con Serilog
    /// Permite búsquedas SQL en logs y análisis avanzado
    /// </summary>
    public static class StructuredLogger
    {
        private static ILogger _logger;
        private static bool _isInitialized;

        public static void Initialize(string dataDir, bool enableDebug = false)
        {
            if (_isInitialized)
                return;

            var logDir = Path.Combine(dataDir, "logs");
            Directory.CreateDirectory(logDir);

            var logConfig = new LoggerConfiguration()
                .MinimumLevel.Is(enableDebug ? LogEventLevel.Debug : LogEventLevel.Information)
                .Enrich.WithProperty("Application", "SlskDown")
                .Enrich.WithProperty("Version", "4.1.0")
                .Enrich.WithThreadId()
                .Enrich.WithThreadName();

            // Archivo de texto con rotación diaria
            logConfig.WriteTo.File(
                Path.Combine(logDir, "slskdown-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] [{ThreadId}] {Message:lj}{NewLine}{Exception}");

            // SQLite para búsquedas avanzadas
            var dbPath = Path.Combine(logDir, "logs.db");
            logConfig.WriteTo.SQLite(
                sqliteDbPath: dbPath,
                tableName: "Logs",
                restrictedToMinimumLevel: LogEventLevel.Information,
                storeTimestampInUtc: false);

            // Consola para debugging
            if (enableDebug)
            {
                // ERROR: logConfig.WriteTo.Console(
                    outputTemplate: "{Timestamp:HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}");
            }

            // ERROR: _logger = logConfig.CreateLogger();
            _isInitialized = true;

            // ERROR: Log.Logger = _logger;
        }

        public static void Debug(string message, params object[] args)
        {
            // ERROR: _logger?.Debug(message, args);
        }

        public static void Information(string message, params object[] args)
        {
            // ERROR: _logger?.Information(message, args);
        }

        public static void Warning(string message, params object[] args)
        {
            // ERROR: _logger?.Warning(message, args);
        }

        public static void Error(Exception ex, string message, params object[] args)
        {
            // ERROR: _logger?.Error(ex, message, args);
        }

        public static void Error(string message, params object[] args)
        {
            // ERROR: _logger?.Error(message, args);
        }

        public static void Fatal(Exception ex, string message, params object[] args)
        {
            // ERROR: _logger?.Fatal(ex, message, args);
        }

        // Métodos específicos del dominio
        public static void LogDownloadStarted(string fileName, string username, long sizeBytes)
        {
            // ERROR: _logger?.Information("Descarga iniciada: {FileName} desde {Username} ({Size} bytes)",
                fileName, username, sizeBytes);
        }

        public static void LogDownloadCompleted(string fileName, string username, TimeSpan duration, double speedMBps)
        {
            // ERROR: _logger?.Information("Descarga completada: {FileName} desde {Username} en {Duration:F1}s a {Speed:F2} MB/s",
                fileName, username, duration.TotalSeconds, speedMBps);
        }

        public static void LogDownloadFailed(string fileName, string username, string error, int retryCount, int maxRetries)
        {
            // ERROR: _logger?.Warning("Descarga fallida: {FileName} desde {Username} - {Error} (reintento {RetryCount}/{MaxRetries})",
                fileName, username, error, retryCount, maxRetries);
        }

        public static void LogSearchStarted(string query, int tabIndex)
        {
            // ERROR: _logger?.Information("Búsqueda iniciada: {Query} en tab {TabIndex}",
                query, tabIndex);
        }

        public static void LogSearchCompleted(string query, int resultCount, TimeSpan duration)
        {
            // ERROR: _logger?.Information("Búsqueda completada: {Query} - {ResultCount} resultados en {Duration:F1}s",
                query, resultCount, duration.TotalSeconds);
        }

        public static void LogLibraryScan(int fileCount, TimeSpan duration)
        {
            // ERROR: _logger?.Information("Escaneo de biblioteca: {FileCount} archivos en {Duration:F1}s",
                fileCount, duration.TotalSeconds);
        }

        public static void LogCircuitBreakerTripped(string provider, int failureCount)
        {
            // ERROR: _logger?.Warning("Circuit breaker activado para {Provider} después de {FailureCount} fallos",
                provider, failureCount);
        }

        public static void LogMemoryUsage(long workingSetMB, long gcMemoryMB)
        {
            // ERROR: _logger?.Debug("Uso de memoria: WorkingSet={WorkingSetMB}MB, GC={GcMemoryMB}MB",
                workingSetMB, gcMemoryMB);
        }

        public static void Close()
        {
            (_logger as IDisposable)?.Dispose();
            Log.CloseAndFlush();
        }
    }
}
