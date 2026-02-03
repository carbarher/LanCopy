using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SlskDown
{
    /// <summary>
    /// Gestor de actualizaciones UI en lotes para reducir carga en el thread de UI
    /// Mejora: Reduce actualizaciones UI de miles por segundo a ~10-20 por segundo
    /// </summary>
    public class BatchUIUpdater : IDisposable
    {
        private readonly Control uiControl;
        private readonly ConcurrentQueue<Action> pendingUpdates;
        private readonly System.Threading.Timer batchTimer;
        private readonly int batchIntervalMs;
        private readonly int maxBatchSize;
        private bool isProcessing;
        private readonly object lockObj = new object();
        
        // Métricas
        private long totalUpdatesQueued;
        private long totalBatchesProcessed;
        private long totalUpdatesExecuted;
        private DateTime lastBatchTime;
        
        public BatchUIUpdater(Control uiControl, int batchIntervalMs = 100, int maxBatchSize = 50)
        {
            this.uiControl = uiControl ?? throw new ArgumentNullException(nameof(uiControl));
            this.batchIntervalMs = batchIntervalMs;
            this.maxBatchSize = maxBatchSize;
            this.pendingUpdates = new ConcurrentQueue<Action>();
            
            // Timer para procesar lotes periódicamente
            this.batchTimer = new System.Threading.Timer(ProcessBatch, null, System.Threading.Timeout.Infinite, System.Threading.Timeout.Infinite);
            this.batchTimer.Change(batchIntervalMs, batchIntervalMs);
            this.lastBatchTime = DateTime.Now;
        }
        
        /// <summary>
        /// Encola una actualización UI para procesamiento en lote
        /// </summary>
        public void QueueUpdate(Action updateAction)
        {
            if (updateAction == null) return;

            try
            {
                if (uiControl == null || uiControl.IsDisposed || !uiControl.IsHandleCreated)
                {
                    return;
                }
            }
            catch
            {
                return;
            }
            
            pendingUpdates.Enqueue(updateAction);
            Interlocked.Increment(ref totalUpdatesQueued);
            
            // Si hay muchas actualizaciones pendientes, procesar inmediatamente
            if (pendingUpdates.Count >= maxBatchSize)
            {
                ProcessBatch(null);
            }
        }
        
        /// <summary>
        /// Procesa un lote de actualizaciones UI
        /// </summary>
        private void ProcessBatch(object state)
        {
            // Evitar procesamiento concurrente
            if (isProcessing || pendingUpdates.IsEmpty)
                return;

            try
            {
                if (uiControl == null || uiControl.IsDisposed || !uiControl.IsHandleCreated)
                {
                    return;
                }
            }
            catch
            {
                return;
            }
            
            lock (lockObj)
            {
                if (isProcessing) return;
                isProcessing = true;
            }
            
            try
            {
                var batch = new List<Action>(maxBatchSize);
                
                // Extraer lote de actualizaciones
                while (batch.Count < maxBatchSize && pendingUpdates.TryDequeue(out var action))
                {
                    batch.Add(action);
                }
                
                if (batch.Count == 0)
                {
                    isProcessing = false;
                    return;
                }
                
                // Ejecutar lote en el thread de UI
                if (uiControl.InvokeRequired)
                {
                    try
                    {
                        if (!uiControl.IsDisposed && uiControl.IsHandleCreated)
                        {
                            uiControl.BeginInvoke(new Action(() => ExecuteBatch(batch)));
                        }
                    }
                    catch
                    {
                    }
                }
                else
                {
                    ExecuteBatch(batch);
                }
                
                Interlocked.Increment(ref totalBatchesProcessed);
                lastBatchTime = DateTime.Now;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BatchUIUpdater] Error procesando lote: {ex.Message}");
            }
            finally
            {
                isProcessing = false;
            }
        }
        
        /// <summary>
        /// Ejecuta un lote de actualizaciones (debe llamarse en UI thread)
        /// </summary>
        private void ExecuteBatch(List<Action> batch)
        {
            try
            {
                // Suspender redibujado para mejor rendimiento
                if (uiControl is ListView listView)
                {
                    listView.BeginUpdate();
                }
                
                // Ejecutar todas las actualizaciones del lote
                foreach (var action in batch)
                {
                    try
                    {
                        action();
                        Interlocked.Increment(ref totalUpdatesExecuted);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[BatchUIUpdater] Error ejecutando actualización: {ex.Message}");
                    }
                }
            }
            finally
            {
                // Reanudar redibujado
                if (uiControl is ListView listView)
                {
                    listView.EndUpdate();
                }
            }
        }
        
        /// <summary>
        /// Fuerza el procesamiento inmediato de todas las actualizaciones pendientes
        /// </summary>
        public void Flush()
        {
            try
            {
                if (uiControl == null || uiControl.IsDisposed || !uiControl.IsHandleCreated)
                {
                    while (pendingUpdates.TryDequeue(out _))
                    {
                    }
                    return;
                }
            }
            catch
            {
                while (pendingUpdates.TryDequeue(out _))
                {
                }
                return;
            }

            while (!pendingUpdates.IsEmpty)
            {
                ProcessBatch(null);
                Thread.Sleep(10); // Pequeña pausa para evitar saturación
            }
        }
        
        /// <summary>
        /// Obtiene estadísticas del batch updater
        /// </summary>
        public BatchUIStats GetStats()
        {
            return new BatchUIStats
            {
                TotalUpdatesQueued = totalUpdatesQueued,
                TotalBatchesProcessed = totalBatchesProcessed,
                TotalUpdatesExecuted = totalUpdatesExecuted,
                PendingUpdates = pendingUpdates.Count,
                LastBatchTime = lastBatchTime,
                AverageBatchSize = totalBatchesProcessed > 0 
                    ? (double)totalUpdatesExecuted / totalBatchesProcessed 
                    : 0,
                ReductionRatio = totalUpdatesQueued > 0
                    ? (double)totalBatchesProcessed / totalUpdatesQueued
                    : 0
            };
        }
        
        /// <summary>
        /// Muestra reporte de estadísticas
        /// </summary>
        public string GetStatsReport()
        {
            var stats = GetStats();
            return $@"
📊 BATCH UI UPDATER - ESTADÍSTICAS
═══════════════════════════════════════
📥 Actualizaciones encoladas: {stats.TotalUpdatesQueued:N0}
📦 Lotes procesados: {stats.TotalBatchesProcessed:N0}
✅ Actualizaciones ejecutadas: {stats.TotalUpdatesExecuted:N0}
⏳ Pendientes: {stats.PendingUpdates}
📈 Tamaño promedio de lote: {stats.AverageBatchSize:F1}
⚡ Reducción de llamadas UI: {stats.ReductionRatio:P1}
🕐 Último lote: {stats.LastBatchTime:HH:mm:ss}

💡 Beneficio: {(1 - stats.ReductionRatio):P0} menos llamadas al UI thread
";
        }
        
        public void Dispose()
        {
            batchTimer?.Dispose();
            try
            {
                if (uiControl != null && !uiControl.IsDisposed && uiControl.IsHandleCreated)
                {
                    Flush(); // Procesar actualizaciones pendientes antes de cerrar
                }
                else
                {
                    while (pendingUpdates.TryDequeue(out _))
                    {
                    }
                }
            }
            catch
            {
                while (pendingUpdates.TryDequeue(out _))
                {
                }
            }
        }
    }
    
    /// <summary>
    /// Estadísticas del batch UI updater
    /// </summary>
    public class BatchUIStats
    {
        public long TotalUpdatesQueued { get; set; }
        public long TotalBatchesProcessed { get; set; }
        public long TotalUpdatesExecuted { get; set; }
        public int PendingUpdates { get; set; }
        public DateTime LastBatchTime { get; set; }
        public double AverageBatchSize { get; set; }
        public double ReductionRatio { get; set; }
    }
}
