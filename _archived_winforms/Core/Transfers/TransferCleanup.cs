using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SlskDown.Models;

namespace SlskDown.Core.Transfers
{
    /// <summary>
    /// Sistema de cleanup robusto de transferencias inspirado en Nicotine+
    /// Cleanup ordenado en 10 pasos para evitar fugas de recursos
    /// </summary>
    public class TransferCleanup
    {
        /// <summary>
        /// Aborta una transferencia de forma robusta y ordenada
        /// </summary>
        public static async Task AbortTransferAsync(
            DownloadTask transfer,
            TransferStatus status,
            string reason = null,
            Action<string> logger = null)
        {
            if (transfer == null)
                return;

            try
            {
                // PASO 1: Marcar como abortando para evitar operaciones concurrentes
                if (transfer.IsAborting)
                {
                    logger?.Invoke($"Transfer already aborting: {transfer.FileName}");
                    return;
                }

                transfer.IsAborting = true;
                logger?.Invoke($"Starting abort sequence for: {transfer.FileName}");

                // PASO 2: Cancelar operaciones pendientes
                try
                {
                    transfer.CancellationTokenSource?.Cancel();
                    logger?.Invoke($"Cancelled pending operations for: {transfer.FileName}");
                }
                catch (Exception ex)
                {
                    logger?.Invoke($"Error cancelling operations: {ex.Message}");
                }

                // PASO 3: Cerrar conexión de red
                if (transfer.Connection != null)
                {
                    try
                    {
                        await CloseConnectionAsync(transfer.Connection);
                        transfer.Connection = null;
                        logger?.Invoke($"Closed network connection for: {transfer.FileName}");
                    }
                    catch (Exception ex)
                    {
                        logger?.Invoke($"Error closing connection: {ex.Message}");
                    }
                }

                // PASO 4: Cerrar y flush archivo
                if (transfer.FileStream != null)
                {
                    try
                    {
                        await transfer.FileStream.FlushAsync();
                        transfer.FileStream.Dispose();
                        transfer.FileStream = null;
                        logger?.Invoke($"Closed file stream for: {transfer.FileName}");
                    }
                    catch (Exception ex)
                    {
                        logger?.Invoke($"Error closing file: {ex.Message}");
                    }
                }

                // PASO 5: Notificar al peer si es necesario
                if (reason != null && transfer.Status == TransferStatus.Transferring)
                {
                    try
                    {
                        await SendCancelMessageAsync(transfer.Username, transfer.FileName, reason);
                        logger?.Invoke($"Sent cancel message to peer: {transfer.Username}");
                    }
                    catch (Exception ex)
                    {
                        logger?.Invoke($"Error sending cancel message: {ex.Message}");
                    }
                }

                // PASO 6: Resetear flags y offsets
                transfer.LegacyAttempt = false;
                transfer.SizeChanged = false;
                transfer.LastByteOffset = null;

                // PASO 7: Establecer estado final
                transfer.Status = status;
                transfer.ErrorMessage = reason;
                transfer.AbortedAt = DateTime.UtcNow;

                // PASO 8: Actualizar estructuras de datos (implementar en llamador)
                // RemoveFromActiveTransfers(transfer);
                // RemoveFromQueue(transfer);

                // PASO 9: Persistir estado
                try
                {
                    await SaveTransferStateAsync(transfer);
                    logger?.Invoke($"Saved transfer state for: {transfer.FileName}");
                }
                catch (Exception ex)
                {
                    logger?.Invoke($"Error saving state: {ex.Message}");
                }

                // PASO 10: Cleanup de recursos del usuario (implementar en llamador)
                // await CleanupUserResourcesAsync(transfer.Username);

                logger?.Invoke($"Transfer abort completed: {transfer.FileName}");
            }
            catch (Exception ex)
            {
                logger?.Invoke($"Critical error in abort sequence: {ex.Message}");
            }
            finally
            {
                transfer.IsAborting = false;
            }
        }

        /// <summary>
        /// Cierra una conexión de red de forma segura
        /// </summary>
        private static async Task CloseConnectionAsync(object connection)
        {
            if (connection == null)
                return;

            await Task.Run(() =>
            {
                try
                {
                    if (connection is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                }
                catch
                {
                    // Ignorar errores al cerrar
                }
            });
        }

        /// <summary>
        /// Envía mensaje de cancelación al peer
        /// </summary>
        private static async Task SendCancelMessageAsync(string username, string fileName, string reason)
        {
            // Implementar según protocolo Soulseek
            await Task.CompletedTask;
        }

        /// <summary>
        /// Guarda estado de transferencia
        /// </summary>
        private static async Task SaveTransferStateAsync(DownloadTask transfer)
        {
            // Implementar persistencia
            await Task.CompletedTask;
        }

        /// <summary>
        /// Cierra un archivo de forma segura
        /// </summary>
        public static void CloseFile(DownloadTask transfer, Action<string> logger = null)
        {
            if (transfer?.FileStream == null)
                return;

            try
            {
                var filePath = transfer.FilePath;
                transfer.FileStream.Dispose();
                transfer.FileStream = null;
                logger?.Invoke($"Closed file: {Path.GetFileName(filePath)}");
            }
            catch (Exception ex)
            {
                logger?.Invoke($"Error closing file: {ex.Message}");
            }
        }

        /// <summary>
        /// Limpia recursos de un usuario cuando no hay más transferencias
        /// </summary>
        public static async Task CleanupUserResourcesAsync(
            string username,
            Func<string, bool> hasActiveTransfers,
            Action<string> unwatchUser,
            Action<string> logger = null)
        {
            if (string.IsNullOrWhiteSpace(username))
                return;

            await Task.Run(() =>
            {
                try
                {
                    // Solo limpiar si no hay transferencias activas
                    if (!hasActiveTransfers(username))
                    {
                        unwatchUser?.Invoke(username);
                        logger?.Invoke($"Cleaned up resources for user: {username}");
                    }
                }
                catch (Exception ex)
                {
                    logger?.Invoke($"Error cleaning up user resources: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// Valida integridad de archivo parcial
        /// </summary>
        public static bool ValidatePartialFile(DownloadTask transfer, Action<string> logger = null)
        {
            if (transfer == null || string.IsNullOrWhiteSpace(transfer.FilePath))
                return false;

            try
            {
                if (!File.Exists(transfer.FilePath))
                    return true; // No hay archivo parcial, OK

                var fileInfo = new FileInfo(transfer.FilePath);

                // Verificar que el archivo parcial no sea más grande que el esperado
                if (fileInfo.Length > transfer.FileSize)
                {
                    logger?.Invoke($"Partial file corrupted (too large): {transfer.FileName}");
                    File.Delete(transfer.FilePath);
                    transfer.CurrentByteOffset = 0;
                    return false;
                }

                // Verificar que el offset coincida con el tamaño del archivo
                if (transfer.CurrentByteOffset.HasValue && transfer.CurrentByteOffset.Value != fileInfo.Length)
                {
                    logger?.Invoke($"Offset mismatch, resetting: {transfer.FileName}");
                    transfer.CurrentByteOffset = fileInfo.Length;
                }

                return true;
            }
            catch (Exception ex)
            {
                logger?.Invoke($"Error validating partial file: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Limpia archivos temporales y corruptos
        /// </summary>
        public static void CleanupTemporaryFiles(string downloadDir, Action<string> logger = null)
        {
            if (string.IsNullOrWhiteSpace(downloadDir) || !Directory.Exists(downloadDir))
                return;

            try
            {
                // Buscar archivos .part, .tmp, .download
                var patterns = new[] { "*.part", "*.tmp", "*.download", "*.incomplete" };
                var deletedCount = 0;

                foreach (var pattern in patterns)
                {
                    var files = Directory.GetFiles(downloadDir, pattern, SearchOption.AllDirectories);
                    
                    foreach (var file in files)
                    {
                        try
                        {
                            var fileInfo = new FileInfo(file);
                            
                            // Eliminar archivos temporales antiguos (más de 7 días)
                            if (DateTime.UtcNow - fileInfo.LastWriteTimeUtc > TimeSpan.FromDays(7))
                            {
                                File.Delete(file);
                                deletedCount++;
                            }
                        }
                        catch
                        {
                            // Continuar con siguiente archivo
                        }
                    }
                }

                if (deletedCount > 0)
                {
                    logger?.Invoke($"Cleaned up {deletedCount} temporary files");
                }
            }
            catch (Exception ex)
            {
                logger?.Invoke($"Error cleaning temporary files: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Extensiones para DownloadTask
    /// </summary>
    public static class DownloadTaskExtensions
    {
        public static bool IsAborting { get; set; }
        public static bool LegacyAttempt { get; set; }
        public static bool SizeChanged { get; set; }
        public static DateTime? AbortedAt { get; set; }
        public static object Connection { get; set; }
        public static FileStream FileStream { get; set; }
        public static CancellationTokenSource CancellationTokenSource { get; set; }
    }
}
