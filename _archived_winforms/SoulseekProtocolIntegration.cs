using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Soulseek;

namespace SlskDown
{
    public class SoulseekProtocolIntegration
    {
        private SoulseekClient client;
        private InterestsSystem interestsSystem;
        private PrivilegedUsersManager privilegedUsersManager;
        private QueueManagementSystem queueManagement;
        private AdvancedProtocolManager advancedProtocol;
        private PrivateRoomsManager privateRooms;
        private Action<string> logAction;
        
        public SoulseekProtocolIntegration(
            SoulseekClient slskClient,
            InterestsSystem interests,
            PrivilegedUsersManager privileged,
            QueueManagementSystem queue,
            AdvancedProtocolManager protocol,
            PrivateRoomsManager rooms,
            Action<string> logger)
        {
            client = slskClient;
            interestsSystem = interests;
            privilegedUsersManager = privileged;
            queueManagement = queue;
            advancedProtocol = protocol;
            privateRooms = rooms;
            logAction = logger;
        }
        
        // ═══════════════════════════════════════════════════════════════
        // INTERESES (Server Code 51, 52, 57, 110)
        // ═══════════════════════════════════════════════════════════════
        
        public async Task SendAddInterest(string interest, bool isLiked)
        {
            try
            {
                if (isLiked)
                {
                    // Server Code 51: AddThingILike
                    await client.AddInterestAsync(interest);
                    interestsSystem.AddLikedInterest(interest);
                    logAction?.Invoke($"Interés agregado al servidor: {interest}");
                }
                else
                {
                    // Server Code 117: AddThingIHate
                    await client.RemoveInterestAsync(interest);
                    interestsSystem.AddHatedInterest(interest);
                    logAction?.Invoke($"Interés rechazado en servidor: {interest}");
                }
            }
            catch (Exception ex)
            {
                logAction?.Invoke($"Error enviando interés: {ex.Message}");
            }
        }
        
        public async Task SendRemoveInterest(string interest, bool wasLiked)
        {
            try
            {
                if (wasLiked)
                {
                    // Server Code 52: RemoveThingILike
                    await client.RemoveInterestAsync(interest);
                    interestsSystem.RemoveLikedInterest(interest);
                }
                else
                {
                    // Server Code 118: RemoveThingIHate
                    await client.RemoveInterestAsync(interest);
                    interestsSystem.RemoveHatedInterest(interest);
                }
                
                logAction?.Invoke($"Interés eliminado del servidor: {interest}");
            }
            catch (Exception ex)
            {
                logAction?.Invoke($"Error eliminando interés: {ex.Message}");
            }
        }
        
        public async Task RequestUserInterests(string username)
        {
            try
            {
                // Server Code 57: UserInterests
                var interests = await client.GetUserInterestsAsync(username);
                
                logAction?.Invoke($"Intereses de {username}: {interests.Likes?.Count ?? 0} likes, {interests.Hates?.Count ?? 0} hates");
                
                // Calcular similitud
                if (interests.Likes != null && interests.Hates != null)
                {
                    int similarity = interestsSystem.CalculateSimilarity(
                        interests.Likes.ToList(),
                        interests.Hates.ToList()
                    );
                    
                    logAction?.Invoke($"   Similitud con {username}: {similarity}%");
                }
            }
            catch (Exception ex)
            {
                logAction?.Invoke($"Error obteniendo intereses de {username}: {ex.Message}");
            }
        }
        
        public async Task RequestSimilarUsers()
        {
            try
            {
                // Server Code 110: SimilarUsers
                var recommendations = await client.GetRecommendationsAsync();
                
                if (recommendations?.Recommendations != null)
                {
                    var similarUsers = recommendations.Recommendations
                        .Select(r => new SimilarUser
                        {
                            Username = r.Username,
                            SimilarityScore = r.Score,
                            CommonInterests = new List<string>() // TODO: Obtener intereses comunes
                        })
                        .ToList();
                    
                    interestsSystem.UpdateSimilarUsers(similarUsers);
                    logAction?.Invoke($"{similarUsers.Count} usuarios similares actualizados");
                }
            }
            catch (Exception ex)
            {
                logAction?.Invoke($"Error obteniendo usuarios similares: {ex.Message}");
            }
        }
        
        public async Task RequestGlobalRecommendations()
        {
            try
            {
                // Server Code 56: GlobalRecommendations
                var recommendations = await client.GetRecommendationsAsync();
                
                if (recommendations?.Recommendations != null)
                {
                    var globalRecs = recommendations.Recommendations
                        .Select(r => new Recommendation
                        {
                            Item = r.Username,
                            Score = r.Score,
                            Source = "Global"
                        })
                        .ToList();
                    
                    interestsSystem.UpdateGlobalRecommendations(globalRecs);
                    logAction?.Invoke($"{globalRecs.Count} recomendaciones globales actualizadas");
                }
            }
            catch (Exception ex)
            {
                logAction?.Invoke($"Error obteniendo recomendaciones globales: {ex.Message}");
            }
        }
        
        // ═══════════════════════════════════════════════════════════════
        // PRIVILEGIOS (Server Code 69, 92, 123, 124)
        // ═══════════════════════════════════════════════════════════════
        
        public async Task RequestPrivilegedUsers()
        {
            try
            {
                // Server Code 69: PrivilegedUsers
                var privileged = await client.GetPrivilegedUsersAsync();
                
                if (privileged != null)
                {
                    privilegedUsersManager.UpdatePrivilegedUsersList(privileged.ToList());
                    logAction?.Invoke($"{privileged.Count()} usuarios privilegiados actualizados");
                }
            }
            catch (Exception ex)
            {
                logAction?.Invoke($"Error obteniendo usuarios privilegiados: {ex.Message}");
            }
        }
        
        public async Task CheckMyPrivileges()
        {
            try
            {
                // Server Code 92: CheckPrivileges
                var privileges = await client.GetPrivilegesAsync();
                
                if (privileges != null)
                {
                    int daysRemaining = privileges.TimeRemaining / (24 * 3600);
                    privilegedUsersManager.UpdateMyPrivileges(daysRemaining);
                    logAction?.Invoke($"Privilegios verificados: {daysRemaining} días restantes");
                }
            }
            catch (Exception ex)
            {
                logAction?.Invoke($"Error verificando privilegios: {ex.Message}");
            }
        }
        
        public async Task GivePrivileges(string username, int days)
        {
            try
            {
                // Server Code 123: GivePrivileges
                await client.GivePrivilegesAsync(username, days);
                
                privilegedUsersManager.RecordGiftSent(username, days);
                logAction?.Invoke($"Privilegios enviados a {username}: {days} días");
            }
            catch (Exception ex)
            {
                logAction?.Invoke($"Error enviando privilegios: {ex.Message}");
            }
        }
        
        public void OnPrivilegesReceived(string fromUsername, int days)
        {
            // Server Code 124: NotifyPrivileges
            privilegedUsersManager.RecordGiftReceived(fromUsername, days);
            logAction?.Invoke($"Privilegios recibidos de {fromUsername}: {days} días");
        }
        
        // ═══════════════════════════════════════════════════════════════
        // PLACE IN LINE (Server Code 59/60)
        // ═══════════════════════════════════════════════════════════════
        
        public async Task RequestPlaceInLine(string username, string filename)
        {
            try
            {
                // Server Code 59: PlaceInLineRequest
                var place = await client.GetDownloadPlaceInQueueAsync(username, filename);
                
                if (place != null)
                {
                    queueManagement.UpdateQueuePosition(username, filename, place.Value, 0);
                    logAction?.Invoke($"Posición en cola: {filename} → {place.Value}");
                }
            }
            catch (Exception ex)
            {
                logAction?.Invoke($"Error obteniendo posición en cola: {ex.Message}");
            }
        }
        
        // ═══════════════════════════════════════════════════════════════
        // PRIVATE ROOMS (Server Code 133-148)
        // ═══════════════════════════════════════════════════════════════
        
        public async Task JoinPrivateRoom(string roomName)
        {
            try
            {
                await client.JoinRoomAsync(roomName);
                logAction?.Invoke($"Unido a room privado: {roomName}");
            }
            catch (Exception ex)
            {
                logAction?.Invoke($"Error uniéndose a room privado: {ex.Message}");
            }
        }
        
        public async Task LeavePrivateRoom(string roomName)
        {
            try
            {
                await client.LeaveRoomAsync(roomName);
                logAction?.Invoke($"Salido de room privado: {roomName}");
            }
            catch (Exception ex)
            {
                logAction?.Invoke($"Error saliendo de room privado: {ex.Message}");
            }
        }
        
        public async Task AddRoomMember(string roomName, string username)
        {
            try
            {
                // Server Code 134: AddRoomMember
                await client.AddPrivateRoomMemberAsync(roomName, username);
                privateRooms.AddMember(roomName, username);
                logAction?.Invoke($"➕ Miembro agregado a {roomName}: {username}");
            }
            catch (Exception ex)
            {
                logAction?.Invoke($"Error agregando miembro: {ex.Message}");
            }
        }
        
        public async Task RemoveRoomMember(string roomName, string username)
        {
            try
            {
                // Server Code 135: RemoveRoomMember
                await client.RemovePrivateRoomMemberAsync(roomName, username);
                privateRooms.RemoveMember(roomName, username);
                logAction?.Invoke($"➖ Miembro eliminado de {roomName}: {username}");
            }
            catch (Exception ex)
            {
                logAction?.Invoke($"Error eliminando miembro: {ex.Message}");
            }
        }
        
        public async Task AddRoomOperator(string roomName, string username)
        {
            try
            {
                // Server Code 143: AddRoomOperator
                await client.AddPrivateRoomOperatorAsync(roomName, username);
                privateRooms.AddOperator(roomName, username);
                logAction?.Invoke($"👮 Operador agregado a {roomName}: {username}");
            }
            catch (Exception ex)
            {
                logAction?.Invoke($"Error agregando operador: {ex.Message}");
            }
        }
        
        public async Task RemoveRoomOperator(string roomName, string username)
        {
            try
            {
                // Server Code 144: RemoveRoomOperator
                await client.RemovePrivateRoomOperatorAsync(roomName, username);
                privateRooms.RemoveOperator(roomName, username);
                logAction?.Invoke($"👮 Operador eliminado de {roomName}: {username}");
            }
            catch (Exception ex)
            {
                logAction?.Invoke($"Error eliminando operador: {ex.Message}");
            }
        }
        
        // ═══════════════════════════════════════════════════════════════
        // BÚSQUEDA EXACTA (Server Code 65)
        // ═══════════════════════════════════════════════════════════════
        
        public async Task<List<Soulseek.File>> ExactFileSearch(string filename, string folder, long size)
        {
            try
            {
                // Server Code 65: ExactFileSearch
                var searchOptions = new SearchOptions(
                    searchTimeout: 30,
                    responseLimit: 100,
                    filterResponses: true
                );
                
                var query = $"\"{filename}\"";
                var results = await client.SearchAsync(SearchQuery.FromText(query), searchOptions);
                
                // Filtrar por tamaño exacto
                var exactMatches = results.Responses
                    .SelectMany(r => r.Files)
                    .Where(f => f.Size == size && f.Filename.Contains(filename))
                    .ToList();
                
                logAction?.Invoke($"Búsqueda exacta: {exactMatches.Count} resultados para {filename}");
                
                return exactMatches;
            }
            catch (Exception ex)
            {
                logAction?.Invoke($"Error en búsqueda exacta: {ex.Message}");
                return new List<Soulseek.File>();
            }
        }
        
        // ═══════════════════════════════════════════════════════════════
        // EVENTOS Y CALLBACKS
        // ═══════════════════════════════════════════════════════════════
        
        public void SetupEventHandlers()
        {
            // Privilegios recibidos
            client.PrivilegeNotificationReceived += (sender, e) =>
            {
                OnPrivilegesReceived(e.Username, e.Id);
            };
            
            // Usuario privilegiado actualizado
            client.UserStatusChanged += async (sender, e) =>
            {
                if (e.IsPrivileged)
                {
                    await RequestPrivilegedUsers();
                }
            };
            
            logAction?.Invoke("Event handlers del protocolo configurados");
        }
    }
}
