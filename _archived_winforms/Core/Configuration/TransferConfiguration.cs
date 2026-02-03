using System;
using System.Collections.Generic;

namespace SlskDown.Core.Configuration
{
    /// <summary>
    /// Configuración granular de transferencias inspirada en Nicotine+
    /// 30+ opciones configurables para control total
    /// </summary>
    public class TransferConfiguration
    {
        // === LÍMITES DE VELOCIDAD ===
        
        /// <summary>
        /// Límite de velocidad de descarga en KB/s (0 = sin límite)
        /// </summary>
        public int DownloadSpeedLimit { get; set; } = 0;

        /// <summary>
        /// Límite de velocidad de subida en KB/s (0 = sin límite)
        /// </summary>
        public int UploadSpeedLimit { get; set; } = 0;

        /// <summary>
        /// Usar límite de velocidad de descarga
        /// </summary>
        public bool UseDownloadSpeedLimit { get; set; } = false;

        /// <summary>
        /// Usar límite de velocidad de subida
        /// </summary>
        public bool UseUploadSpeedLimit { get; set; } = false;

        // === FILTROS ===

        /// <summary>
        /// Regex para filtrar descargas
        /// </summary>
        public string DownloadRegex { get; set; } = "";

        /// <summary>
        /// Regex para filtrar subidas
        /// </summary>
        public string UploadRegex { get; set; } = "";

        /// <summary>
        /// Palabras clave excluidas
        /// </summary>
        public List<string> ExcludedKeywords { get; set; } = new List<string>();

        /// <summary>
        /// Extensiones permitidas
        /// </summary>
        public List<string> AllowedExtensions { get; set; } = new List<string>
        {
            ".epub", ".mobi", ".azw3", ".pdf", ".fb2", ".txt", ".doc", ".docx"
        };

        /// <summary>
        /// Tamaño mínimo de archivo en bytes
        /// </summary>
        public long MinimumFileSize { get; set; } = 100;

        /// <summary>
        /// Tamaño máximo de archivo en bytes (0 = sin límite)
        /// </summary>
        public long MaximumFileSize { get; set; } = 0;

        // === DIRECTORIOS ===

        /// <summary>
        /// Directorio de descargas
        /// </summary>
        public string DownloadDirectory { get; set; } = "";

        /// <summary>
        /// Directorio de descargas incompletas
        /// </summary>
        public string IncompleteDirectory { get; set; } = "";

        /// <summary>
        /// Directorio de subidas
        /// </summary>
        public string UploadDirectory { get; set; } = "";

        /// <summary>
        /// Organizar por autor en subcarpetas
        /// </summary>
        public bool OrganizeByAuthor { get; set; } = true;

        // === COMPORTAMIENTO DE COLA ===

        /// <summary>
        /// Limpiar descargas completadas automáticamente
        /// </summary>
        public bool AutoClearCompletedDownloads { get; set; } = false;

        /// <summary>
        /// Limpiar subidas completadas automáticamente
        /// </summary>
        public bool AutoClearCompletedUploads { get; set; } = false;

        /// <summary>
        /// Máximo de descargas en cola
        /// </summary>
        public int MaxDownloadQueue { get; set; } = 10000;

        /// <summary>
        /// Máximo de subidas en cola
        /// </summary>
        public int MaxUploadQueue { get; set; } = 10000;

        /// <summary>
        /// Usar cola FIFO (First In First Out) en lugar de LIFO
        /// </summary>
        public bool UseFIFOQueue { get; set; } = false;

        /// <summary>
        /// Descargas paralelas simultáneas
        /// </summary>
        public int MaxParallelDownloads { get; set; } = 3;

        /// <summary>
        /// Subidas paralelas simultáneas
        /// </summary>
        public int MaxParallelUploads { get; set; } = 2;

        // === REINTENTOS ===

        /// <summary>
        /// Número máximo de reintentos por proveedor
        /// </summary>
        public int MaxRetries { get; set; } = 3;

        /// <summary>
        /// Número máximo de proveedores alternativos a intentar
        /// </summary>
        public int MaxAlternativeProviders { get; set; } = 3;

        /// <summary>
        /// Límite absoluto de intentos totales
        /// </summary>
        public int MaxTotalAttempts { get; set; } = 15;

        /// <summary>
        /// Delay entre reintentos
        /// </summary>
        public TimeSpan RetryDelay { get; set; } = TimeSpan.FromMinutes(2);

        /// <summary>
        /// Usar backoff exponencial para reintentos
        /// </summary>
        public bool UseExponentialBackoff { get; set; } = true;

        // === TIMEOUTS ===

        /// <summary>
        /// Timeout de conexión
        /// </summary>
        public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Timeout de transferencia
        /// </summary>
        public TimeSpan TransferTimeout { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Timeout de stall (sin progreso)
        /// </summary>
        public TimeSpan StallTimeout { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Timeout de stall para eMule (3x más largo)
        /// </summary>
        public TimeSpan EMuleStallTimeout { get; set; } = TimeSpan.FromMinutes(15);

        // === BLOQUEOS ===

        /// <summary>
        /// Usar mensaje de ban personalizado
        /// </summary>
        public bool UseCustomBanMessage { get; set; } = false;

        /// <summary>
        /// Mensaje de ban personalizado
        /// </summary>
        public string CustomBanMessage { get; set; } = "Access denied";

        /// <summary>
        /// Habilitar bloqueo geográfico
        /// </summary>
        public bool EnableGeoBlocking { get; set; } = false;

        /// <summary>
        /// Países bloqueados (códigos ISO)
        /// </summary>
        public List<string> BlockedCountries { get; set; } = new List<string>();

        // === VALIDACIÓN ===

        /// <summary>
        /// Validar archivos después de descarga
        /// </summary>
        public bool ValidateAfterDownload { get; set; } = true;

        /// <summary>
        /// Verificar hash SHA256
        /// </summary>
        public bool VerifyHash { get; set; } = false;

        /// <summary>
        /// Mover archivos corruptos a carpeta especial
        /// </summary>
        public bool QuarantineCorruptedFiles { get; set; } = true;

        // === OPTIMIZACIONES ===

        /// <summary>
        /// Usar connection pooling
        /// </summary>
        public bool UseConnectionPooling { get; set; } = true;

        /// <summary>
        /// Timeout de conexión idle en pool
        /// </summary>
        public TimeSpan ConnectionPoolIdleTimeout { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Usar caché de validación SHA256
        /// </summary>
        public bool UseValidationCache { get; set; } = true;

        /// <summary>
        /// Usar lazy loading de metadatos
        /// </summary>
        public bool UseLazyMetadataLoading { get; set; } = true;

        /// <summary>
        /// Tamaño de buffer de lectura/escritura
        /// </summary>
        public int BufferSize { get; set; } = 8192;

        // === ESTADÍSTICAS ===

        /// <summary>
        /// Habilitar tracking de estadísticas detalladas
        /// </summary>
        public bool EnableDetailedStatistics { get; set; } = true;

        /// <summary>
        /// Guardar estadísticas en archivo
        /// </summary>
        public bool SaveStatisticsToFile { get; set; } = true;

        /// <summary>
        /// Intervalo de guardado de estadísticas
        /// </summary>
        public TimeSpan StatisticsSaveInterval { get; set; } = TimeSpan.FromMinutes(5);

        // === MÉTODOS DE UTILIDAD ===

        /// <summary>
        /// Valida la configuración
        /// </summary>
        public List<string> Validate()
        {
            var errors = new List<string>();

            if (MaxParallelDownloads < 1)
                errors.Add("MaxParallelDownloads debe ser al menos 1");

            if (MaxRetries < 0)
                errors.Add("MaxRetries no puede ser negativo");

            if (MaxTotalAttempts < MaxRetries)
                errors.Add("MaxTotalAttempts debe ser mayor o igual que MaxRetries");

            if (ConnectionTimeout.TotalSeconds < 5)
                errors.Add("ConnectionTimeout debe ser al menos 5 segundos");

            if (!string.IsNullOrWhiteSpace(DownloadDirectory) && !System.IO.Directory.Exists(DownloadDirectory))
                errors.Add($"DownloadDirectory no existe: {DownloadDirectory}");

            return errors;
        }

        /// <summary>
        /// Crea una configuración por defecto
        /// </summary>
        public static TransferConfiguration CreateDefault()
        {
            return new TransferConfiguration();
        }

        /// <summary>
        /// Crea una configuración optimizada para velocidad
        /// </summary>
        public static TransferConfiguration CreateSpeedOptimized()
        {
            return new TransferConfiguration
            {
                MaxParallelDownloads = 10,
                UseConnectionPooling = true,
                UseValidationCache = true,
                UseLazyMetadataLoading = true,
                BufferSize = 16384,
                ConnectionTimeout = TimeSpan.FromSeconds(15),
                MaxRetries = 5,
                UseExponentialBackoff = true
            };
        }

        /// <summary>
        /// Crea una configuración conservadora (para conexiones lentas)
        /// </summary>
        public static TransferConfiguration CreateConservative()
        {
            return new TransferConfiguration
            {
                MaxParallelDownloads = 2,
                ConnectionTimeout = TimeSpan.FromMinutes(1),
                StallTimeout = TimeSpan.FromMinutes(10),
                MaxRetries = 3,
                RetryDelay = TimeSpan.FromMinutes(5)
            };
        }
    }
}
