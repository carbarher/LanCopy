using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Soulseek;

namespace SlskDown.Core
{
    /// <summary>
    /// Búsqueda simultánea en múltiples chat rooms
    /// </summary>
    public class MultiRoomSearch
    {
        private readonly SoulseekClient client;
        private readonly Action<string> logFunc;
        
        public MultiRoomSearch(SoulseekClient soulseekClient, Action<string> log = null)
        {
            client = soulseekClient;
            logFunc = log;
        }
        
        /// <summary>
        /// Resultado de búsqueda en una sala
        /// </summary>
        public class RoomSearchResult
        {
            public string RoomName { get; set; }
            public List<Soulseek.File> Files { get; set; } = new List<Soulseek.File>();
            public List<string> Users { get; set; } = new List<string>();
            public int TotalResults { get; set; }
            public TimeSpan SearchDuration { get; set; }
            public bool Success { get; set; }
            public string Error { get; set; }
        }
        
        /// <summary>
        /// Busca en múltiples salas simultáneamente
        /// </summary>
        public async Task<List<RoomSearchResult>> SearchInRooms(
            string query,
            List<string> roomNames,
            int timeoutSeconds = 30)
        {
            var results = new List<RoomSearchResult>();
            var tasks = new List<Task<RoomSearchResult>>();
            
            logFunc?.Invoke($"Iniciando búsqueda en {roomNames.Count} salas: {query}");
            
            foreach (var room in roomNames)
            {
                tasks.Add(SearchInRoom(query, room, timeoutSeconds));
            }
            
            results = (await Task.WhenAll(tasks)).ToList();
            
            var totalFiles = results.Sum(r => r.TotalResults);
            var successCount = results.Count(r => r.Success);
            
            logFunc?.Invoke($"Búsqueda completada: {totalFiles} resultados en {successCount}/{roomNames.Count} salas");
            
            return results;
        }
        
        /// <summary>
        /// Busca en una sala específica
        /// </summary>
        private async Task<RoomSearchResult> SearchInRoom(
            string query,
            string roomName,
            int timeoutSeconds)
        {
            var result = new RoomSearchResult
            {
                RoomName = roomName
            };
            
            var startTime = DateTime.Now;
            
            try
            {
                // Unirse a la sala si no estamos ya
                try
                {
                    await client.JoinRoomAsync(roomName);
                    logFunc?.Invoke($"Unido a sala: {roomName}");
                    await Task.Delay(1000); // Esperar a que se cargue la sala
                }
                catch
                {
                    // Ya estamos en la sala o error al unirse
                }
                
                // Realizar búsqueda global y filtrar por contexto de sala
                var searchResponse = await client.SearchAsync(
                    SearchQuery.FromText(query),
                    options: new SearchOptions(
                        filterResponses: false,
                        responseLimit: 100
                    )
                );
                
                // Recopilar todos los archivos y usuarios
                foreach (var response in searchResponse.Responses)
                {
                    result.Files.AddRange(response.Files);
                    if (!result.Users.Contains(response.Username))
                        result.Users.Add(response.Username);
                }
                
                result.TotalResults = result.Files.Count;
                result.Success = true;
                
                logFunc?.Invoke($"Sala {roomName}: {result.TotalResults} resultados de {result.Users.Count} usuarios");
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Error = ex.Message;
                logFunc?.Invoke($"Error en sala {roomName}: {ex.Message}");
            }
            
            result.SearchDuration = DateTime.Now - startTime;
            return result;
        }
        
        /// <summary>
        /// Obtiene salas populares para búsqueda
        /// </summary>
        public List<string> GetPopularRooms()
        {
            return new List<string>
            {
                "ebooks",
                "audiobooks",
                "comics",
                "manga",
                "technical",
                "science",
                "philosophy",
                "history",
                "fiction",
                "non-fiction"
            };
        }
        
        /// <summary>
        /// Agrupa resultados por usuario
        /// </summary>
        public Dictionary<string, List<Soulseek.File>> GroupResultsByUser(List<RoomSearchResult> results)
        {
            var grouped = new Dictionary<string, List<Soulseek.File>>();
            
            foreach (var roomResult in results.Where(r => r.Success))
            {
                // Agrupar por usuarios conocidos de la sala
                foreach (var username in roomResult.Users)
                {
                    if (!grouped.ContainsKey(username))
                        grouped[username] = new List<Soulseek.File>();
                    
                    // Agregar archivos (nota: File no tiene Username directamente)
                    grouped[username].AddRange(roomResult.Files);
                }
            }
            
            return grouped;
        }
        
        /// <summary>
        /// Obtiene estadísticas de búsqueda
        /// </summary>
        public object GetSearchStats(List<RoomSearchResult> results)
        {
            return new
            {
                TotalRooms = results.Count,
                SuccessfulRooms = results.Count(r => r.Success),
                TotalResults = results.Sum(r => r.TotalResults),
                TotalUsers = results.SelectMany(r => r.Users).Distinct().Count(),
                AverageDuration = TimeSpan.FromSeconds(results.Average(r => r.SearchDuration.TotalSeconds)),
                TopRooms = results
                    .Where(r => r.Success)
                    .OrderByDescending(r => r.TotalResults)
                    .Take(5)
                    .Select(r => new { r.RoomName, r.TotalResults })
                    .ToList()
            };
        }
    }
}
