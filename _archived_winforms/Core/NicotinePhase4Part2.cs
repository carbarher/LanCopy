using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace SlskDown.Core
{
    // ═══════════════════════════════════════════════════════════════
    // FASE 4 - PARTE 2: CARACTERÍSTICAS ADICIONALES
    // ═══════════════════════════════════════════════════════════════
    
    // ═══════════════════════════════════════════════════════════════
    // 11. COMANDOS DE SALA DE CHAT
    // ═══════════════════════════════════════════════════════════════
    
    public class RoomCommands
    {
        private bool isAway = false;
        private string awayMessage = "";
        
        public string ProcessCommand(string command, string room, string username)
        {
            if (!command.StartsWith("/"))
                return null;
            
            var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                return null;
            
            var cmd = parts[0].ToLower();
            var args = string.Join(" ", parts.Skip(1));
            
            return cmd switch
            {
                "/me" => $"* {username} {args}",
                "/away" => SetAway(args),
                "/back" => SetBack(),
                "/join" => JoinRoom(args),
                "/leave" => LeaveRoom(room),
                "/users" => GetRoomUsers(room),
                "/clear" => ClearChat(),
                "/help" => GetHelp(),
                "/topic" => GetTopic(room),
                "/whois" => WhoisUser(args),
                _ => $"Comando desconocido: {cmd}. Usa /help para ver comandos disponibles."
            };
        }
        
        private string SetAway(string message)
        {
            isAway = true;
            awayMessage = message;
            return $"Estado cambiado a ausente: {message}";
        }
        
        private string SetBack()
        {
            isAway = false;
            awayMessage = "";
            return "Estado cambiado a disponible";
        }
        
        private string JoinRoom(string roomName)
        {
            if (string.IsNullOrWhiteSpace(roomName))
                return "Especifica el nombre de la sala";
            return $"Uniéndose a sala: {roomName}";
        }
        
        private string LeaveRoom(string roomName)
        {
            return $"Saliendo de sala: {roomName}";
        }
        
        private string GetRoomUsers(string room)
        {
            return $"Solicitando lista de usuarios en {room}...";
        }
        
        private string ClearChat()
        {
            return "[CLEAR_CHAT]";
        }
        
        private string GetTopic(string room)
        {
            return $"Solicitando tema de {room}...";
        }
        
        private string WhoisUser(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
                return "Especifica un nombre de usuario";
            return $"Solicitando información de {username}...";
        }
        
        private string GetHelp()
        {
            return @"📖 COMANDOS DISPONIBLES:
/me <acción> - Acción en tercera persona
/away <mensaje> - Establecer estado ausente
/back - Volver a disponible
/join <sala> - Unirse a una sala
/leave - Salir de la sala actual
/users - Listar usuarios en la sala
/topic - Ver tema de la sala
/whois <usuario> - Información de usuario
/clear - Limpiar chat
/help - Mostrar esta ayuda";
        }
        
        public bool IsAway => isAway;
        public string AwayMessage => awayMessage;
    }
    
    // ═══════════════════════════════════════════════════════════════
    // 12. FILTROS DE MENSAJES
    // ═══════════════════════════════════════════════════════════════
    
    public class MessageFilter
    {
        private HashSet<string> bannedWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private HashSet<string> mutedUsers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private bool filterSpam = true;
        private bool filterCapsLock = true;
        private bool filterRepetition = true;
        private readonly string configFile;
        
        public MessageFilter(string configFile)
        {
            this.configFile = configFile;
            LoadConfig();
        }
        
        public void AddBannedWord(string word)
        {
            bannedWords.Add(word);
            SaveConfig();
        }
        
        public void RemoveBannedWord(string word)
        {
            bannedWords.Remove(word);
            SaveConfig();
        }
        
        public void MuteUser(string username)
        {
            mutedUsers.Add(username);
            SaveConfig();
        }
        
        public void UnmuteUser(string username)
        {
            mutedUsers.Remove(username);
            SaveConfig();
        }
        
        public bool ShouldShowMessage(string username, string message)
        {
            // Usuario silenciado
            if (mutedUsers.Contains(username))
                return false;
            
            // Palabras prohibidas
            var lowerMessage = message.ToLower();
            if (bannedWords.Any(word => lowerMessage.Contains(word)))
                return false;
            
            // Filtro de spam
            if (filterSpam && IsSpam(message))
                return false;
            
            return true;
        }
        
        private bool IsSpam(string message)
        {
            if (message.Length < 10)
                return false;
            
            // Detectar muchas mayúsculas
            if (filterCapsLock)
            {
                var upperCount = message.Count(char.IsUpper);
                var upperRatio = (double)upperCount / message.Length;
                
                if (upperRatio > 0.7)
                    return true;
            }
            
            // Detectar repetición excesiva
            if (filterRepetition && HasExcessiveRepetition(message))
                return true;
            
            return false;
        }
        
        private bool HasExcessiveRepetition(string message)
        {
            var words = message.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length < 5)
                return false;
            
            var wordCount = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            
            foreach (var word in words)
            {
                if (!wordCount.ContainsKey(word))
                    wordCount[word] = 0;
                wordCount[word]++;
            }
            
            return wordCount.Any(kvp => kvp.Value > 5);
        }
        
        public bool FilterSpam
        {
            get => filterSpam;
            set { filterSpam = value; SaveConfig(); }
        }
        
        public bool FilterCapsLock
        {
            get => filterCapsLock;
            set { filterCapsLock = value; SaveConfig(); }
        }
        
        public bool FilterRepetition
        {
            get => filterRepetition;
            set { filterRepetition = value; SaveConfig(); }
        }
        
        public List<string> GetBannedWords() => bannedWords.ToList();
        public List<string> GetMutedUsers() => mutedUsers.ToList();
        
        private void SaveConfig()
        {
            try
            {
                var data = new
                {
                    BannedWords = bannedWords.ToList(),
                    MutedUsers = mutedUsers.ToList(),
                    FilterSpam = filterSpam,
                    FilterCapsLock = filterCapsLock,
                    FilterRepetition = filterRepetition
                };
                var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(configFile, json);
            }
            catch { }
        }
        
        private void LoadConfig()
        {
            try
            {
                if (File.Exists(configFile))
                {
                    var json = File.ReadAllText(configFile);
                    var data = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
                    
                    if (data != null)
                    {
                        if (data.ContainsKey("BannedWords"))
                        {
                            var words = JsonSerializer.Deserialize<List<string>>(data["BannedWords"].ToString());
                            bannedWords = new HashSet<string>(words, StringComparer.OrdinalIgnoreCase);
                        }
                        
                        if (data.ContainsKey("MutedUsers"))
                        {
                            var users = JsonSerializer.Deserialize<List<string>>(data["MutedUsers"].ToString());
                            mutedUsers = new HashSet<string>(users, StringComparer.OrdinalIgnoreCase);
                        }
                    }
                }
            }
            catch { }
        }
    }
    
    // ═══════════════════════════════════════════════════════════════
    // 13. EXPORTACIÓN DE DATOS
    // ═══════════════════════════════════════════════════════════════
    
    public class DataExporter
    {
        public void ExportToCSV(string filename, List<Dictionary<string, object>> data)
        {
            if (data == null || data.Count == 0)
                return;
            
            using (var writer = new StreamWriter(filename, false, Encoding.UTF8))
            {
                // Escribir encabezados
                var headers = data[0].Keys;
                writer.WriteLine(string.Join(",", headers.Select(h => EscapeCSV(h))));
                
                // Escribir datos
                foreach (var row in data)
                {
                    var values = headers.Select(h => EscapeCSV(row.ContainsKey(h) ? row[h]?.ToString() : ""));
                    writer.WriteLine(string.Join(",", values));
                }
            }
        }
        
        private string EscapeCSV(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "\"\"";
            
            if (value.Contains(",") || value.Contains("\"") || value.Contains("\n"))
            {
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            }
            
            return value;
        }
        
        public void ExportToJSON(string filename, object data)
        {
            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(filename, json, Encoding.UTF8);
        }
        
        public void ExportToHTML(string filename, string title, List<Dictionary<string, object>> data)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html>");
            sb.AppendLine("<head>");
            sb.AppendLine($"    <title>{title}</title>");
            sb.AppendLine("    <meta charset='UTF-8'>");
            sb.AppendLine("    <style>");
            sb.AppendLine("        body { font-family: Arial, sans-serif; margin: 20px; background: #f5f5f5; }");
            sb.AppendLine("        h1 { color: #333; }");
            sb.AppendLine("        table { border-collapse: collapse; width: 100%; background: white; box-shadow: 0 2px 4px rgba(0,0,0,0.1); }");
            sb.AppendLine("        th, td { border: 1px solid #ddd; padding: 12px; text-align: left; }");
            sb.AppendLine("        th { background-color: #4CAF50; color: white; font-weight: bold; }");
            sb.AppendLine("        tr:nth-child(even) { background-color: #f9f9f9; }");
            sb.AppendLine("        tr:hover { background-color: #f5f5f5; }");
            sb.AppendLine("    </style>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");
            sb.AppendLine($"    <h1>{title}</h1>");
            
            if (data != null && data.Count > 0)
            {
                sb.AppendLine("    <table>");
                sb.AppendLine("        <thead><tr>");
                
                // Encabezados
                foreach (var header in data[0].Keys)
                {
                    sb.AppendLine($"            <th>{header}</th>");
                }
                
                sb.AppendLine("        </tr></thead>");
                sb.AppendLine("        <tbody>");
                
                // Datos
                foreach (var row in data)
                {
                    sb.AppendLine("        <tr>");
                    foreach (var key in data[0].Keys)
                    {
                        var value = row.ContainsKey(key) ? row[key]?.ToString() : "";
                        sb.AppendLine($"            <td>{value}</td>");
                    }
                    sb.AppendLine("        </tr>");
                }
                
                sb.AppendLine("        </tbody>");
                sb.AppendLine("    </table>");
            }
            
            sb.AppendLine($"    <p style='margin-top: 20px; color: #666;'>Generado: {DateTime.Now:yyyy-MM-dd HH:mm:ss}</p>");
            sb.AppendLine("</body>");
            sb.AppendLine("</html>");
            
            File.WriteAllText(filename, sb.ToString(), Encoding.UTF8);
        }
    }
    
    // ═══════════════════════════════════════════════════════════════
    // 14. BACKUP AUTOMÁTICO
    // ═══════════════════════════════════════════════════════════════
    
    public class AutoBackup
    {
        private readonly string backupDirectory;
        private readonly int maxBackups;
        
        public AutoBackup(string backupDirectory, int maxBackups = 10)
        {
            this.backupDirectory = backupDirectory;
            this.maxBackups = maxBackups;
            
            if (!Directory.Exists(backupDirectory))
                Directory.CreateDirectory(backupDirectory);
        }
        
        public string CreateBackup(string sourceFile)
        {
            if (!File.Exists(sourceFile))
                return null;
            
            try
            {
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var filename = Path.GetFileName(sourceFile);
                var backupFile = Path.Combine(backupDirectory, $"{filename}.{timestamp}.bak");
                
                File.Copy(sourceFile, backupFile, overwrite: true);
                
                CleanOldBackups(filename);
                
                return backupFile;
            }
            catch
            {
                return null;
            }
        }
        
        private void CleanOldBackups(string filename)
        {
            try
            {
                var backups = Directory.GetFiles(backupDirectory, $"{filename}.*.bak")
                    .OrderByDescending(f => File.GetCreationTime(f))
                    .ToList();
                
                // Mantener solo los últimos N backups
                foreach (var backup in backups.Skip(maxBackups))
                {
                    File.Delete(backup);
                }
            }
            catch { }
        }
        
        public List<BackupInfo> GetAvailableBackups(string filename)
        {
            try
            {
                return Directory.GetFiles(backupDirectory, $"{filename}.*.bak")
                    .OrderByDescending(f => File.GetCreationTime(f))
                    .Select(f => new BackupInfo
                    {
                        FilePath = f,
                        FileName = Path.GetFileName(f),
                        CreationTime = File.GetCreationTime(f),
                        Size = new FileInfo(f).Length
                    })
                    .ToList();
            }
            catch
            {
                return new List<BackupInfo>();
            }
        }
        
        public bool RestoreBackup(string backupFile, string targetFile)
        {
            try
            {
                if (!File.Exists(backupFile))
                    return false;
                
                // Crear backup del archivo actual antes de restaurar
                if (File.Exists(targetFile))
                {
                    var tempBackup = targetFile + ".before_restore.bak";
                    File.Copy(targetFile, tempBackup, overwrite: true);
                }
                
                File.Copy(backupFile, targetFile, overwrite: true);
                return true;
            }
            catch
            {
                return false;
            }
        }
        
        public void CleanAllBackups()
        {
            try
            {
                var backups = Directory.GetFiles(backupDirectory, "*.bak");
                foreach (var backup in backups)
                {
                    File.Delete(backup);
                }
            }
            catch { }
        }
        
        public long GetTotalBackupSize()
        {
            try
            {
                return Directory.GetFiles(backupDirectory, "*.bak")
                    .Sum(f => new FileInfo(f).Length);
            }
            catch
            {
                return 0;
            }
        }
    }
    
    public class BackupInfo
    {
        public string FilePath { get; set; }
        public string FileName { get; set; }
        public DateTime CreationTime { get; set; }
        public long Size { get; set; }
    }
    
    // ═══════════════════════════════════════════════════════════════
    // 15. UTILIDADES ADICIONALES
    // ═══════════════════════════════════════════════════════════════
    
    public static class Phase4Utils
    {
        public static string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:F2} {sizes[order]}";
        }
        
        public static string FormatTimeSpan(TimeSpan ts)
        {
            if (ts.TotalDays >= 1)
                return $"{ts.Days}d {ts.Hours}h";
            if (ts.TotalHours >= 1)
                return $"{ts.Hours}h {ts.Minutes}m";
            if (ts.TotalMinutes >= 1)
                return $"{ts.Minutes}m {ts.Seconds}s";
            return $"{ts.Seconds}s";
        }
        
        public static string SanitizeFilename(string filename)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var sanitized = string.Join("_", filename.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
            
            // Prevenir path traversal
            sanitized = sanitized.Replace("..", "").Replace("~", "");
            
            // Limitar longitud
            if (sanitized.Length > 255)
                sanitized = sanitized.Substring(0, 255);
            
            return sanitized;
        }
        
        public static bool IsValidIPAddress(string ip)
        {
            if (string.IsNullOrWhiteSpace(ip))
                return false;
            
            var parts = ip.Split('.');
            if (parts.Length != 4)
                return false;
            
            return parts.All(part => byte.TryParse(part, out _));
        }
    }
}
