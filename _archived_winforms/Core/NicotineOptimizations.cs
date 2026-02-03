using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using SlskDown.Models;

namespace SlskDown.Core
{
    /// <summary>
    /// Optimizaciones inspiradas en Nicotine+ para mejorar estabilidad y rendimiento
    /// Basado en análisis del código fuente de Nicotine+ (transfers.py)
    /// </summary>
    public class NicotineOptimizations
    {
        // Gestión de usuarios por estado (patrón de Nicotine+)
        private readonly Dictionary<string, List<DownloadTask>> queuedUsers = new();
        private readonly Dictionary<string, List<DownloadTask>> activeUsers = new();
        private readonly Dictionary<string, List<DownloadTask>> failedUsers = new();
        
        // Límites de cola por usuario (patrón de Nicotine+)
        private readonly Dictionary<string, int> userQueueLimits = new();
        private readonly Dictionary<string, int> userQueueSizes = new();
        
        // Usuarios en línea (para watch)
        private readonly HashSet<string> onlineUsers = new();
        
        // Timer para auto-save cada 3 minutos
        private System.Threading.Timer autoSaveTimer;
        
        // Callback para guardar transferencias
        private readonly Action saveTransfersCallback;
        
        // Constantes de Nicotine+
        private const int MAX_DOWNLOADS_PER_USER = 3;
        private const int AUTO_SAVE_INTERVAL_SECONDS = 180; // 3 minutos
        
        public NicotineOptimizations(Action saveCallback)
        {
            saveTransfersCallback = saveCallback;
            InitializeAutoSave();
        }
        
        /// <summary>
        /// Inicializa el timer de auto-save cada 3 minutos (patrón de Nicotine+)
        /// </summary>
        private void InitializeAutoSave()
        {
            autoSaveTimer = new System.Threading.Timer(
                callback: _ => saveTransfersCallback?.Invoke(),
                state: null,
                dueTime: TimeSpan.FromSeconds(AUTO_SAVE_INTERVAL_SECONDS),
                period: TimeSpan.FromSeconds(AUTO_SAVE_INTERVAL_SECONDS)
            );
        }
        
        /// <summary>
        /// Configura TCP Keepalive en un socket (reemplaza ServerPing obsoleto)
        /// Nicotine+ usa TCP keepalive en lugar de ServerPing
        /// </summary>
        public static void ConfigureTcpKeepalive(Socket socket)
        {
            if (socket == null || !socket.Connected)
                return;
                
            try
            {
                // Habilitar TCP keepalive
                socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                
                // Tiempo antes del primer keepalive: 60 segundos
                socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, 60);
                
                // Intervalo entre keepalives: 10 segundos
                socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval, 10);
                
                // Número de reintentos antes de considerar conexión muerta
                socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveRetryCount, 5);
            }
            catch (Exception)
            {
                // Algunas plataformas no soportan todas las opciones
            }
        }
        
        /// <summary>
        /// Agrega una transferencia a la gestión por estado de usuario
        /// </summary>
        public void AddTransfer(DownloadTask task)
        {
            if (task == null || string.IsNullOrEmpty(task.Username))
                return;
                
            var username = task.Username;
            
            switch (task.Status)
            {
                case DownloadStatus.Queued:
                case DownloadStatus.GettingStatus:
                    if (!queuedUsers.ContainsKey(username))
                        queuedUsers[username] = new List<DownloadTask>();
                    queuedUsers[username].Add(task);
                    break;
                    
                case DownloadStatus.Downloading:
                    if (!activeUsers.ContainsKey(username))
                        activeUsers[username] = new List<DownloadTask>();
                    activeUsers[username].Add(task);
                    break;
                    
                case DownloadStatus.Failed:
                case DownloadStatus.ConnectionClosed:
                case DownloadStatus.ConnectionTimeout:
                case DownloadStatus.UserLoggedOff:
                    if (!failedUsers.ContainsKey(username))
                        failedUsers[username] = new List<DownloadTask>();
                    failedUsers[username].Add(task);
                    break;
            }
            
            UpdateUserQueueSize(username);
        }
        
        /// <summary>
        /// Mueve una transferencia entre estados
        /// </summary>
        public void MoveTransfer(DownloadTask task, DownloadStatus newStatus)
        {
            if (task == null || string.IsNullOrEmpty(task.Username))
                return;
                
            RemoveTransfer(task);
            task.Status = newStatus;
            AddTransfer(task);
        }
        
        /// <summary>
        /// Elimina una transferencia de todos los estados
        /// </summary>
        public void RemoveTransfer(DownloadTask task)
        {
            if (task == null || string.IsNullOrEmpty(task.Username))
                return;
                
            var username = task.Username;
            
            queuedUsers.GetValueOrDefault(username)?.Remove(task);
            activeUsers.GetValueOrDefault(username)?.Remove(task);
            failedUsers.GetValueOrDefault(username)?.Remove(task);
            
            UpdateUserQueueSize(username);
        }
        
        /// <summary>
        /// Actualiza el tamaño de la cola de un usuario
        /// </summary>
        private void UpdateUserQueueSize(string username)
        {
            int totalSize = 0;
            
            if (queuedUsers.ContainsKey(username))
                totalSize += queuedUsers[username].Count;
            if (activeUsers.ContainsKey(username))
                totalSize += activeUsers[username].Count;
                
            userQueueSizes[username] = totalSize;
        }
        
        /// <summary>
        /// Verifica si un usuario puede aceptar más descargas
        /// </summary>
        public bool CanAcceptMoreDownloads(string username)
        {
            if (string.IsNullOrEmpty(username))
                return false;
                
            int currentActive = activeUsers.GetValueOrDefault(username)?.Count ?? 0;
            int limit = userQueueLimits.GetValueOrDefault(username, MAX_DOWNLOADS_PER_USER);
            
            return currentActive < limit;
        }
        
        /// <summary>
        /// Obtiene el número de descargas activas de un usuario
        /// </summary>
        public int GetActiveDownloadsCount(string username)
        {
            return activeUsers.GetValueOrDefault(username)?.Count ?? 0;
        }
        
        /// <summary>
        /// Obtiene todas las transferencias de un usuario
        /// </summary>
        public IEnumerable<DownloadTask> GetUserTransfers(string username)
        {
            var transfers = new List<DownloadTask>();
            
            if (queuedUsers.ContainsKey(username))
                transfers.AddRange(queuedUsers[username]);
            if (activeUsers.ContainsKey(username))
                transfers.AddRange(activeUsers[username]);
            if (failedUsers.ContainsKey(username))
                transfers.AddRange(failedUsers[username]);
                
            return transfers;
        }
        
        /// <summary>
        /// Marca un usuario como en línea
        /// </summary>
        public void SetUserOnline(string username)
        {
            if (!string.IsNullOrEmpty(username))
                onlineUsers.Add(username);
        }
        
        /// <summary>
        /// Marca un usuario como fuera de línea y pausa sus transferencias
        /// </summary>
        public void SetUserOffline(string username)
        {
            if (string.IsNullOrEmpty(username))
                return;
                
            onlineUsers.Remove(username);
            
            // Pausar todas las transferencias activas de este usuario
            if (activeUsers.ContainsKey(username))
            {
                foreach (var task in activeUsers[username].ToList())
                {
                    MoveTransfer(task, DownloadStatus.UserLoggedOff);
                }
            }
        }
        
        /// <summary>
        /// Verifica si un usuario está en línea
        /// </summary>
        public bool IsUserOnline(string username)
        {
            return onlineUsers.Contains(username);
        }
        
        /// <summary>
        /// Limpia todos los estados al desconectar del servidor
        /// </summary>
        public void ClearAllOnDisconnect()
        {
            queuedUsers.Clear();
            activeUsers.Clear();
            failedUsers.Clear();
            onlineUsers.Clear();
            userQueueLimits.Clear();
            userQueueSizes.Clear();
        }
        
        /// <summary>
        /// Obtiene estadísticas de transferencias
        /// </summary>
        public (int queued, int active, int failed) GetStatistics()
        {
            int queued = queuedUsers.Values.Sum(list => list.Count);
            int active = activeUsers.Values.Sum(list => list.Count);
            int failed = failedUsers.Values.Sum(list => list.Count);
            
            return (queued, active, failed);
        }
        
        /// <summary>
        /// Detiene el auto-save timer
        /// </summary>
        public void Dispose()
        {
            autoSaveTimer?.Dispose();
            autoSaveTimer = null;
        }
    }
    
    /// <summary>
    /// Atributos de archivo en formato JSON (patrón de Nicotine+ 3.3.0+)
    /// </summary>
    public class FileAttributes
    {
        public int? Bitrate { get; set; }
        public int? Length { get; set; }        // Duración en segundos
        public bool? IsVBR { get; set; }
        public int? SampleRate { get; set; }
        public int? BitDepth { get; set; }
        
        /// <summary>
        /// Convierte de formato legacy (string) a formato moderno (JSON)
        /// </summary>
        public static FileAttributes FromLegacyString(string bitrateStr, string lengthStr)
        {
            var attrs = new FileAttributes();
            
            if (!string.IsNullOrEmpty(bitrateStr))
            {
                bool isVbr = bitrateStr.Contains("(vbr)", StringComparison.OrdinalIgnoreCase);
                string cleanBitrate = bitrateStr.Replace(" (vbr)", "").Replace(" (VBR)", "").Trim();
                
                if (int.TryParse(cleanBitrate, out int bitrate))
                {
                    attrs.Bitrate = bitrate;
                    attrs.IsVBR = isVbr;
                }
            }
            
            if (!string.IsNullOrEmpty(lengthStr) && lengthStr.Contains(":"))
            {
                // Convertir HH:mm:ss a segundos
                int seconds = 0;
                foreach (var part in lengthStr.Split(':'))
                {
                    if (int.TryParse(part, out int value))
                        seconds = seconds * 60 + value;
                }
                attrs.Length = seconds;
            }
            
            return attrs;
        }
        
        /// <summary>
        /// Formatea para mostrar en UI
        /// </summary>
        public string ToDisplayString()
        {
            var parts = new List<string>();
            
            if (Bitrate.HasValue)
            {
                string bitrateStr = $"{Bitrate} kbps";
                if (IsVBR == true)
                    bitrateStr += " (VBR)";
                parts.Add(bitrateStr);
            }
            
            if (Length.HasValue)
            {
                int seconds = Length.Value;
                int hours = seconds / 3600;
                int minutes = (seconds % 3600) / 60;
                int secs = seconds % 60;
                
                if (hours > 0)
                    parts.Add($"{hours:D2}:{minutes:D2}:{secs:D2}");
                else
                    parts.Add($"{minutes:D2}:{secs:D2}");
            }
            
            if (SampleRate.HasValue)
                parts.Add($"{SampleRate / 1000.0:F1} kHz");
                
            if (BitDepth.HasValue)
                parts.Add($"{BitDepth}-bit");
            
            return string.Join(" | ", parts);
        }
    }
}
