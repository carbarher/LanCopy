using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;
using System.Drawing;

namespace SlskDown
{
    public class PrivilegedUser
    {
        public string Username { get; set; }
        public DateTime GrantedDate { get; set; }
        public DateTime ExpiryDate { get; set; }
        public int DaysRemaining => Math.Max(0, (ExpiryDate - DateTime.Now).Days);
        public bool IsActive => DateTime.Now < ExpiryDate;
    }
    
    public class PrivilegeGift
    {
        public string FromUsername { get; set; }
        public string ToUsername { get; set; }
        public int Days { get; set; }
        public DateTime GiftedDate { get; set; }
    }
    
    public class PrivilegedUsersManager
    {
        private HashSet<string> privilegedUsers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, PrivilegedUser> myPrivileges = new Dictionary<string, PrivilegedUser>(StringComparer.OrdinalIgnoreCase);
        private List<PrivilegeGift> giftHistory = new List<PrivilegeGift>();
        private string dataDir;
        private Action<string> logAction;
        private string currentUsername;
        
        public PrivilegedUsersManager(string dataDirectory, Action<string> logger, string username)
        {
            dataDir = dataDirectory;
            logAction = logger;
            currentUsername = username;
            LoadPrivileges();
        }
        
        public void UpdatePrivilegedUsersList(List<string> users)
        {
            privilegedUsers.Clear();
            foreach (var user in users)
            {
                privilegedUsers.Add(user);
            }
            
            logAction?.Invoke($"👑 {users.Count} usuarios privilegiados actualizados");
            SavePrivileges();
        }
        
        public bool IsPrivileged(string username)
        {
            return privilegedUsers.Contains(username);
        }
        
        public void UpdateMyPrivileges(int daysRemaining)
        {
            if (!myPrivileges.ContainsKey(currentUsername))
            {
                myPrivileges[currentUsername] = new PrivilegedUser
                {
                    Username = currentUsername,
                    GrantedDate = DateTime.Now
                };
            }
            
            myPrivileges[currentUsername].ExpiryDate = DateTime.Now.AddDays(daysRemaining);
            
            logAction?.Invoke($"👑 Privilegios actualizados: {daysRemaining} días restantes");
            SavePrivileges();
        }
        
        public void RecordGiftReceived(string fromUsername, int days)
        {
            var gift = new PrivilegeGift
            {
                FromUsername = fromUsername,
                ToUsername = currentUsername,
                Days = days,
                GiftedDate = DateTime.Now
            };
            
            giftHistory.Add(gift);
            
            if (!myPrivileges.ContainsKey(currentUsername))
            {
                myPrivileges[currentUsername] = new PrivilegedUser
                {
                    Username = currentUsername,
                    GrantedDate = DateTime.Now,
                    ExpiryDate = DateTime.Now.AddDays(days)
                };
            }
            else
            {
                myPrivileges[currentUsername].ExpiryDate = 
                    myPrivileges[currentUsername].ExpiryDate.AddDays(days);
            }
            
            logAction?.Invoke($"🎁 Privilegios recibidos de {fromUsername}: {days} días");
            
            MessageBox.Show(
                $"¡{fromUsername} te ha regalado {days} días de privilegios!\n\n" +
                $"Días totales restantes: {myPrivileges[currentUsername].DaysRemaining}",
                "Regalo de Privilegios",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
            
            SavePrivileges();
        }
        
        public void RecordGiftSent(string toUsername, int days)
        {
            var gift = new PrivilegeGift
            {
                FromUsername = currentUsername,
                ToUsername = toUsername,
                Days = days,
                GiftedDate = DateTime.Now
            };
            
            giftHistory.Add(gift);
            
            logAction?.Invoke($"🎁 Privilegios enviados a {toUsername}: {days} días");
            SavePrivileges();
        }
        
        public int GetMyDaysRemaining()
        {
            if (myPrivileges.TryGetValue(currentUsername, out var priv))
            {
                return priv.DaysRemaining;
            }
            return 0;
        }
        
        public bool AmIPrivileged()
        {
            if (myPrivileges.TryGetValue(currentUsername, out var priv))
            {
                return priv.IsActive;
            }
            return false;
        }
        
        public int GetPriorityBoost(string username)
        {
            return IsPrivileged(username) ? 100 : 0;
        }
        
        public Panel CreatePrivilegesPanel()
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
                Orientation = Orientation.Vertical,
                SplitterDistance = 300,
                BackColor = Color.FromArgb(40, 40, 40)
            };
            
            // Panel izquierdo: Mis privilegios
            var leftPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(5) };
            
            var lblMyPrivileges = new Label
            {
                Text = "👑 Mis Privilegios",
                Dock = DockStyle.Top,
                Height = 30,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            };
            
            var statusPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 150,
                BackColor = Color.FromArgb(40, 40, 40),
                Padding = new Padding(10)
            };
            
            int daysRemaining = GetMyDaysRemaining();
            bool isActive = AmIPrivileged();
            
            var lblStatus = new Label
            {
                Text = isActive ? "✅ ACTIVO" : "❌ INACTIVO",
                Location = new Point(10, 10),
                AutoSize = true,
                ForeColor = isActive ? Color.LightGreen : Color.Red,
                Font = new Font("Segoe UI", 14, FontStyle.Bold)
            };
            
            var lblDays = new Label
            {
                Text = $"Días restantes: {daysRemaining}",
                Location = new Point(10, 45),
                AutoSize = true,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 12)
            };
            
            var lblBenefits = new Label
            {
                Text = "Beneficios:\n• Prioridad en colas de descarga\n• Badge especial en UI\n• Acceso a features premium",
                Location = new Point(10, 75),
                AutoSize = true,
                ForeColor = Color.LightGray,
                Font = new Font("Segoe UI", 9)
            };
            
            statusPanel.Controls.AddRange(new Control[] { lblStatus, lblDays, lblBenefits });
            
            var lblGiftHistory = new Label
            {
                Text = "🎁 Historial de Regalos",
                Dock = DockStyle.Top,
                Height = 30,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            };
            
            var lvGifts = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                BackColor = Color.FromArgb(35, 35, 35),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 9)
            };
            
            lvGifts.Columns.Add("De/Para", 100);
            lvGifts.Columns.Add("Días", 50);
            lvGifts.Columns.Add("Fecha", 100);
            
            foreach (var gift in giftHistory.OrderByDescending(g => g.GiftedDate).Take(50))
            {
                var item = new ListViewItem();
                
                if (gift.FromUsername.Equals(currentUsername, StringComparison.OrdinalIgnoreCase))
                {
                    item.Text = $"→ {gift.ToUsername}";
                    item.ForeColor = Color.Orange;
                }
                else
                {
                    item.Text = $"← {gift.FromUsername}";
                    item.ForeColor = Color.LightGreen;
                }
                
                item.SubItems.Add(gift.Days.ToString());
                item.SubItems.Add(gift.GiftedDate.ToString("yyyy-MM-dd"));
                
                lvGifts.Items.Add(item);
            }
            
            leftPanel.Controls.Add(lvGifts);
            leftPanel.Controls.Add(lblGiftHistory);
            leftPanel.Controls.Add(statusPanel);
            leftPanel.Controls.Add(lblMyPrivileges);
            
            // Panel derecho: Usuarios privilegiados
            var rightPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(5) };
            
            var lblPrivilegedUsers = new Label
            {
                Text = $"👑 Usuarios Privilegiados ({privilegedUsers.Count})",
                Dock = DockStyle.Top,
                Height = 30,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            };
            
            var txtSearch = new TextBox
            {
                Dock = DockStyle.Top,
                Height = 25,
                BackColor = Color.FromArgb(50, 50, 50),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10)
            };
            txtSearch.Text = "🔍 Buscar usuario...";
            txtSearch.ForeColor = Color.Gray;
            
            var lvPrivileged = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                BackColor = Color.FromArgb(35, 35, 35),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 9)
            };
            
            lvPrivileged.Columns.Add("Usuario", 200);
            lvPrivileged.Columns.Add("Estado", 80);
            
            foreach (var user in privilegedUsers.OrderBy(u => u))
            {
                var item = new ListViewItem(user);
                item.SubItems.Add("👑 Activo");
                item.ForeColor = Color.Gold;
                lvPrivileged.Items.Add(item);
            }
            
            txtSearch.TextChanged += (s, e) =>
            {
                if (txtSearch.Text == "🔍 Buscar usuario..." || string.IsNullOrWhiteSpace(txtSearch.Text))
                    return;
                
                lvPrivileged.Items.Clear();
                var filtered = privilegedUsers.Where(u => 
                    u.IndexOf(txtSearch.Text, StringComparison.OrdinalIgnoreCase) >= 0);
                
                foreach (var user in filtered.OrderBy(u => u))
                {
                    var item = new ListViewItem(user);
                    item.SubItems.Add("👑 Activo");
                    item.ForeColor = Color.Gold;
                    lvPrivileged.Items.Add(item);
                }
            };
            
            txtSearch.Enter += (s, e) =>
            {
                if (txtSearch.Text == "🔍 Buscar usuario...")
                {
                    txtSearch.Text = "";
                    txtSearch.ForeColor = Color.White;
                }
            };
            
            txtSearch.Leave += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(txtSearch.Text))
                {
                    txtSearch.Text = "🔍 Buscar usuario...";
                    txtSearch.ForeColor = Color.Gray;
                }
            };
            
            rightPanel.Controls.Add(lvPrivileged);
            rightPanel.Controls.Add(txtSearch);
            rightPanel.Controls.Add(lblPrivilegedUsers);
            
            splitContainer.Panel1.Controls.Add(leftPanel);
            splitContainer.Panel2.Controls.Add(rightPanel);
            
            panel.Controls.Add(splitContainer);
            
            return panel;
        }
        
        private void LoadPrivileges()
        {
            try
            {
                string path = Path.Combine(dataDir, "privileges.json");
                if (File.Exists(path))
                {
                    var json = File.ReadAllText(path);
                    var data = JsonSerializer.Deserialize<PrivilegesData>(json);
                    
                    if (data != null)
                    {
                        privilegedUsers = new HashSet<string>(data.PrivilegedUsers ?? new List<string>(), 
                            StringComparer.OrdinalIgnoreCase);
                        myPrivileges = data.MyPrivileges ?? new Dictionary<string, PrivilegedUser>();
                        giftHistory = data.GiftHistory ?? new List<PrivilegeGift>();
                    }
                }
            }
            catch (Exception ex)
            {
                logAction?.Invoke($"⚠️ Error cargando privilegios: {ex.Message}");
            }
        }
        
        private void SavePrivileges()
        {
            try
            {
                var data = new PrivilegesData
                {
                    PrivilegedUsers = privilegedUsers.ToList(),
                    MyPrivileges = myPrivileges,
                    GiftHistory = giftHistory
                };
                
                string path = Path.Combine(dataDir, "privileges.json");
                var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                logAction?.Invoke($"⚠️ Error guardando privilegios: {ex.Message}");
            }
        }
        
        private class PrivilegesData
        {
            public List<string> PrivilegedUsers { get; set; }
            public Dictionary<string, PrivilegedUser> MyPrivileges { get; set; }
            public List<PrivilegeGift> GiftHistory { get; set; }
        }
    }
}
