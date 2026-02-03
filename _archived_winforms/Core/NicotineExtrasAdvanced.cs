using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Security.Cryptography;

namespace SlskDown.Core
{
    // ═══════════════════════════════════════════════════════════════
    // 7. INTEGRACIÓN CON REPRODUCTORES DE MÚSICA
    // ═══════════════════════════════════════════════════════════════
    
    public class MusicIntegration
    {
        private string currentTrack = "";
        private string currentArtist = "";
        private string currentAlbum = "";
        private DateTime lastUpdate = DateTime.MinValue;
        
        public void DetectNowPlaying()
        {
            try
            {
                // Spotify
                var spotifyProcess = Process.GetProcessesByName("Spotify").FirstOrDefault();
                if (spotifyProcess != null)
                {
                    currentTrack = GetSpotifyCurrentTrack();
                    lastUpdate = DateTime.Now;
                    return;
                }
                
                // VLC
                var vlcProcess = Process.GetProcessesByName("vlc").FirstOrDefault();
                if (vlcProcess != null)
                {
                    currentTrack = GetVLCCurrentTrack();
                    lastUpdate = DateTime.Now;
                    return;
                }
                
                // Windows Media Player
                var wmpProcess = Process.GetProcessesByName("wmplayer").FirstOrDefault();
                if (wmpProcess != null)
                {
                    currentTrack = "Playing in Windows Media Player";
                    lastUpdate = DateTime.Now;
                    return;
                }
                
                // Foobar2000
                var foobarProcess = Process.GetProcessesByName("foobar2000").FirstOrDefault();
                if (foobarProcess != null)
                {
                    currentTrack = "Playing in Foobar2000";
                    lastUpdate = DateTime.Now;
                    return;
                }
            }
            catch { }
        }
        
        private string GetSpotifyCurrentTrack()
        {
            try
            {
                var spotifyProcess = Process.GetProcessesByName("Spotify").FirstOrDefault();
                if (spotifyProcess != null && !string.IsNullOrEmpty(spotifyProcess.MainWindowTitle))
                {
                    var title = spotifyProcess.MainWindowTitle;
                    if (title != "Spotify" && title != "Spotify Premium" && title != "Spotify Free")
                    {
                        return title;
                    }
                }
            }
            catch { }
            return "";
        }
        
        private string GetVLCCurrentTrack()
        {
            try
            {
                var vlcProcess = Process.GetProcessesByName("vlc").FirstOrDefault();
                if (vlcProcess != null && !string.IsNullOrEmpty(vlcProcess.MainWindowTitle))
                {
                    var title = vlcProcess.MainWindowTitle;
                    if (!title.Contains("VLC media player"))
                    {
                        return title.Replace(" - VLC media player", "");
                    }
                }
            }
            catch { }
            return "";
        }
        
        public string GetNowPlayingMessage()
        {
            // Actualizar si han pasado más de 5 segundos
            if ((DateTime.Now - lastUpdate).TotalSeconds > 5)
            {
                DetectNowPlaying();
            }
            
            if (!string.IsNullOrEmpty(currentTrack))
                return $"🎵 Escuchando: {currentTrack}";
            
            return "";
        }
        
        public bool IsPlaying => !string.IsNullOrEmpty(currentTrack);
        public string CurrentTrack => currentTrack;
    }
    
    // ═══════════════════════════════════════════════════════════════
    // 8. TRADUCCIÓN AUTOMÁTICA DE MENSAJES
    // ═══════════════════════════════════════════════════════════════
    
    public class MessageTranslator
    {
        private Dictionary<string, string> languageCache = new Dictionary<string, string>();
        
        public string DetectLanguage(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "unknown";
            
            // Detección simple basada en caracteres
            if (text.Any(c => c >= 0x4E00 && c <= 0x9FFF))
                return "zh"; // Chino
            
            if (text.Any(c => c >= 0x3040 && c <= 0x309F))
                return "ja"; // Japonés
            
            if (text.Any(c => c >= 0xAC00 && c <= 0xD7AF))
                return "ko"; // Coreano
            
            if (text.Any(c => c >= 0x0400 && c <= 0x04FF))
                return "ru"; // Ruso
            
            if (text.Any(c => c >= 0x0600 && c <= 0x06FF))
                return "ar"; // Árabe
            
            if (text.Any(c => c >= 0x0E00 && c <= 0x0E7F))
                return "th"; // Tailandés
            
            // Detección por palabras comunes
            var lowerText = text.ToLower();
            
            if (ContainsSpanishWords(lowerText))
                return "es";
            
            if (ContainsFrenchWords(lowerText))
                return "fr";
            
            if (ContainsGermanWords(lowerText))
                return "de";
            
            if (ContainsItalianWords(lowerText))
                return "it";
            
            if (ContainsPortugueseWords(lowerText))
                return "pt";
            
            return "en"; // Por defecto inglés
        }
        
        private bool ContainsSpanishWords(string text)
        {
            string[] spanishWords = { "hola", "gracias", "por favor", "buenos días", "buenas tardes", "qué", "cómo", "dónde" };
            return spanishWords.Any(w => text.Contains(w));
        }
        
        private bool ContainsFrenchWords(string text)
        {
            string[] frenchWords = { "bonjour", "merci", "s'il vous plaît", "comment", "où", "quoi", "pourquoi" };
            return frenchWords.Any(w => text.Contains(w));
        }
        
        private bool ContainsGermanWords(string text)
        {
            string[] germanWords = { "hallo", "danke", "bitte", "guten tag", "wie", "was", "wo", "warum" };
            return germanWords.Any(w => text.Contains(w));
        }
        
        private bool ContainsItalianWords(string text)
        {
            string[] italianWords = { "ciao", "grazie", "per favore", "buongiorno", "come", "dove", "cosa", "perché" };
            return italianWords.Any(w => text.Contains(w));
        }
        
        private bool ContainsPortugueseWords(string text)
        {
            string[] portugueseWords = { "olá", "obrigado", "por favor", "bom dia", "como", "onde", "o que", "por que" };
            return portugueseWords.Any(w => text.Contains(w));
        }
        
        public string GetLanguageName(string code)
        {
            return code switch
            {
                "es" => "Español",
                "en" => "English",
                "fr" => "Français",
                "de" => "Deutsch",
                "it" => "Italiano",
                "pt" => "Português",
                "ru" => "Русский",
                "zh" => "中文",
                "ja" => "日本語",
                "ko" => "한국어",
                "ar" => "العربية",
                "th" => "ไทย",
                _ => "Unknown"
            };
        }
    }
    
    // ═══════════════════════════════════════════════════════════════
    // 9. CIFRADO DE MENSAJES PRIVADOS
    // ═══════════════════════════════════════════════════════════════
    
    public class MessageEncryption
    {
        private RSACryptoServiceProvider rsa;
        private Dictionary<string, string> userPublicKeys = new Dictionary<string, string>();
        private readonly string keysFile;
        
        public MessageEncryption(string keysFile)
        {
            this.keysFile = keysFile;
            rsa = new RSACryptoServiceProvider(2048);
            LoadKeys();
        }
        
        public string GetPublicKey()
        {
            return Convert.ToBase64String(rsa.ExportRSAPublicKey());
        }
        
        public void AddUserPublicKey(string username, string publicKey)
        {
            userPublicKeys[username] = publicKey;
            SaveKeys();
        }
        
        public string EncryptMessage(string message, string recipientUsername)
        {
            if (!userPublicKeys.ContainsKey(recipientUsername))
                return null; // No se puede cifrar sin clave pública
            
            try
            {
                var publicKey = Convert.FromBase64String(userPublicKeys[recipientUsername]);
                var recipientRsa = new RSACryptoServiceProvider();
                recipientRsa.ImportRSAPublicKey(publicKey, out _);
                
                var messageBytes = Encoding.UTF8.GetBytes(message);
                var encrypted = recipientRsa.Encrypt(messageBytes, true);
                return Convert.ToBase64String(encrypted);
            }
            catch
            {
                return null;
            }
        }
        
        public string DecryptMessage(string encryptedMessage)
        {
            try
            {
                var encrypted = Convert.FromBase64String(encryptedMessage);
                var decrypted = rsa.Decrypt(encrypted, true);
                return Encoding.UTF8.GetString(decrypted);
            }
            catch
            {
                return null;
            }
        }
        
        public bool HasPublicKey(string username)
        {
            return userPublicKeys.ContainsKey(username);
        }
        
        private void SaveKeys()
        {
            try
            {
                var data = new
                {
                    PrivateKey = Convert.ToBase64String(rsa.ExportRSAPrivateKey()),
                    UserPublicKeys = userPublicKeys
                };
                var json = System.Text.Json.JsonSerializer.Serialize(data);
                File.WriteAllText(keysFile, json);
            }
            catch { }
        }
        
        private void LoadKeys()
        {
            try
            {
                if (File.Exists(keysFile))
                {
                    var json = File.ReadAllText(keysFile);
                    var data = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(json);
                    
                    if (data.ContainsKey("PrivateKey"))
                    {
                        var privateKey = Convert.FromBase64String(data["PrivateKey"].ToString());
                        rsa.ImportRSAPrivateKey(privateKey, out _);
                    }
                    
                    if (data.ContainsKey("UserPublicKeys"))
                    {
                        // Cargar claves públicas de usuarios
                    }
                }
            }
            catch { }
        }
    }
    
    // ═══════════════════════════════════════════════════════════════
    // 10. RED DISTRIBUIDA (DISTRIBUTED NETWORK)
    // ═══════════════════════════════════════════════════════════════
    
    public class DistributedNetwork
    {
        public int BranchLevel { get; set; } = 0;
        public string BranchRoot { get; set; }
        public List<string> ChildPeers { get; set; } = new List<string>();
        public string ParentPeer { get; set; }
        public bool AcceptChildren { get; set; } = true;
        public int MaxChildren { get; set; } = 10;
        
        // Estadísticas
        public int SearchesForwarded { get; set; }
        public int SearchesReceived { get; set; }
        public DateTime LastDistribSearch { get; set; }
        
        public void AddChild(string username)
        {
            if (ChildPeers.Count < MaxChildren && !ChildPeers.Contains(username))
            {
                ChildPeers.Add(username);
            }
        }
        
        public void RemoveChild(string username)
        {
            ChildPeers.Remove(username);
        }
        
        public void SetParent(string username, int level)
        {
            ParentPeer = username;
            BranchLevel = level + 1;
        }
        
        public bool CanAcceptChildren => AcceptChildren && ChildPeers.Count < MaxChildren;
        
        public string GetNetworkInfo()
        {
            var sb = new StringBuilder();
            sb.AppendLine("═══════════════════════════════════════");
            sb.AppendLine("🌍 RED DISTRIBUIDA");
            sb.AppendLine("═══════════════════════════════════════");
            sb.AppendLine($"Nivel: {BranchLevel}");
            sb.AppendLine($"Raíz: {BranchRoot ?? "N/A"}");
            sb.AppendLine($"Padre: {ParentPeer ?? "N/A"}");
            sb.AppendLine($"Hijos: {ChildPeers.Count}/{MaxChildren}");
            if (ChildPeers.Count > 0)
            {
                sb.AppendLine("Lista de hijos:");
                foreach (var child in ChildPeers)
                {
                    sb.AppendLine($"  - {child}");
                }
            }
            sb.AppendLine($"\nBúsquedas reenviadas: {SearchesForwarded}");
            sb.AppendLine($"Búsquedas recibidas: {SearchesReceived}");
            return sb.ToString();
        }
    }
    
    // ═══════════════════════════════════════════════════════════════
    // UTILIDADES ADICIONALES
    // ═══════════════════════════════════════════════════════════════
    
    public static class NicotineUtils
    {
        public static string FormatBytes(long bytes)
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
        
        public static string FormatSpeed(double bytesPerSecond)
        {
            return $"{FormatBytes((long)bytesPerSecond)}/s";
        }
        
        public static string FormatDuration(TimeSpan duration)
        {
            if (duration.TotalHours >= 1)
                return $"{duration.Hours}h {duration.Minutes}m";
            if (duration.TotalMinutes >= 1)
                return $"{duration.Minutes}m {duration.Seconds}s";
            return $"{duration.Seconds}s";
        }
        
        public static string GetFileIcon(string filename)
        {
            var ext = Path.GetExtension(filename).ToLower();
            return ext switch
            {
                ".mp3" or ".flac" or ".wav" or ".m4a" or ".aac" or ".ogg" => "[Audio]",
                ".mp4" or ".mkv" or ".avi" or ".mov" or ".wmv" => "[Video]",
                ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" => "[Imagen]",
                ".pdf" => "[PDF]",
                ".zip" or ".rar" or ".7z" or ".tar" or ".gz" => "[Archivo]",
                ".exe" or ".msi" => "[Ejecutable]",
                ".txt" or ".doc" or ".docx" => "[Documento]",
                _ => "[Archivo]"
            };
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
    }
}
