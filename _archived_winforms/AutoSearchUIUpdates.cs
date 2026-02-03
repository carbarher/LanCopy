// AutoSearchUIUpdates.cs - Sistema de actualizaciones UI en batch para búsqueda automática
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace SlskDown
{
    public partial class MainForm
    {
        /// <summary>
        /// Inicia timer para procesar actualizaciones UI en batch durante búsqueda automática
        /// </summary>
        private void StartAutoSearchUIUpdateTimer()
        {
            autoSearchUIUpdateTimer?.Dispose();
            autoSearchUIUpdateTimer = new System.Threading.Timer(_ =>
            {
                FlushAutoSearchUIUpdates();
            }, null, 1000, 1000); // Cada 1 segundo
        }
        
        /// <summary>
        /// Detiene timer de actualizaciones UI para búsqueda automática
        /// </summary>
        private void StopAutoSearchUIUpdateTimer()
        {
            autoSearchUIUpdateTimer?.Dispose();
            autoSearchUIUpdateTimer = null;
        }
        
        /// <summary>
        /// Encola actualización de UI para procesamiento en batch (búsqueda automática)
        /// </summary>
        private void QueueAutoSearchUIUpdate(string author, int filesCount, string status, Color? color = null)
        {
            pendingAutoSearchUIUpdates.Enqueue((author, filesCount, status, color));
        }
        
        /// <summary>
        /// Procesa todas las actualizaciones UI pendientes en batch (búsqueda automática)
        /// </summary>
        private void FlushAutoSearchUIUpdates()
        {
            if (pendingAutoSearchUIUpdates.IsEmpty)
                return;
            
            var updates = new List<(string author, int filesCount, string status, Color? color)>();
            
            // Extraer todas las actualizaciones pendientes
            while (pendingAutoSearchUIUpdates.TryDequeue(out var update))
            {
                updates.Add(update);
            }
            
            if (updates.Count == 0)
                return;
            
            // Aplicar actualizaciones en batch en UI thread
            SafeBeginInvoke(() =>
            {
                try
                {
                    // Validar que el ListView existe y tiene handle
                    if (lvAutoAuthors == null || !lvAutoAuthors.IsHandleCreated)
                        return;
                    
                    // Deshabilitar redibujado durante actualizaciones en batch
                    lvAutoAuthors.BeginUpdate();
                    
                    // Aplicar todas las actualizaciones
                    foreach (var (author, filesCount, status, color) in updates)
                    {
                        if (authorIndex != null && authorIndex.TryGetValue(author, out var authorData))
                        {
                            authorData.FilesCount = filesCount;
                            authorData.Status = status;
                            if (color.HasValue)
                                authorData.ForeColor = color.Value;
                        }
                    }
                    
                    // Limpiar cache SIEMPRE para forzar actualización visible
                    if (itemCache != null)
                    {
                        itemCache.Clear();
                        cacheStart = -1;
                        cacheEnd = -1;
                    }
                    
                    // Reactivar redibujado
                    lvAutoAuthors.EndUpdate();
                    
                    // Forzar redibujado completo del ListView
                    lvAutoAuthors.Invalidate();
                    
                    // Forzar actualización del VirtualListSize para disparar RetrieveVirtualItem
                    if (filteredAuthorsData != null)
                    {
                        lvAutoAuthors.VirtualListSize = filteredAuthorsData.Count;
                    }
                    
                    // Refrescar para asegurar que se muestran los cambios
                    lvAutoAuthors.Refresh();
                }
                catch (Exception ex)
                {
                    AutoLog($"⚠️ Error en FlushAutoSearchUIUpdates: {ex.Message}");
                }
            });
        }
    }
}
