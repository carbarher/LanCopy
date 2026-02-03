using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SlskDown.Core
{
    /// <summary>
    /// MEJORA #7 (Nicotine+ 3.3.0): Graceful Shutdown
    /// Espera a que terminen uploads activos antes de cerrar la aplicación
    /// </summary>
    public class GracefulShutdownManager
    {
        private readonly Func<List<object>> getActiveUploads;
        private readonly Func<List<object>> getActiveDownloads;
        private readonly Action<string> log;
        
        public bool WaitForUploads { get; set; } = true;
        public bool WaitForDownloads { get; set; } = false; // Opcional
        public int MaxWaitMinutes { get; set; } = 5;

        public GracefulShutdownManager(
            Func<List<object>> getActiveUploads,
            Func<List<object>> getActiveDownloads,
            Action<string> log)
        {
            this.getActiveUploads = getActiveUploads;
            this.getActiveDownloads = getActiveDownloads;
            this.log = log;
        }

        /// <summary>
        /// Intenta cerrar la aplicación de forma elegante
        /// </summary>
        /// <returns>True si se debe proceder con el cierre, False si se canceló</returns>
        public async Task<bool> TryShutdownAsync()
        {
            var activeUploads = getActiveUploads?.Invoke() ?? new List<object>();
            var activeDownloads = getActiveDownloads?.Invoke() ?? new List<object>();

            // Si no hay nada activo, cerrar inmediatamente
            if (activeUploads.Count == 0 && (!WaitForDownloads || activeDownloads.Count == 0))
            {
                log?.Invoke("✅ No hay transferencias activas, cerrando...");
                return true;
            }

            // Construir mensaje
            var message = BuildShutdownMessage(activeUploads.Count, activeDownloads.Count);
            
            // Mostrar diálogo
            var result = MessageBox.Show(
                message,
                "Cerrar SlskDown",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button3 // Default: Cancel
            );

            switch (result)
            {
                case DialogResult.Yes:
                    // Esperar a que terminen
                    log?.Invoke($"⏳ Esperando a que terminen {activeUploads.Count} subidas...");
                    await WaitForTransfersAsync(activeUploads, activeDownloads);
                    return true;

                case DialogResult.No:
                    // Cerrar inmediatamente sin esperar
                    log?.Invoke("⚠️ Cerrando sin esperar transferencias...");
                    return true;

                case DialogResult.Cancel:
                default:
                    // No cerrar
                    log?.Invoke("Cierre cancelado por el usuario");
                    return false;
            }
        }

        private string BuildShutdownMessage(int uploads, int downloads)
        {
            var parts = new List<string>();

            if (uploads > 0)
                parts.Add($"{uploads} subida{(uploads > 1 ? "s" : "")} activa{(uploads > 1 ? "s" : "")}");

            if (WaitForDownloads && downloads > 0)
                parts.Add($"{downloads} descarga{(downloads > 1 ? "s" : "")} activa{(downloads > 1 ? "s" : "")}");

            var transfersText = string.Join(" y ", parts);

            return $"Hay {transfersText}.\n\n" +
                   $"¿Qué deseas hacer?\n\n" +
                   $"• SÍ: Esperar hasta {MaxWaitMinutes} minutos a que terminen\n" +
                   $"• NO: Cerrar inmediatamente (se interrumpirán)\n" +
                   $"• CANCELAR: No cerrar la aplicación";
        }

        private async Task WaitForTransfersAsync(List<object> uploads, List<object> downloads)
        {
            var startTime = DateTime.Now;
            var maxWait = TimeSpan.FromMinutes(MaxWaitMinutes);

            while ((DateTime.Now - startTime) < maxWait)
            {
                // Actualizar listas
                uploads = getActiveUploads?.Invoke() ?? new List<object>();
                downloads = WaitForDownloads ? (getActiveDownloads?.Invoke() ?? new List<object>()) : new List<object>();

                // Si ya no hay transferencias activas, salir
                if (uploads.Count == 0 && downloads.Count == 0)
                {
                    log?.Invoke("✅ Todas las transferencias completadas");
                    return;
                }

                // Log progreso cada 10 segundos
                var elapsed = DateTime.Now - startTime;
                if (elapsed.TotalSeconds % 10 < 1)
                {
                    var remaining = maxWait - elapsed;
                    log?.Invoke($"⏳ Esperando... {uploads.Count} subidas, {downloads.Count} descargas " +
                               $"(quedan {remaining.TotalMinutes:F1} min)");
                }

                // Esperar 1 segundo antes de verificar de nuevo
                await Task.Delay(1000);
            }

            // Timeout alcanzado
            log?.Invoke($"⚠️ Timeout de {MaxWaitMinutes} minutos alcanzado, cerrando de todas formas...");
        }

        /// <summary>
        /// Versión sincrónica para usar en FormClosing event
        /// </summary>
        public bool TryShutdown()
        {
            return TryShutdownAsync().GetAwaiter().GetResult();
        }
    }
}
