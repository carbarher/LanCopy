using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Forms;

namespace SlskDown.Models
{
    public sealed class AutoSearchFileResult
    {
        public string Author { get; set; }
        public string Username { get; set; }
        public string FileName { get; set; }
        public string Directory { get; set; }
        public long SizeBytes { get; set; }
        public string SizeReadable { get; set; }
        public bool IsSpanish { get; set; }
        public bool IsDocument { get; set; }
        public DateTime Timestamp { get; set; }
        
        // INTEGRACIÓN MULTI-RED: Red de origen del resultado
        public string Network { get; set; } = "Soulseek"; // Default: Soulseek
        
        // Hash del archivo (para eMule/ed2k)
        public string FileHash { get; set; }
    }

    public class WishlistItem
    {
        public string SearchTerm { get; set; }
        public string Author { get; set; }
        public DateTime DateAdded { get; set; }
        public DateTime? LastSearched { get; set; }
        public bool AutoSearch { get; set; } = true;
        public int TimesFound { get; set; } = 0;
        public string Notes { get; set; }
        public string ItemType { get; set; } = "term";
        public HashSet<string> SeenKeys { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Prioridad de descarga
    /// </summary>
    public enum DownloadPriority
    {
        Low = 0,
        Normal = 1,
        High = 2
    }

    /// <summary>
    /// Estados de descarga inspirados en Nicotine+ para diagnóstico preciso
    /// </summary>
    public enum DownloadStatus
    {
        Queued,              // En cola esperando inicio
        GettingStatus,       // Verificando disponibilidad del archivo
        Downloading,         // Descargando activamente
        Paused,              // Pausado manualmente por el usuario
        Completed,           // Descarga completada exitosamente
        Failed,              // Fallo genérico (ver FailureReason para detalles)
        Cancelled,           // Cancelado manualmente por el usuario
        Filtered,            // Filtrado por blacklist o reglas
        UserLoggedOff,       // Usuario desconectado de la red
        ConnectionClosed,    // Conexión cerrada inesperadamente
        ConnectionTimeout,   // Timeout de conexión
        LocalFileError,      // Error escribiendo archivo local
        RemoteFileError,     // Error leyendo archivo remoto (no compartido, error de lectura)
        UserQueueFull,       // Cola del usuario llena ("Too many files")
        UserQuotaExceeded,   // Cuota del usuario excedida ("Too many megabytes")
        Corrupted,           // MEJORA #16: Archivo corrupto detectado (falla verificación de integridad)
        Incomplete           // MEJORA #16: Archivo incompleto (descarga parcial)
    }

    /// <summary>
    /// Razones detalladas de fallo para análisis
    /// </summary>
    public enum DownloadFailureReason
    {
        Unknown,
        Connection,
        FileIo,
        RemoteCancelled,
        UserCancelled,
        Timeout,
        QueueFull,
        QuotaExceeded,
        FileNotShared,
        Banned,
        PendingShutdown
    }

    public enum QueuePrioritizationStrategy
    {
        Manual,              // Usuario controla prioridades
        FastestFirst,        // Usuarios más rápidos primero
        SmallestFirst,       // Archivos pequeños primero
        LargestFirst,        // Archivos grandes primero
        Balanced             // Balance entre velocidad y tamaño
    }

    public class DownloadTask
    {
        public AutoSearchFileResult File { get; set; }
        public string LocalPath { get; set; }
        public string TargetPath { get; set; }
        public DownloadStatus Status { get; set; }
        public long BytesDownloaded { get; set; }
        public double ProgressPercent { get; set; }
        public double SpeedMBps { get; set; }
        public string UiStatusText { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public string ErrorMessage { get; set; }
        public CancellationTokenSource CancellationToken { get; set; }
        public ListViewItem ListViewItem { get; set; }
        public int QueuePosition { get; set; } = 0; // Posición en cola del proveedor
        public bool IsScheduled { get; set; } = false;
        public DateTime? ScheduledAt { get; set; }
        public string AssignedWorkerId { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        
        // Sistema de reintentos automáticos
        public int RetryCount { get; set; } = 0;
        public int MaxRetries { get; set; } = 3;
        public DateTime? LastRetryTime { get; set; }
        public bool AutoRetryEnabled { get; set; } = true;
        public DateTime? FinalFailureTime { get; set; } = null;
        
        // Multi-source download
        public List<string> AlternativeSources { get; set; } = new List<string>();
        public Dictionary<string, ChunkDownload> ActiveChunks { get; set; } = new Dictionary<string, ChunkDownload>();
        public bool IsMultiSource { get; set; } = false;
        
        // Detección de descargas lentas
        public int SlowDownloadChecks { get; set; } = 0;
        public DateTime LastSpeedCheck { get; set; } = DateTime.MinValue;
        
        // Medición de velocidad para gráfico
        public double LastSpeedMeasurement { get; set; } = 0;
        public long LastBytesDownloaded { get; set; } = 0;

        public DownloadFailureReason LastFailureReason { get; set; } = DownloadFailureReason.Unknown;
        
        // MEJORA #16: Checksum SHA256 del archivo descargado
        public string Checksum { get; set; } = null;

        // Detección de duplicados por contenido
        public bool IsDuplicate { get; set; }
        public string DuplicateOf { get; set; }

        // Filtro de idioma: permite omitir verificación de español para archivos específicos
        public bool SkipLanguageCheck { get; set; } = false;

        // ⭐ NICOTINE+ DESCARGAS: Tiempo restante estimado con cálculo preciso
        private TimeSpan? _estimatedTimeRemaining;
        public TimeSpan EstimatedTimeRemaining
        {
            get
            {
                // Si se estableció manualmente (por AccurateSpeedCalculator), usar ese valor
                if (_estimatedTimeRemaining.HasValue)
                    return _estimatedTimeRemaining.Value;
                
                // Fallback: cálculo automático
                if (SpeedMBps == 0 || Status != DownloadStatus.Downloading) 
                    return TimeSpan.MaxValue;
                
                long bytesRemaining = File.SizeBytes - BytesDownloaded;
                double secondsRemaining = bytesRemaining / (SpeedMBps * 1024 * 1024);
                
                return TimeSpan.FromSeconds(secondsRemaining);
            }
            set => _estimatedTimeRemaining = value;
        }

        // INTEGRACIÓN NICOTINE+: Propiedades adicionales para compatibilidad
        public double Speed => SpeedMBps * 1024 * 1024; // Convertir a bytes/s
        public DateTime? RetryAt { get; set; }
        public DownloadPriority Priority { get; set; } = DownloadPriority.Normal;
        
        // Identificación de fuente de red
        public string Source => File?.Network ?? "Soulseek"; // Obtener de File.Network
        
        // Propiedades de compatibilidad con código legacy
        public string RemotePath => File?.Directory + "/" + File?.FileName;
        public string Username => File?.Username;
        public long FileSize => File?.SizeBytes ?? 0;
        public string Error { get => ErrorMessage; set => ErrorMessage = value; }
        public DateTime? CompletedTime => CompletedAt;
    }

    public class ChunkDownload
    {
        public string Username { get; set; }
        public long StartOffset { get; set; }
        public long EndOffset { get; set; }
        public long BytesDownloaded { get; set; }
        public double SpeedMBps { get; set; }
        public bool IsComplete { get; set; }
    }

    public class UserProviderStats
    {
        public string Username { get; set; }
        public int TotalDownloads { get; set; }
        public int SuccessfulDownloads { get; set; }
        public int FailedDownloads { get; set; }
        public double AverageSpeed { get; set; }
        public long TotalBytesDownloaded { get; set; }
        public DateTime LastDownload { get; set; }
        public DateTime LastDownloadDate { get; set; }
        public double SuccessRate => TotalDownloads > 0 ? (double)SuccessfulDownloads / TotalDownloads * 100 : 0;
        public double QualityScore => CalculateQualityScore();
        
        private double CalculateQualityScore()
        {
            // Score basado en: velocidad promedio (50%), tasa de éxito (40%), total descargas (10%)
            // PRIORIDAD: Velocidad > Confiabilidad > Experiencia
            double speedScore = Math.Min(AverageSpeed / 10.0, 10) * 5.0; // Max 50 puntos (10 MB/s = 50)
            double successScore = SuccessRate * 0.4; // Max 40 puntos (100% = 40)
            double volumeScore = Math.Min(TotalDownloads / 10.0, 10) * 1.0; // Max 10 puntos (100 descargas = 10)
            
            return speedScore + successScore + volumeScore;
        }
    }

    // Alias para compatibilidad con código existente
    public class ProviderStats : UserProviderStats
    {
    }

    public class DownloadHistoryRecord
    {
        public string FileName { get; set; }
        public string Author { get; set; }
        public string Username { get; set; }
        public long SizeBytes { get; set; }
        public DateTime DownloadedAt { get; set; }
        public DateTime CompletedAt { get; set; }
        public string Status { get; set; }
        public double SpeedMBps { get; set; }
        public string Hash { get; set; } // BLAKE3 hash for integrity verification
        public string RelativePath { get; set; }
        public bool? IsSpanish { get; set; }
        public bool IsDuplicate { get; set; }
        public string DuplicateOf { get; set; }
    }

    public class AppStatistics
    {
        public int TotalFilesFound { get; set; }
        public int TotalFilesDownloaded { get; set; }
        public long TotalBytesDownloaded { get; set; }
        public int TotalSearches { get; set; }
        public int SuccessfulSearches { get; set; }
        public int TotalDownloads { get; set; }
        public int SuccessfulDownloads { get; set; }
        public int TotalResultsFound { get; set; }
        public DateTime SessionStartTime { get; set; }
        public TimeSpan TotalDownloadTime { get; set; }
        public double AverageDownloadSpeed { get; set; }
        public double LastDownloadSpeed { get; set; }
        public double PeakDownloadSpeed { get; set; }
        public double AverageThroughputPerFile { get; set; }
        public int ThroughputSamples { get; set; }
        public DateTime FirstUse { get; set; }
        public DateTime LastUse { get; set; }
    }

    /// <summary>
    /// Regla de auto-descarga para descargar automáticamente archivos que cumplan ciertos criterios
    /// </summary>
    public class AutoDownloadRule
    {
        public string Name { get; set; }
        public string SearchPattern { get; set; }
        public string AuthorFilter { get; set; }
        public long MinSizeBytes { get; set; }
        public long MaxSizeBytes { get; set; }
        public List<string> AllowedExtensions { get; set; } = new List<string>();
        public List<string> BlockedExtensions { get; set; } = new List<string>();
        public List<string> RequiredKeywords { get; set; } = new List<string>();
        public List<string> BlockedKeywords { get; set; } = new List<string>();
        public bool OnlySpanish { get; set; }
        public int MaxDownloadsPerDay { get; set; } = 10;
        public string TargetFolder { get; set; }
        public bool Enabled { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? LastUsed { get; set; }
        public int TimesUsed { get; set; } = 0;
        public int TodayDownloads { get; set; } = 0;
        public DateTime LastResetDate { get; set; } = DateTime.Today;
    }

    public sealed class SearchAlternativesResult
    {
        public bool Success { get; init; }
        public AutoSearchFileResult Alternative { get; init; }
        public IReadOnlyList<AutoSearchFileResult> Candidates { get; init; } = Array.Empty<AutoSearchFileResult>();
        public string FailureReason { get; init; }
    }
}
