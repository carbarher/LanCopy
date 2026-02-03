using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;
using System.Drawing;

namespace SlskDown
{
    public class PrivateRoom
    {
        public string Name { get; set; }
        public string Owner { get; set; }
        public List<string> Members { get; set; } = new List<string>();
        public List<string> Operators { get; set; } = new List<string>();
        public bool IsOwner { get; set; }
        public bool IsOperator { get; set; }
        public DateTime JoinedDate { get; set; }
    }
    
    public class RoomInvitation
    {
        public string RoomName { get; set; }
        public string InvitedBy { get; set; }
        public DateTime InvitedDate { get; set; }
        public bool Accepted { get; set; }
    }
    
    public class PrivateRoomsManager
    {
        private Dictionary<string, PrivateRoom> privateRooms = new Dictionary<string, PrivateRoom>(StringComparer.OrdinalIgnoreCase);
        private List<RoomInvitation> invitations = new List<RoomInvitation>();
        private string dataDir;
        private Action<string> logAction;
        private string currentUsername;
        
        public PrivateRoomsManager(string dataDirectory, Action<string> logger, string username)
        {
            dataDir = dataDirectory;
            logAction = logger;
            currentUsername = username;
            LoadRooms();
        }
        
        public void AddPrivateRoom(string roomName, string owner, List<string> members, List<string> operators)
        {
            privateRooms[roomName] = new PrivateRoom
            {
                Name = roomName,
                Owner = owner,
                Members = members ?? new List<string>(),
                Operators = operators ?? new List<string>(),
                IsOwner = owner.Equals(currentUsername, StringComparison.OrdinalIgnoreCase),
                IsOperator = operators?.Contains(currentUsername, StringComparer.OrdinalIgnoreCase) ?? false,
                JoinedDate = DateTime.Now
            };
            
            SaveRooms();
            logAction?.Invoke($"🔒 Room privado agregado: {roomName}");
        }
        
        public void UpdateRoomMembers(string roomName, List<string> members)
        {
            if (privateRooms.TryGetValue(roomName, out var room))
            {
                room.Members = members;
                SaveRooms();
                logAction?.Invoke($"👥 Miembros actualizados en {roomName}: {members.Count}");
            }
        }
        
        public void UpdateRoomOperators(string roomName, List<string> operators)
        {
            if (privateRooms.TryGetValue(roomName, out var room))
            {
                room.Operators = operators;
                room.IsOperator = operators.Contains(currentUsername, StringComparer.OrdinalIgnoreCase);
                SaveRooms();
                logAction?.Invoke($"👮 Operadores actualizados en {roomName}: {operators.Count}");
            }
        }
        
        public void AddMember(string roomName, string username)
        {
            if (privateRooms.TryGetValue(roomName, out var room))
            {
                if (!room.Members.Contains(username, StringComparer.OrdinalIgnoreCase))
                {
                    room.Members.Add(username);
                    SaveRooms();
                    logAction?.Invoke($"➕ Miembro agregado a {roomName}: {username}");
                }
            }
        }
        
        public void RemoveMember(string roomName, string username)
        {
            if (privateRooms.TryGetValue(roomName, out var room))
            {
                room.Members.RemoveAll(m => m.Equals(username, StringComparison.OrdinalIgnoreCase));
                SaveRooms();
                logAction?.Invoke($"➖ Miembro eliminado de {roomName}: {username}");
            }
        }
        
        public void AddOperator(string roomName, string username)
        {
            if (privateRooms.TryGetValue(roomName, out var room))
            {
                if (!room.Operators.Contains(username, StringComparer.OrdinalIgnoreCase))
                {
                    room.Operators.Add(username);
                    
                    if (username.Equals(currentUsername, StringComparison.OrdinalIgnoreCase))
                    {
                        room.IsOperator = true;
                    }
                    
                    SaveRooms();
                    logAction?.Invoke($"👮 Operador agregado a {roomName}: {username}");
                }
            }
        }
        
        public void RemoveOperator(string roomName, string username)
        {
            if (privateRooms.TryGetValue(roomName, out var room))
            {
                room.Operators.RemoveAll(o => o.Equals(username, StringComparison.OrdinalIgnoreCase));
                
                if (username.Equals(currentUsername, StringComparison.OrdinalIgnoreCase))
                {
                    room.IsOperator = false;
                }
                
                SaveRooms();
                logAction?.Invoke($"👮 Operador eliminado de {roomName}: {username}");
            }
        }
        
        public void AddInvitation(string roomName, string invitedBy)
        {
            var invitation = new RoomInvitation
            {
                RoomName = roomName,
                InvitedBy = invitedBy,
                InvitedDate = DateTime.Now,
                Accepted = false
            };
            
            invitations.Add(invitation);
            SaveRooms();
            
            logAction?.Invoke($"📨 Invitación recibida a {roomName} de {invitedBy}");
            
            MessageBox.Show(
                $"Has sido invitado al room privado '{roomName}' por {invitedBy}.\n\n" +
                $"¿Deseas aceptar la invitación?",
                "Invitación a Room Privado",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question
            );
        }
        
        public void AcceptInvitation(string roomName)
        {
            var invitation = invitations.FirstOrDefault(i => 
                i.RoomName.Equals(roomName, StringComparison.OrdinalIgnoreCase) && !i.Accepted);
            
            if (invitation != null)
            {
                invitation.Accepted = true;
                SaveRooms();
                logAction?.Invoke($"✅ Invitación aceptada: {roomName}");
            }
        }
        
        public bool IsPrivateRoom(string roomName)
        {
            return privateRooms.ContainsKey(roomName);
        }
        
        public bool IsMember(string roomName)
        {
            if (privateRooms.TryGetValue(roomName, out var room))
            {
                return room.Members.Contains(currentUsername, StringComparer.OrdinalIgnoreCase);
            }
            return false;
        }
        
        public bool IsOperator(string roomName)
        {
            if (privateRooms.TryGetValue(roomName, out var room))
            {
                return room.IsOperator;
            }
            return false;
        }
        
        public bool IsOwner(string roomName)
        {
            if (privateRooms.TryGetValue(roomName, out var room))
            {
                return room.IsOwner;
            }
            return false;
        }
        
        public List<PrivateRoom> GetMyPrivateRooms()
        {
            return privateRooms.Values.ToList();
        }
        
        public Panel CreatePrivateRoomsPanel()
        {
            var panel = new Panel 
            { 
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(30, 30, 30),
                Padding = new Padding(10)
            };
            
            var splitContainer = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterDistance = 300,
                BackColor = Color.FromArgb(40, 40, 40)
            };
            
            // Panel superior: Mis rooms privados
            var topPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(5) };
            
            var lblMyRooms = new Label
            {
                Text = $"🔒 Mis Rooms Privados ({privateRooms.Count})",
                Dock = DockStyle.Top,
                Height = 30,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            };
            
            var lvRooms = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                BackColor = Color.FromArgb(35, 35, 35),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 9)
            };
            
            lvRooms.Columns.Add("Room", 150);
            lvRooms.Columns.Add("Rol", 80);
            lvRooms.Columns.Add("Owner", 100);
            lvRooms.Columns.Add("Miembros", 80);
            lvRooms.Columns.Add("Operadores", 80);
            
            foreach (var room in privateRooms.Values.OrderBy(r => r.Name))
            {
                var item = new ListViewItem(room.Name);
                
                string role = room.IsOwner ? "👑 Owner" : 
                             room.IsOperator ? "👮 Operator" : 
                             "👤 Member";
                item.SubItems.Add(role);
                
                if (room.IsOwner)
                    item.ForeColor = Color.Gold;
                else if (room.IsOperator)
                    item.ForeColor = Color.LightBlue;
                
                item.SubItems.Add(room.Owner);
                item.SubItems.Add(room.Members.Count.ToString());
                item.SubItems.Add(room.Operators.Count.ToString());
                
                lvRooms.Items.Add(item);
            }
            
            topPanel.Controls.Add(lvRooms);
            topPanel.Controls.Add(lblMyRooms);
            
            // Panel inferior: Invitaciones pendientes
            var bottomPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(5) };
            
            var pendingInvitations = invitations.Where(i => !i.Accepted).ToList();
            
            var lblInvitations = new Label
            {
                Text = $"📨 Invitaciones Pendientes ({pendingInvitations.Count})",
                Dock = DockStyle.Top,
                Height = 30,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            };
            
            var lvInvitations = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                BackColor = Color.FromArgb(35, 35, 35),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 9)
            };
            
            lvInvitations.Columns.Add("Room", 200);
            lvInvitations.Columns.Add("Invitado por", 150);
            lvInvitations.Columns.Add("Fecha", 120);
            
            foreach (var invitation in pendingInvitations.OrderByDescending(i => i.InvitedDate))
            {
                var item = new ListViewItem(invitation.RoomName);
                item.SubItems.Add(invitation.InvitedBy);
                item.SubItems.Add(invitation.InvitedDate.ToString("yyyy-MM-dd HH:mm"));
                item.Tag = invitation;
                item.ForeColor = Color.Yellow;
                
                lvInvitations.Items.Add(item);
            }
            
            bottomPanel.Controls.Add(lvInvitations);
            bottomPanel.Controls.Add(lblInvitations);
            
            splitContainer.Panel1.Controls.Add(topPanel);
            splitContainer.Panel2.Controls.Add(bottomPanel);
            
            panel.Controls.Add(splitContainer);
            
            return panel;
        }
        
        private void LoadRooms()
        {
            try
            {
                string path = Path.Combine(dataDir, "private_rooms.json");
                if (File.Exists(path))
                {
                    var json = File.ReadAllText(path);
                    var data = JsonSerializer.Deserialize<PrivateRoomsData>(json);
                    
                    if (data != null)
                    {
                        privateRooms = data.PrivateRooms?.ToDictionary(
                            r => r.Name,
                            r => r,
                            StringComparer.OrdinalIgnoreCase
                        ) ?? new Dictionary<string, PrivateRoom>(StringComparer.OrdinalIgnoreCase);
                        
                        invitations = data.Invitations ?? new List<RoomInvitation>();
                    }
                }
            }
            catch (Exception ex)
            {
                logAction?.Invoke($"⚠️ Error cargando rooms privados: {ex.Message}");
            }
        }
        
        private void SaveRooms()
        {
            try
            {
                var data = new PrivateRoomsData
                {
                    PrivateRooms = privateRooms.Values.ToList(),
                    Invitations = invitations
                };
                
                string path = Path.Combine(dataDir, "private_rooms.json");
                var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                logAction?.Invoke($"⚠️ Error guardando rooms privados: {ex.Message}");
            }
        }
        
        private class PrivateRoomsData
        {
            public List<PrivateRoom> PrivateRooms { get; set; }
            public List<RoomInvitation> Invitations { get; set; }
        }
    }
}
