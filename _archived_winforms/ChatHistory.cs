using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;
using System.Drawing;

namespace SlskDown
{
    // Sistema de historial de chats persistente
    public class ChatMessage
    {
        public string Username { get; set; }
        public string Message { get; set; }
        public DateTime Timestamp { get; set; }
        public bool IsOutgoing { get; set; }
        public bool IsRead { get; set; }
    }
    
    public class ChatConversation
    {
        public string Username { get; set; }
        public List<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
        public bool IsPinned { get; set; }
        public DateTime LastMessageTime => Messages.Any() ? Messages.Last().Timestamp : DateTime.MinValue;
        public int UnreadCount => Messages.Count(m => !m.IsRead && !m.IsOutgoing);
    }
    
    public class ChatHistoryManager
    {
        private Dictionary<string, ChatConversation> chatHistory = new Dictionary<string, ChatConversation>(StringComparer.OrdinalIgnoreCase);
        private HashSet<string> pinnedChats = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private string chatHistoryPath;
        private int maxHistoryDays = 90;
        private Action<string> logAction;
        
        public ChatHistoryManager(string dataDir, Action<string> logger)
        {
            chatHistoryPath = Path.Combine(dataDir, "chat_history.json");
            logAction = logger;
            LoadChatHistory();
            CleanOldMessages();
        }
        
        public void SaveMessage(string username, string message, bool isOutgoing)
        {
            if (!chatHistory.ContainsKey(username))
            {
                chatHistory[username] = new ChatConversation 
                { 
                    Username = username,
                    IsPinned = pinnedChats.Contains(username)
                };
            }
            
            chatHistory[username].Messages.Add(new ChatMessage
            {
                Username = username,
                Message = message,
                Timestamp = DateTime.Now,
                IsOutgoing = isOutgoing,
                IsRead = isOutgoing // Mensajes salientes marcados como leídos
            });
            
            SaveChatHistory();
        }
        
        public void MarkAsRead(string username)
        {
            if (chatHistory.ContainsKey(username))
            {
                foreach (var msg in chatHistory[username].Messages.Where(m => !m.IsRead))
                {
                    msg.IsRead = true;
                }
                SaveChatHistory();
            }
        }
        
        public void PinChat(string username)
        {
            pinnedChats.Add(username);
            if (chatHistory.ContainsKey(username))
            {
                chatHistory[username].IsPinned = true;
                SaveChatHistory();
            }
        }
        
        public void UnpinChat(string username)
        {
            pinnedChats.Remove(username);
            if (chatHistory.ContainsKey(username))
            {
                chatHistory[username].IsPinned = false;
                SaveChatHistory();
            }
        }
        
        public void DeleteConversation(string username)
        {
            if (chatHistory.ContainsKey(username))
            {
                chatHistory.Remove(username);
                pinnedChats.Remove(username);
                SaveChatHistory();
                logAction?.Invoke($"🗑️ Conversación con {username} eliminada");
            }
        }
        
        public List<ChatMessage> GetMessages(string username)
        {
            return chatHistory.ContainsKey(username) 
                ? new List<ChatMessage>(chatHistory[username].Messages) 
                : new List<ChatMessage>();
        }
        
        public List<ChatConversation> GetAllConversations()
        {
            return chatHistory.Values
                .OrderByDescending(c => c.IsPinned)
                .ThenByDescending(c => c.LastMessageTime)
                .ToList();
        }
        
        public int GetUnreadCount(string username)
        {
            return chatHistory.ContainsKey(username) 
                ? chatHistory[username].UnreadCount 
                : 0;
        }
        
        public int GetTotalUnreadCount()
        {
            return chatHistory.Values.Sum(c => c.UnreadCount);
        }
        
        public Panel CreateChatHistoryPanel(Action<string> onChatSelected)
        {
            var panel = new Panel 
            { 
                Dock = DockStyle.Left, 
                Width = 220, 
                BackColor = Color.FromArgb(35, 35, 35),
                Padding = new Padding(5)
            };
            
            var lblTitle = new Label
            {
                Text = "💬 Conversaciones",
                Dock = DockStyle.Top,
                Height = 30,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(5, 0, 0, 0)
            };
            panel.Controls.Add(lblTitle);
            
            var lvChats = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                BackColor = Color.FromArgb(40, 40, 40),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.None,
                HeaderStyle = ColumnHeaderStyle.None,
                Font = new Font("Segoe UI", 9)
            };
            
            lvChats.Columns.Add("Usuario", 150);
            lvChats.Columns.Add("Tiempo", 60);
            
            foreach (var chat in GetAllConversations())
            {
                var item = new ListViewItem(chat.Username);
                item.SubItems.Add(GetRelativeTime(chat.LastMessageTime));
                item.Tag = chat.Username;
                
                if (chat.IsPinned)
                {
                    item.BackColor = Color.FromArgb(60, 60, 80);
                    item.Font = new Font(item.Font, FontStyle.Bold);
                    item.Text = "📌 " + item.Text;
                }
                
                if (chat.UnreadCount > 0)
                {
                    item.ForeColor = Color.LightGreen;
                    item.Font = new Font(item.Font, FontStyle.Bold);
                    item.SubItems[0].Text += $" ({chat.UnreadCount})";
                }
                
                lvChats.Items.Add(item);
            }
            
            lvChats.ItemActivate += (s, e) =>
            {
                if (lvChats.SelectedItems.Count > 0)
                {
                    string username = lvChats.SelectedItems[0].Tag as string;
                    onChatSelected?.Invoke(username);
                }
            };
            
            // Menú contextual
            var contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add("📌 Pin/Unpin", null, (s, e) =>
            {
                if (lvChats.SelectedItems.Count > 0)
                {
                    string username = lvChats.SelectedItems[0].Tag as string;
                    if (pinnedChats.Contains(username))
                        UnpinChat(username);
                    else
                        PinChat(username);
                }
            });
            contextMenu.Items.Add("✓ Marcar como leído", null, (s, e) =>
            {
                if (lvChats.SelectedItems.Count > 0)
                {
                    string username = lvChats.SelectedItems[0].Tag as string;
                    MarkAsRead(username);
                }
            });
            contextMenu.Items.Add("🗑️ Eliminar conversación", null, (s, e) =>
            {
                if (lvChats.SelectedItems.Count > 0)
                {
                    string username = lvChats.SelectedItems[0].Tag as string;
                    if (MessageBox.Show($"¿Eliminar conversación con {username}?", "Confirmar", 
                        MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    {
                        DeleteConversation(username);
                    }
                }
            });
            lvChats.ContextMenuStrip = contextMenu;
            
            panel.Controls.Add(lvChats);
            lblTitle.BringToFront();
            
            return panel;
        }
        
        public RichTextBox CreateChatDisplay(string username)
        {
            var rtb = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10),
                BorderStyle = BorderStyle.None,
                ReadOnly = true,
                Padding = new Padding(10)
            };
            
            var messages = GetMessages(username);
            foreach (var msg in messages)
            {
                string prefix = msg.IsOutgoing ? "Tú" : username;
                string timestamp = msg.Timestamp.ToString("HH:mm");
                
                rtb.SelectionColor = Color.Gray;
                rtb.AppendText($"[{timestamp}] ");
                
                rtb.SelectionColor = msg.IsOutgoing ? Color.LightBlue : Color.LightGreen;
                rtb.SelectionFont = new Font(rtb.Font, FontStyle.Bold);
                rtb.AppendText($"{prefix}: ");
                
                rtb.SelectionColor = Color.White;
                rtb.SelectionFont = new Font(rtb.Font, FontStyle.Regular);
                rtb.AppendText($"{msg.Message}\n");
            }
            
            MarkAsRead(username);
            
            return rtb;
        }
        
        private string GetRelativeTime(DateTime timestamp)
        {
            if (timestamp == DateTime.MinValue) return "";
            
            var diff = DateTime.Now - timestamp;
            
            if (diff.TotalMinutes < 1) return "ahora";
            if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m";
            if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h";
            if (diff.TotalDays < 7) return $"{(int)diff.TotalDays}d";
            if (diff.TotalDays < 30) return $"{(int)(diff.TotalDays / 7)}sem";
            return timestamp.ToString("dd/MM");
        }
        
        private void CleanOldMessages()
        {
            var cutoffDate = DateTime.Now.AddDays(-maxHistoryDays);
            int removed = 0;
            
            foreach (var conversation in chatHistory.Values.ToList())
            {
                int originalCount = conversation.Messages.Count;
                conversation.Messages.RemoveAll(m => m.Timestamp < cutoffDate);
                removed += originalCount - conversation.Messages.Count;
                
                // Eliminar conversación si no tiene mensajes
                if (conversation.Messages.Count == 0 && !conversation.IsPinned)
                {
                    chatHistory.Remove(conversation.Username);
                }
            }
            
            if (removed > 0)
            {
                SaveChatHistory();
                logAction?.Invoke($"🗑️ Limpiados {removed} mensajes antiguos (>{maxHistoryDays} días)");
            }
        }
        
        public void SetMaxHistoryDays(int days)
        {
            maxHistoryDays = days;
        }
        
        private void LoadChatHistory()
        {
            try
            {
                if (File.Exists(chatHistoryPath))
                {
                    var json = File.ReadAllText(chatHistoryPath);
                    var data = JsonSerializer.Deserialize<ChatHistoryData>(json);
                    
                    if (data != null)
                    {
                        chatHistory = data.Conversations?.ToDictionary(c => c.Username, StringComparer.OrdinalIgnoreCase) 
                            ?? new Dictionary<string, ChatConversation>(StringComparer.OrdinalIgnoreCase);
                        pinnedChats = new HashSet<string>(data.PinnedChats ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
                        
                        // Actualizar estado de pinned en conversaciones
                        foreach (var username in pinnedChats)
                        {
                            if (chatHistory.ContainsKey(username))
                            {
                                chatHistory[username].IsPinned = true;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logAction?.Invoke($"⚠️ Error cargando historial de chat: {ex.Message}");
            }
        }
        
        private void SaveChatHistory()
        {
            try
            {
                var data = new ChatHistoryData
                {
                    Conversations = chatHistory.Values.ToList(),
                    PinnedChats = pinnedChats.ToList()
                };
                
                var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(chatHistoryPath, json);
            }
            catch (Exception ex)
            {
                logAction?.Invoke($"⚠️ Error guardando historial de chat: {ex.Message}");
            }
        }
        
        private class ChatHistoryData
        {
            public List<ChatConversation> Conversations { get; set; }
            public List<string> PinnedChats { get; set; }
        }
    }
}
