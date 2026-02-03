using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.IO;
using System.Text.Json;
using System.Diagnostics;
using System.Windows.Forms;

namespace SlskDown.Core
{
    // ═══════════════════════════════════════════════════════════════
    // NOTA: La clase TransferStatistics está en Statistics\TransferStatistics.cs
    // ═══════════════════════════════════════════════════════════════
    
    // ═══════════════════════════════════════════════════════════════
    // 1. SISTEMA DE NOTAS Y ETIQUETAS DE USUARIOS
    // ═══════════════════════════════════════════════════════════════
    
    public enum UserTag
    {
        Friend,      // Verde
        Trusted,     // Azul
        Neutral,     // Gris
        Suspicious,  // Amarillo
        Blocked      // Rojo
    }
    
    public class UserNote
    {
        public string Username { get; set; }
        public string Note { get; set; }
        public UserTag Tag { get; set; }
        public List<string> Groups { get; set; } = new List<string>();
        public List<UserInteraction> History { get; set; } = new List<UserInteraction>();
        public DateTime FirstSeen { get; set; }
        public DateTime LastSeen { get; set; }
        public int TotalInteractions => History.Count;
    }
    
    public class UserInteraction
    {
        public DateTime Timestamp { get; set; }
        public string Type { get; set; } // "download", "upload", "message", "search"
        public string Details { get; set; }
    }
    
    public class UserNotesSystem
    {
        private Dictionary<string, UserNote> userNotes = new Dictionary<string, UserNote>();
        private readonly string dataFile;
        
        public UserNotesSystem(string dataFile)
        {
            this.dataFile = dataFile;
            LoadFromFile();
        }
        
        public void AddInteraction(string username, string type, string details)
        {
            if (!userNotes.ContainsKey(username))
            {
                userNotes[username] = new UserNote
                {
                    Username = username,
                    FirstSeen = DateTime.Now,
                    Tag = UserTag.Neutral
                };
            }
            
            userNotes[username].LastSeen = DateTime.Now;
            userNotes[username].History.Add(new UserInteraction
            {
                Timestamp = DateTime.Now,
                Type = type,
                Details = details
            });
            
            // Limitar historial a 100 entradas
            if (userNotes[username].History.Count > 100)
                userNotes[username].History.RemoveAt(0);
        }
        
        public void SetNote(string username, string note)
        {
            if (!userNotes.ContainsKey(username))
                AddInteraction(username, "note", "Created");
            
            userNotes[username].Note = note;
        }
        
        public void SetTag(string username, UserTag tag)
        {
            if (!userNotes.ContainsKey(username))
                AddInteraction(username, "tag", $"Set to {tag}");
            
            userNotes[username].Tag = tag;
        }
        
        public void AddToGroup(string username, string group)
        {
            if (!userNotes.ContainsKey(username))
                AddInteraction(username, "group", $"Added to {group}");
            
            if (!userNotes[username].Groups.Contains(group))
                userNotes[username].Groups.Add(group);
        }
        
        public Color GetUserColor(string username)
        {
            if (!userNotes.ContainsKey(username))
                return Color.Gray;
            
            return userNotes[username].Tag switch
            {
                UserTag.Friend => Color.FromArgb(0, 200, 0),
                UserTag.Trusted => Color.FromArgb(0, 120, 215),
                UserTag.Suspicious => Color.FromArgb(255, 185, 0),
                UserTag.Blocked => Color.FromArgb(232, 17, 35),
                _ => Color.Gray
            };
        }
        
        public UserNote GetUserNote(string username)
        {
            return userNotes.ContainsKey(username) ? userNotes[username] : null;
        }
        
        public List<string> GetUsersByTag(UserTag tag)
        {
            return userNotes.Where(kvp => kvp.Value.Tag == tag).Select(kvp => kvp.Key).ToList();
        }
        
        public List<string> GetUsersByGroup(string group)
        {
            return userNotes.Where(kvp => kvp.Value.Groups.Contains(group)).Select(kvp => kvp.Key).ToList();
        }
        
        public void SaveToFile()
        {
            try
            {
                var json = JsonSerializer.Serialize(userNotes, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(dataFile, json);
            }
            catch { }
        }
        
        private void LoadFromFile()
        {
            try
            {
                if (File.Exists(dataFile))
                {
                    var json = File.ReadAllText(dataFile);
                    userNotes = JsonSerializer.Deserialize<Dictionary<string, UserNote>>(json) 
                        ?? new Dictionary<string, UserNote>();
                }
            }
            catch { }
        }
    }
    
    // ═══════════════════════════════════════════════════════════════
    // 3. NOTIFICACIONES PUSH AVANZADAS
    // ═══════════════════════════════════════════════════════════════
    
    public enum NotificationType
    {
        DownloadComplete,
        DownloadStarted,
        DownloadFailed,
        MessageReceived,
        SearchComplete,
        UserOnline,
        UserOffline,
        WishlistMatch,
        ConnectionLost,
        ConnectionRestored
    }
    
    public class NotificationSystem
    {
        private Dictionary<NotificationType, bool> enabledNotifications = new Dictionary<NotificationType, bool>();
        private Dictionary<NotificationType, string> soundFiles = new Dictionary<NotificationType, string>();
        private NotifyIcon notifyIcon;
        
        public NotificationSystem(NotifyIcon notifyIcon)
        {
            this.notifyIcon = notifyIcon;
            
            // Habilitar todas por defecto
            foreach (NotificationType type in Enum.GetValues(typeof(NotificationType)))
            {
                enabledNotifications[type] = true;
            }
        }
        
        public void Notify(NotificationType type, string title, string message, int durationMs = 3000)
        {
            if (!enabledNotifications.GetValueOrDefault(type, true))
                return;
            
            // Notificación de Windows
            if (notifyIcon != null)
            {
                notifyIcon.BalloonTipTitle = title;
                notifyIcon.BalloonTipText = message;
                notifyIcon.BalloonTipIcon = GetIconForType(type);
                notifyIcon.ShowBalloonTip(durationMs);
            }
            
            // Sonido
            if (soundFiles.ContainsKey(type) && File.Exists(soundFiles[type]))
            {
                try
                {
                    var player = new System.Media.SoundPlayer(soundFiles[type]);
                    player.Play();
                }
                catch { }
            }
        }
        
        private ToolTipIcon GetIconForType(NotificationType type)
        {
            return type switch
            {
                NotificationType.DownloadComplete => ToolTipIcon.Info,
                NotificationType.DownloadFailed => ToolTipIcon.Error,
                NotificationType.MessageReceived => ToolTipIcon.Info,
                NotificationType.ConnectionLost => ToolTipIcon.Warning,
                NotificationType.WishlistMatch => ToolTipIcon.Info,
                _ => ToolTipIcon.None
            };
        }
        
        public void SetEnabled(NotificationType type, bool enabled)
        {
            enabledNotifications[type] = enabled;
        }
        
        public void SetSound(NotificationType type, string soundFile)
        {
            soundFiles[type] = soundFile;
        }
        
        public bool IsEnabled(NotificationType type)
        {
            return enabledNotifications.GetValueOrDefault(type, true);
        }
    }
    
    // ═══════════════════════════════════════════════════════════════
    // 4. AUTO-REPLY AVANZADO CON VARIABLES
    // ═══════════════════════════════════════════════════════════════
    
    public class AdvancedAutoReply
    {
        private Dictionary<string, string> userReplies = new Dictionary<string, string>();
        private Dictionary<string, string> keywordReplies = new Dictionary<string, string>();
        private bool isAway = false;
        private string awayMessage = "Estoy ausente. Volveré pronto.";
        private Func<int> getActiveDownloadsCount;
        private Func<string, int> getQueuePosition;
        
        public AdvancedAutoReply(Func<int> getActiveDownloadsCount, Func<string, int> getQueuePosition)
        {
            this.getActiveDownloadsCount = getActiveDownloadsCount;
            this.getQueuePosition = getQueuePosition;
            
            // Respuestas por defecto
            keywordReplies["hola"] = "¡Hola ${user}! Soy un bot. El usuario volverá pronto.";
            keywordReplies["hello"] = "Hello ${user}! I'm a bot. The user will be back soon.";
            keywordReplies["queue"] = "Tu posición en cola: ${queue}";
            keywordReplies["position"] = "Your queue position: ${queue}";
        }
        
        public string ProcessMessage(string username, string message)
        {
            // Auto-away
            if (isAway)
                return ReplaceVariables(awayMessage, username);
            
            // Respuesta por usuario
            if (userReplies.ContainsKey(username))
                return ReplaceVariables(userReplies[username], username);
            
            // Respuesta por palabra clave
            var lowerMessage = message.ToLower();
            foreach (var kvp in keywordReplies)
            {
                if (lowerMessage.Contains(kvp.Key))
                    return ReplaceVariables(kvp.Value, username);
            }
            
            return null;
        }
        
        private string ReplaceVariables(string template, string username)
        {
            return template
                .Replace("${user}", username)
                .Replace("${time}", DateTime.Now.ToString("HH:mm"))
                .Replace("${date}", DateTime.Now.ToString("yyyy-MM-dd"))
                .Replace("${downloads}", getActiveDownloadsCount?.Invoke().ToString() ?? "0")
                .Replace("${queue}", getQueuePosition?.Invoke(username).ToString() ?? "N/A");
        }
        
        public void SetAway(bool away, string message = null)
        {
            isAway = away;
            if (message != null)
                awayMessage = message;
        }
        
        public void SetUserReply(string username, string reply)
        {
            userReplies[username] = reply;
        }
        
        public void SetKeywordReply(string keyword, string reply)
        {
            keywordReplies[keyword.ToLower()] = reply;
        }
        
        public bool IsAway => isAway;
    }
    
    // ═══════════════════════════════════════════════════════════════
    // 5. UI PERSONALIZABLE - LAYOUTS
    // ═══════════════════════════════════════════════════════════════
    
    public class UILayout
    {
        public string Name { get; set; }
        public Dictionary<string, Rectangle> PanelBounds { get; set; } = new Dictionary<string, Rectangle>();
        public Dictionary<string, bool> PanelVisibility { get; set; } = new Dictionary<string, bool>();
        public Dictionary<string, List<ColumnInfo>> ColumnSettings { get; set; } = new Dictionary<string, List<ColumnInfo>>();
        public Size FormSize { get; set; }
        public FormWindowState WindowState { get; set; }
    }
    
    public class ColumnInfo
    {
        public string Text { get; set; }
        public int Width { get; set; }
        public int DisplayIndex { get; set; }
    }
    
    public class UICustomization
    {
        private readonly string layoutsDirectory;
        
        public UICustomization(string layoutsDirectory)
        {
            this.layoutsDirectory = layoutsDirectory;
            if (!Directory.Exists(layoutsDirectory))
                Directory.CreateDirectory(layoutsDirectory);
        }
        
        public void SaveLayout(string name, Form form)
        {
            var layout = new UILayout
            {
                Name = name,
                FormSize = form.Size,
                WindowState = form.WindowState
            };
            
            // Guardar posiciones de paneles
            foreach (Control control in form.Controls)
            {
                if (control is Panel panel)
                {
                    layout.PanelBounds[panel.Name] = panel.Bounds;
                    layout.PanelVisibility[panel.Name] = panel.Visible;
                }
                
                if (control is ListView lv)
                {
                    var columns = new List<ColumnInfo>();
                    foreach (ColumnHeader col in lv.Columns)
                    {
                        columns.Add(new ColumnInfo
                        {
                            Text = col.Text,
                            Width = col.Width,
                            DisplayIndex = col.DisplayIndex
                        });
                    }
                    layout.ColumnSettings[lv.Name] = columns;
                }
            }
            
            var json = JsonSerializer.Serialize(layout, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(Path.Combine(layoutsDirectory, $"{name}.json"), json);
        }
        
        public void LoadLayout(string name, Form form)
        {
            try
            {
                var path = Path.Combine(layoutsDirectory, $"{name}.json");
                if (!File.Exists(path))
                    return;
                
                var json = File.ReadAllText(path);
                var layout = JsonSerializer.Deserialize<UILayout>(json);
                
                if (layout == null)
                    return;
                
                // Restaurar tamaño de formulario
                form.Size = layout.FormSize;
                form.WindowState = layout.WindowState;
                
                // Restaurar paneles
                foreach (Control control in form.Controls)
                {
                    if (control is Panel panel && layout.PanelBounds.ContainsKey(panel.Name))
                    {
                        panel.Bounds = layout.PanelBounds[panel.Name];
                        if (layout.PanelVisibility.ContainsKey(panel.Name))
                            panel.Visible = layout.PanelVisibility[panel.Name];
                    }
                    
                    if (control is ListView lv && layout.ColumnSettings.ContainsKey(lv.Name))
                    {
                        var columns = layout.ColumnSettings[lv.Name];
                        for (int i = 0; i < Math.Min(columns.Count, lv.Columns.Count); i++)
                        {
                            lv.Columns[i].Width = columns[i].Width;
                            lv.Columns[i].DisplayIndex = columns[i].DisplayIndex;
                        }
                    }
                }
            }
            catch { }
        }
        
        public List<string> GetAvailableLayouts()
        {
            try
            {
                return Directory.GetFiles(layoutsDirectory, "*.json")
                    .Select(f => Path.GetFileNameWithoutExtension(f))
                    .ToList();
            }
            catch
            {
                return new List<string>();
            }
        }
    }
    
    // ═══════════════════════════════════════════════════════════════
    // 6. BÚSQUEDA DE USUARIOS SIMILARES
    // ═══════════════════════════════════════════════════════════════
    
    public class UserSimilarity
    {
        public string Username { get; set; }
        public double SimilarityScore { get; set; }
        public List<string> CommonFiles { get; set; } = new List<string>();
        public int TotalCommonFiles => CommonFiles.Count;
    }
    
    public class SimilarUserFinder
    {
        public List<UserSimilarity> FindSimilarUsers(
            Dictionary<string, List<string>> userLibraries,
            List<string> myDownloads,
            int minCommonFiles = 5)
        {
            var similarities = new List<UserSimilarity>();
            
            foreach (var kvp in userLibraries)
            {
                var username = kvp.Key;
                var userFiles = kvp.Value;
                
                // Calcular archivos comunes
                var commonFiles = myDownloads.Intersect(userFiles, StringComparer.OrdinalIgnoreCase).ToList();
                
                if (commonFiles.Count < minCommonFiles)
                    continue;
                
                // Calcular similitud con Jaccard
                var unionFiles = myDownloads.Union(userFiles, StringComparer.OrdinalIgnoreCase).Count();
                var similarity = (double)commonFiles.Count / unionFiles;
                
                similarities.Add(new UserSimilarity
                {
                    Username = username,
                    SimilarityScore = similarity,
                    CommonFiles = commonFiles
                });
            }
            
            return similarities.OrderByDescending(s => s.SimilarityScore).ToList();
        }
        
        public List<string> RecommendFiles(
            Dictionary<string, List<string>> userLibraries,
            List<string> myDownloads,
            string similarUsername)
        {
            if (!userLibraries.ContainsKey(similarUsername))
                return new List<string>();
            
            // Archivos que tiene el usuario similar pero yo no
            return userLibraries[similarUsername]
                .Except(myDownloads, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}
