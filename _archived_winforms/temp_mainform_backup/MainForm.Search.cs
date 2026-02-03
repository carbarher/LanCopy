using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Soulseek;

namespace SlskDown
{
    /// <summary>
    /// MainForm - Partial class para operaciones de búsqueda
    /// </summary>
    public partial class MainForm
    {
        // Variables de búsqueda (se moverán aquí gradualmente)
        private CancellationTokenSource? _searchCancellationTokenSource;
        private readonly List<dynamic> _allResults = new();
        
        /// <summary>
        /// Inicia una búsqueda con los parámetros especificados
        /// </summary>
        private async Task StartSearchAsync(string query, SearchOptions options)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                Log("⚠️ La búsqueda no puede estar vacía");
                return;
            }
            
            if (client == null || !client.State.HasFlag(SoulseekClientStates.Connected))
            {
                Log("⚠️ Debes conectarte primero");
                return;
            }
            
            // Cancelar búsqueda anterior si existe
            _searchCancellationTokenSource?.Cancel();
            _searchCancellationTokenSource = new CancellationTokenSource();
            
            try
            {
                Log($"🔍 Iniciando búsqueda: {query}");
                UpdateControlEnabled(btnSearch, false);
                UpdateControlEnabled(btnStopSearch, true);
                UpdateControlText(lblStatus, "Buscando...");
                
                // La búsqueda real se mantiene en MainForm.cs por ahora
                // Esta es la estructura para futuras mejoras
                
                await Task.CompletedTask;
            }
            catch (OperationCanceledException)
            {
                Log("⏹️ Búsqueda cancelada");
            }
            catch (Exception ex)
            {
                Log($"❌ Error en búsqueda: {ex.Message}");
            }
            finally
            {
                UpdateControlEnabled(btnSearch, true);
                UpdateControlEnabled(btnStopSearch, false);
            }
        }
        
        // StopSearch movido a MainForm.cs principal
        
        /// <summary>
        /// Filtra resultados por tamaño
        /// </summary>
        private bool FilterBySize(long fileSize, long minSize, long maxSize)
        {
            if (minSize > 0 && fileSize < minSize)
                return false;
                
            if (maxSize < long.MaxValue && fileSize > maxSize)
                return false;
                
            return true;
        }
        
        /// <summary>
        /// Filtra resultados por extensión
        /// </summary>
        private bool FilterByExtension(string filename, string extensionFilter)
        {
            if (string.IsNullOrEmpty(extensionFilter) || extensionFilter == "Todos")
                return true;
                
            var ext = System.IO.Path.GetExtension(filename).ToLowerInvariant();
            return MatchesCategory(ext, extensionFilter);
        }
        
        /// <summary>
        /// Verifica si un archivo está en la blacklist
        /// </summary>
        private bool IsBlacklisted(string username)
        {
            return blacklist.Contains(username, StringComparer.OrdinalIgnoreCase);
        }
        
        /// <summary>
        /// Formatea el tamaño de archivo para mostrar
        /// </summary>
        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }
            
            return $"{len:0.##} {sizes[order]}";
        }
        
        // ClearSearchResults movido a MainForm.cs principal
        
        /// <summary>
        /// Exporta resultados a CSV
        /// </summary>
        private async Task ExportResultsToCSVAsync(string filePath)
        {
            try
            {
                var lines = new List<string>
                {
                    "Usuario,Archivo,Tamaño,Extensión,Carpeta"
                };
                
                foreach (var item in _allResults)
                {
                    // Implementación de exportación
                    // Se completará según necesidades
                }
                
                await System.IO.File.WriteAllLinesAsync(filePath, lines);
                Log($"✅ Resultados exportados a: {filePath}");
            }
            catch (Exception ex)
            {
                Log($"❌ Error exportando resultados: {ex.Message}");
            }
        }
    }
    
}
