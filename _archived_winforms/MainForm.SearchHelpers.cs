using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Soulseek;
using SlskDown.Models;

namespace SlskDown
{
    public partial class MainForm
    {
        /// <summary>
        /// Ejecuta una búsqueda directa sin modificar el UI.
        /// Usado por CheckWatchlist para evitar disparar eventos que causan acumulación de tareas.
        /// </summary>
        private async Task ExecuteSearchAsync(string searchTerm, CancellationToken cancellationToken)
        {
            if (client == null || !client.State.HasFlag(SoulseekClientStates.Connected))
            {
                Log($"⚠️ Cliente no conectado, omitiendo búsqueda de: {searchTerm}");
                return;
            }
            
            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                return;
            }
            
            try
            {
                Log($"🔍 Vigilancia: buscando {searchTerm}...");
                
                // Configurar búsqueda con timeout de 5 segundos
                var searchOptions = new SearchOptions(
                    searchTimeout: 5000,
                    maximumPeerQueueLength: 5000,
                    filterResponses: true,
                    minimumResponseFileCount: 1,
                    minimumPeerUploadSpeed: 0
                );
                
                // Ejecutar búsqueda
                var searchResult = await client.SearchAsync(
                    SearchQuery.FromText(searchTerm),
                    options: searchOptions,
                    cancellationToken: cancellationToken
                );
                
                int totalFiles = searchResult.Responses.Sum(r => r.FileCount);
                
                Log($"✅ Vigilancia: {searchTerm} - {totalFiles} archivos encontrados");
            }
            catch (OperationCanceledException)
            {
                Log($"⏱️ Búsqueda cancelada: {searchTerm}");
                throw;
            }
            catch (Exception ex)
            {
                Log($"❌ Error en búsqueda de '{searchTerm}': {ex.Message}");
            }
        }
    }
}
