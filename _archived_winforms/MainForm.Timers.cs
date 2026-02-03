using System;
using System.Linq;
using System.Windows.Forms;
using SlskDown.Models;

namespace SlskDown
{
    public partial class MainForm
    {
        /// <summary>
        /// Inicializa timers de mantenimiento para prevenir memory leaks y mantener la aplicación saludable
        /// </summary>
        private void InitializeMaintenanceTimers()
        {
            // Timer de limpieza de memoria (cada hora)
            var memoryCleanupTimer = new System.Windows.Forms.Timer();
            memoryCleanupTimer.Interval = 60 * 60 * 1000; // 1 hora
            memoryCleanupTimer.Tick += (s, e) =>
            {
                try
                {
                    CleanupAutoSearchResults();
                }
                catch (Exception ex)
                {
                    Log($"❌ Error en limpieza de memoria: {ex.Message}");
                }
            };
            memoryCleanupTimer.Start();
            Log("✅ Timer de limpieza de memoria iniciado (cada 1 hora)");
            
            // Timer de watchlist (cada hora)
            if (watchlistTimer == null)
            {
                watchlistTimer = new System.Windows.Forms.Timer();
                watchlistTimer.Interval = 60 * 60 * 1000; // 1 hora
                watchlistTimer.Tick += async (s, e) =>
                {
                    try
                    {
                        await CheckWatchlist();
                    }
                    catch (Exception ex)
                    {
                        Log($"❌ Error en watchlist timer: {ex.Message}");
                    }
                };
                watchlistTimer.Start();
                Log("✅ Timer de watchlist iniciado (cada 1 hora)");
            }
        }
        
        /// <summary>
        /// Limpia autoSearchResults para prevenir memory leaks.
        /// Mantiene solo los resultados de las últimas 24 horas y limita a 100,000 items máximo.
        /// </summary>
        private void CleanupAutoSearchResults()
        {
            try
            {
                int initialCount = autoSearchResults.Count;
                
                if (initialCount == 0)
                {
                    Log("🧹 Limpieza de memoria: autoSearchResults ya está vacío");
                    return;
                }
                
                // Convertir a lista para poder filtrar
                var allResults = autoSearchResults.ToList();
                
                // Filtrar: mantener solo últimas 24 horas
                var cutoffTime = DateTime.Now.AddHours(-24);
                var recentResults = allResults.Where(r => r.Timestamp > cutoffTime).ToList();
                
                // Limitar a 100,000 items máximo (ordenados por timestamp descendente)
                const int MAX_ITEMS = 100_000;
                if (recentResults.Count > MAX_ITEMS)
                {
                    recentResults = recentResults
                        .OrderByDescending(r => r.Timestamp)
                        .Take(MAX_ITEMS)
                        .ToList();
                }
                
                // Recrear el ConcurrentBag con los resultados filtrados
                autoSearchResults = new System.Collections.Concurrent.ConcurrentBag<AutoSearchFileResult>(recentResults);
                
                int removedCount = initialCount - autoSearchResults.Count;
                
                if (removedCount > 0)
                {
                    Log($"🧹 Limpieza de memoria completada:");
                    Log($"   - Items eliminados: {removedCount:N0}");
                    Log($"   - Items restantes: {autoSearchResults.Count:N0}");
                    Log($"   - Memoria liberada: ~{removedCount * 256 / 1024:N0} KB");
                }
                else
                {
                    Log($"🧹 Limpieza de memoria: sin cambios ({initialCount:N0} items)");
                }
                
                // Forzar recolección de basura si se liberó mucha memoria
                if (removedCount > 10000)
                {
                    GC.Collect(2, GCCollectionMode.Optimized);
                    Log("   - GC ejecutado para liberar memoria");
                }
            }
            catch (Exception ex)
            {
                Log($"❌ Error en CleanupAutoSearchResults: {ex.Message}");
            }
        }
    }
}
