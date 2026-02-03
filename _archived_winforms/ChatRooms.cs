using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.Drawing;
using System.IO;
using System.Text.Json;

namespace SlskDown
{
    public class ChatRoom
    {
        public string Name { get; set; }
        public List<RoomMessage> Messages { get; set; } = new List<RoomMessage>();
        public HashSet<string> Users { get; set; } = new HashSet<string>();
        public bool IsMuted { get; set; }
        public int UnreadCount { get; set; }
        public bool IsPublic { get; set; }
        public bool IsJoined { get; set; }
    }
    
    public class RoomMessage
    {
        public string RoomName { get; set; }
        public string Username { get; set; }
        public string Message { get; set; }
        public DateTime Timestamp { get; set; }
        public bool IsHighlighted { get; set; }
    }
    
    public class WallMessage
    {
        public string Username { get; set; }
        public string Message { get; set; }
        public DateTime SetTime { get; set; }
        public bool IsOnline { get; set; }
        public string CountryCode { get; set; }
    }
    
    public class ChatRoomManager
    {
        private Dictionary<string, ChatRoom> joinedRooms = new Dictionary<string, ChatRoom>(StringComparer.OrdinalIgnoreCase);
        private HashSet<string> mutedRooms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, List<WallMessage>> roomWalls = new Dictionary<string, List<WallMessage>>(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, string> myWallMessages = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private bool showPublicFeed = true;
        private int maxFeedMessages = 100;
        private List<RoomMessage> publicFeed = new List<RoomMessage>();
        private string dataDir;
        private Action<string> logAction;
        private string currentUsername;
        
        public ChatRoomManager(string dataDirectory, Action<string> logger, string username)
        {
            dataDir = dataDirectory;
            logAction = logger;
            currentUsername = username;
            LoadRoomData();
        }
        
        public void JoinRoom(string roomName)
        {
            if (!joinedRooms.ContainsKey(roomName))
            {
                joinedRooms[roomName] = new ChatRoom
                {
                    Name = roomName,
                    IsJoined = true,
                    IsPublic = IsPublicRoom(roomName)
                };
                logAction?.Invoke($"💬 Unido a room: {roomName}");
                SaveRoomData();
            }
        }
        
        public void LeaveRoom(string roomName)
        {
            if (joinedRooms.ContainsKey(roomName))
            {
                joinedRooms[roomName].IsJoined = false;
                logAction?.Invoke($"👋 Saliendo de room: {roomName}");
                SaveRoomData();
            }
        }
        
        public void AddMessage(string roomName, string username, string message)
        {
            var roomMsg = new RoomMessage
            {
                RoomName = roomName,
                Username = username,
                Message = message,
                Timestamp = DateTime.Now,
                IsHighlighted = message.Contains(currentUsername)
            };
            
            if (!joinedRooms.ContainsKey(roomName))
            {
                JoinRoom(roomName);
            }
            
            joinedRooms[roomName].Messages.Add(roomMsg);
            
            if (!mutedRooms.Contains(roomName))
            {
                joinedRooms[roomName].UnreadCount++;
            }
            
            if (showPublicFeed)
            {
                publicFeed.Add(roomMsg);
                if (publicFeed.Count > maxFeedMessages)
                {
                    publicFeed.RemoveAt(0);
                }
            }
        }
        
        public void MuteRoom(string roomName)
        {
            mutedRooms.Add(roomName);
            if (joinedRooms.ContainsKey(roomName))
            {
                joinedRooms[roomName].IsMuted = true;
            }
            logAction?.Invoke($"🔇 Room silenciado: {roomName}");
            SaveRoomData();
        }
        
        public void UnmuteRoom(string roomName)
        {
            mutedRooms.Remove(roomName);
            if (joinedRooms.ContainsKey(roomName))
            {
                joinedRooms[roomName].IsMuted = false;
            }
            logAction?.Invoke($"🔊 Room activado: {roomName}");
            SaveRoomData();
        }
        
        public void MarkAsRead(string roomName)
        {
            if (joinedRooms.ContainsKey(roomName))
            {
                joinedRooms[roomName].UnreadCount = 0;
            }
        }
        
        public bool ShouldNotify(string roomName)
        {
            return !mutedRooms.Contains(roomName);
        }
        
        public Panel CreatePublicFeedPanel(Action<string> onRoomClick)
        {
            var panel = new Panel 
            { 
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(30, 30, 30)
            };
            
            var lblTitle = new Label
            {
                Text = "📡 Public Chat Feed",
                Dock = DockStyle.Top,
                Height = 30,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 0, 0, 0),
                BackColor = Color.FromArgb(40, 40, 40)
            };
            
            var lvFeed = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                BackColor = Color.FromArgb(35, 35, 35),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 9)
            };
            
            lvFeed.Columns.Add("Room", 120);
            lvFeed.Columns.Add("Usuario", 100);
            lvFeed.Columns.Add("Mensaje", 400);
            lvFeed.Columns.Add("Hora", 60);
            
            foreach (var msg in publicFeed.OrderByDescending(m => m.Timestamp).Take(50))
            {
                var item = new ListViewItem(msg.RoomName);
                item.SubItems.Add(msg.Username);
                item.SubItems.Add(msg.Message);
                item.SubItems.Add(msg.Timestamp.ToString("HH:mm"));
                item.Tag = msg.RoomName;
                
                if (msg.IsHighlighted)
                {
                    item.BackColor = Color.FromArgb(60, 60, 0);
                    item.Font = new Font(item.Font, FontStyle.Bold);
                }
                
                lvFeed.Items.Add(item);
            }
            
            lvFeed.ItemActivate += (s, e) =>
            {
                if (lvFeed.SelectedItems.Count > 0)
                {
                    string roomName = lvFeed.SelectedItems[0].Tag as string;
                    onRoomClick?.Invoke(roomName);
                }
            };
            
            panel.Controls.Add(lvFeed);
            panel.Controls.Add(lblTitle);
            lblTitle.BringToFront();
            
            return panel;
        }
        
        public Form CreateRoomWallDialog(string roomName)
        {
            var form = new Form
            {
                Text = $"Room Wall - {roomName}",
                Size = new Size(700, 600),
                StartPosition = FormStartPosition.CenterParent,
                BackColor = Color.FromArgb(30, 30, 30),
                FormBorderStyle = FormBorderStyle.Sizable
            };
            
            var lvWall = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                BackColor = Color.FromArgb(40, 40, 40),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 9)
            };
            
            lvWall.Columns.Add("Estado", 60);
            lvWall.Columns.Add("Usuario", 120);
            lvWall.Columns.Add("Mensaje", 400);
            lvWall.Columns.Add("Desde", 100);
            
            if (roomWalls.ContainsKey(roomName))
            {
                foreach (var msg in roomWalls[roomName].OrderBy(m => m.Username))
                {
                    var item = new ListViewItem();
                    
                    string status = msg.IsOnline ? "🟢 Online" : "🔴 Offline";
                    item.SubItems.Add(status);
                    
                    var usernameItem = item.SubItems.Add(msg.Username);
                    usernameItem.ForeColor = msg.IsOnline ? Color.LightGreen : Color.Gray;
                    
                    item.SubItems.Add(msg.Message);
                    item.SubItems.Add(GetRelativeTime(msg.SetTime));
                    
                    lvWall.Items.Add(item);
                }
            }
            
            var bottomPanel = new Panel 
            { 
                Dock = DockStyle.Bottom, 
                Height = 100, 
                Padding = new Padding(10),
                BackColor = Color.FromArgb(35, 35, 35)
            };
            
            var lblMyMsg = new Label
            {
                Text = "Tu mensaje en el muro:",
                Dock = DockStyle.Top,
                Height = 20,
                ForeColor = Color.LightGray,
                Font = new Font("Segoe UI", 9)
            };
            
            var txtMyMessage = new TextBox
            {
                Dock = DockStyle.Top,
                Height = 30,
                BackColor = Color.FromArgb(50, 50, 50),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10),
                Text = myWallMessages.ContainsKey(roomName) ? myWallMessages[roomName] : ""
            };
            
            var btnPanel = new FlowLayoutPanel 
            { 
                Dock = DockStyle.Bottom, 
                Height = 35, 
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(0, 5, 0, 0)
            };
            
            var btnSet = new Button 
            { 
                Text = "✓ Set", 
                Width = 100, 
                Height = 30,
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnSet.Click += (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(txtMyMessage.Text))
                {
                    SetWallMessage(roomName, txtMyMessage.Text);
                    MessageBox.Show("Mensaje establecido en el muro", "Room Wall", 
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    form.Close();
                }
            };
            
            var btnClear = new Button 
            { 
                Text = "✗ Clear", 
                Width = 100, 
                Height = 30,
                BackColor = Color.FromArgb(180, 0, 0),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnClear.Click += (s, e) =>
            {
                ClearWallMessage(roomName);
                txtMyMessage.Text = "";
                MessageBox.Show("Mensaje eliminado del muro", "Room Wall", 
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            
            btnPanel.Controls.Add(btnSet);
            btnPanel.Controls.Add(btnClear);
            
            bottomPanel.Controls.Add(btnPanel);
            bottomPanel.Controls.Add(txtMyMessage);
            bottomPanel.Controls.Add(lblMyMsg);
            
            form.Controls.Add(lvWall);
            form.Controls.Add(bottomPanel);
            
            return form;
        }
        
        public void SetWallMessage(string roomName, string message)
        {
            myWallMessages[roomName] = message;
            
            if (!roomWalls.ContainsKey(roomName))
            {
                roomWalls[roomName] = new List<WallMessage>();
            }
            
            var existing = roomWalls[roomName].FirstOrDefault(w => w.Username == currentUsername);
            if (existing != null)
            {
                existing.Message = message;
                existing.SetTime = DateTime.Now;
            }
            else
            {
                roomWalls[roomName].Add(new WallMessage
                {
                    Username = currentUsername,
                    Message = message,
                    SetTime = DateTime.Now,
                    IsOnline = true
                });
            }
            
            logAction?.Invoke($"📝 Mensaje establecido en muro de {roomName}");
            SaveRoomData();
        }
        
        public void ClearWallMessage(string roomName)
        {
            myWallMessages.Remove(roomName);
            
            if (roomWalls.ContainsKey(roomName))
            {
                roomWalls[roomName].RemoveAll(w => w.Username == currentUsername);
            }
            
            logAction?.Invoke($"🗑️ Mensaje eliminado del muro de {roomName}");
            SaveRoomData();
        }
        
        private bool IsPublicRoom(string roomName)
        {
            var publicRooms = new[] { "Public", "Music", "Books", "Movies", "Games", "Software" };
            return publicRooms.Any(r => r.Equals(roomName, StringComparison.OrdinalIgnoreCase));
        }
        
        private string GetRelativeTime(DateTime time)
        {
            var diff = DateTime.Now - time;
            if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
            if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";
            if (diff.TotalDays < 7) return $"{(int)diff.TotalDays}d ago";
            return time.ToString("MMM dd");
        }
        
        private void LoadRoomData()
        {
            try
            {
                string roomsPath = Path.Combine(dataDir, "chat_rooms.json");
                if (File.Exists(roomsPath))
                {
                    var json = File.ReadAllText(roomsPath);
                    var data = JsonSerializer.Deserialize<ChatRoomData>(json);
                    
                    if (data != null)
                    {
                        joinedRooms = data.JoinedRooms?.ToDictionary(r => r.Name, StringComparer.OrdinalIgnoreCase) 
                            ?? new Dictionary<string, ChatRoom>(StringComparer.OrdinalIgnoreCase);
                        mutedRooms = new HashSet<string>(data.MutedRooms ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
                        roomWalls = data.RoomWalls ?? new Dictionary<string, List<WallMessage>>(StringComparer.OrdinalIgnoreCase);
                        myWallMessages = data.MyWallMessages ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    }
                }
            }
            catch (Exception ex)
            {
                logAction?.Invoke($"⚠️ Error cargando datos de rooms: {ex.Message}");
            }
        }
        
        private void SaveRoomData()
        {
            try
            {
                var data = new ChatRoomData
                {
                    JoinedRooms = joinedRooms.Values.ToList(),
                    MutedRooms = mutedRooms.ToList(),
                    RoomWalls = roomWalls,
                    MyWallMessages = myWallMessages
                };
                
                string roomsPath = Path.Combine(dataDir, "chat_rooms.json");
                var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(roomsPath, json);
            }
            catch (Exception ex)
            {
                logAction?.Invoke($"⚠️ Error guardando datos de rooms: {ex.Message}");
            }
        }
        
        private class ChatRoomData
        {
            public List<ChatRoom> JoinedRooms { get; set; }
            public List<string> MutedRooms { get; set; }
            public Dictionary<string, List<WallMessage>> RoomWalls { get; set; }
            public Dictionary<string, string> MyWallMessages { get; set; }
        }
    }
}
