using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;
using System.Drawing;

namespace SlskDown
{
    public class UserInterest
    {
        public string Interest { get; set; }
        public bool IsLiked { get; set; }
    }
    
    public class SimilarUser
    {
        public string Username { get; set; }
        public int SimilarityScore { get; set; }
        public List<string> CommonInterests { get; set; } = new List<string>();
    }
    
    public class Recommendation
    {
        public string Item { get; set; }
        public int Score { get; set; }
        public string Source { get; set; }
    }
    
    public class InterestsSystem
    {
        private List<string> likedInterests = new List<string>();
        private List<string> hatedInterests = new List<string>();
        private List<SimilarUser> similarUsers = new List<SimilarUser>();
        private List<Recommendation> globalRecommendations = new List<Recommendation>();
        private List<Recommendation> personalRecommendations = new List<Recommendation>();
        private string dataDir;
        private Action<string> logAction;
        
        public InterestsSystem(string dataDirectory, Action<string> logger)
        {
            dataDir = dataDirectory;
            logAction = logger;
            LoadInterests();
        }
        
        public void AddLikedInterest(string interest)
        {
            if (!likedInterests.Contains(interest))
            {
                likedInterests.Add(interest);
                hatedInterests.Remove(interest);
                SaveInterests();
                logAction?.Invoke($"💚 Interés agregado: {interest}");
            }
        }
        
        public void AddHatedInterest(string interest)
        {
            if (!hatedInterests.Contains(interest))
            {
                hatedInterests.Add(interest);
                likedInterests.Remove(interest);
                SaveInterests();
                logAction?.Invoke($"💔 Interés rechazado: {interest}");
            }
        }
        
        public void RemoveLikedInterest(string interest)
        {
            if (likedInterests.Remove(interest))
            {
                SaveInterests();
                logAction?.Invoke($"🗑️ Interés eliminado: {interest}");
            }
        }
        
        public void RemoveHatedInterest(string interest)
        {
            if (hatedInterests.Remove(interest))
            {
                SaveInterests();
                logAction?.Invoke($"🗑️ Interés rechazado eliminado: {interest}");
            }
        }
        
        public List<string> GetLikedInterests() => new List<string>(likedInterests);
        public List<string> GetHatedInterests() => new List<string>(hatedInterests);
        
        public void UpdateSimilarUsers(List<SimilarUser> users)
        {
            similarUsers = users.OrderByDescending(u => u.SimilarityScore).ToList();
            logAction?.Invoke($"👥 {users.Count} usuarios similares actualizados");
        }
        
        public List<SimilarUser> GetSimilarUsers(int minScore = 0)
        {
            return similarUsers.Where(u => u.SimilarityScore >= minScore).ToList();
        }
        
        public void UpdateGlobalRecommendations(List<Recommendation> recommendations)
        {
            globalRecommendations = recommendations.OrderByDescending(r => r.Score).ToList();
            logAction?.Invoke($"🌐 {recommendations.Count} recomendaciones globales actualizadas");
        }
        
        public void UpdatePersonalRecommendations(List<Recommendation> recommendations)
        {
            personalRecommendations = recommendations.OrderByDescending(r => r.Score).ToList();
            logAction?.Invoke($"🎯 {recommendations.Count} recomendaciones personales actualizadas");
        }
        
        public List<Recommendation> GetGlobalRecommendations(int count = 20)
        {
            return globalRecommendations.Take(count).ToList();
        }
        
        public List<Recommendation> GetPersonalRecommendations(int count = 20)
        {
            return personalRecommendations.Where(r => r.Score > 0).Take(count).ToList();
        }
        
        public int CalculateSimilarity(List<string> otherLikes, List<string> otherHates)
        {
            int commonLikes = likedInterests.Intersect(otherLikes).Count();
            int commonHates = hatedInterests.Intersect(otherHates).Count();
            int conflicts = likedInterests.Intersect(otherHates).Count() + 
                           hatedInterests.Intersect(otherLikes).Count();
            
            int totalInterests = likedInterests.Count + hatedInterests.Count + 
                                otherLikes.Count + otherHates.Count;
            
            if (totalInterests == 0) return 0;
            
            int score = ((commonLikes + commonHates) * 100 - conflicts * 50) / Math.Max(1, totalInterests);
            return Math.Max(0, Math.Min(100, score));
        }
        
        public Panel CreateInterestsPanel()
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
            
            // Panel superior: Mis intereses
            var topPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(5) };
            
            var lblMyInterests = new Label
            {
                Text = "💚 Mis Intereses",
                Dock = DockStyle.Top,
                Height = 30,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            };
            
            var flowLikes = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Color.FromArgb(35, 35, 35),
                Padding = new Padding(5)
            };
            
            foreach (var interest in likedInterests)
            {
                var btn = CreateInterestButton(interest, true);
                flowLikes.Controls.Add(btn);
            }
            
            var txtNewInterest = new TextBox
            {
                Dock = DockStyle.Bottom,
                Height = 30,
                BackColor = Color.FromArgb(50, 50, 50),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10)
            };
            
            txtNewInterest.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter && !string.IsNullOrWhiteSpace(txtNewInterest.Text))
                {
                    AddLikedInterest(txtNewInterest.Text.Trim());
                    flowLikes.Controls.Add(CreateInterestButton(txtNewInterest.Text.Trim(), true));
                    txtNewInterest.Clear();
                }
            };
            
            topPanel.Controls.Add(flowLikes);
            topPanel.Controls.Add(txtNewInterest);
            topPanel.Controls.Add(lblMyInterests);
            
            // Panel inferior: Usuarios similares
            var bottomPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(5) };
            
            var lblSimilarUsers = new Label
            {
                Text = "👥 Usuarios Similares",
                Dock = DockStyle.Top,
                Height = 30,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            };
            
            var lvSimilarUsers = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                BackColor = Color.FromArgb(35, 35, 35),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 9)
            };
            
            lvSimilarUsers.Columns.Add("Usuario", 150);
            lvSimilarUsers.Columns.Add("Similitud", 80);
            lvSimilarUsers.Columns.Add("Intereses Comunes", 300);
            
            foreach (var user in similarUsers.Take(50))
            {
                var item = new ListViewItem(user.Username);
                item.SubItems.Add($"{user.SimilarityScore}%");
                item.SubItems.Add(string.Join(", ", user.CommonInterests.Take(5)));
                
                if (user.SimilarityScore >= 80)
                    item.BackColor = Color.FromArgb(0, 60, 0);
                else if (user.SimilarityScore >= 50)
                    item.BackColor = Color.FromArgb(60, 60, 0);
                
                lvSimilarUsers.Items.Add(item);
            }
            
            bottomPanel.Controls.Add(lvSimilarUsers);
            bottomPanel.Controls.Add(lblSimilarUsers);
            
            splitContainer.Panel1.Controls.Add(topPanel);
            splitContainer.Panel2.Controls.Add(bottomPanel);
            
            panel.Controls.Add(splitContainer);
            
            return panel;
        }
        
        private Button CreateInterestButton(string interest, bool isLiked)
        {
            var btn = new Button
            {
                Text = $"{(isLiked ? "💚" : "💔")} {interest}",
                AutoSize = true,
                Height = 30,
                Margin = new Padding(3),
                BackColor = isLiked ? Color.FromArgb(0, 100, 0) : Color.FromArgb(100, 0, 0),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            
            btn.Click += (s, e) =>
            {
                if (isLiked)
                    RemoveLikedInterest(interest);
                else
                    RemoveHatedInterest(interest);
                
                btn.Parent?.Controls.Remove(btn);
            };
            
            return btn;
        }
        
        private void LoadInterests()
        {
            try
            {
                string path = Path.Combine(dataDir, "interests.json");
                if (File.Exists(path))
                {
                    var json = File.ReadAllText(path);
                    var data = JsonSerializer.Deserialize<InterestsData>(json);
                    
                    if (data != null)
                    {
                        likedInterests = data.LikedInterests ?? new List<string>();
                        hatedInterests = data.HatedInterests ?? new List<string>();
                        similarUsers = data.SimilarUsers ?? new List<SimilarUser>();
                        globalRecommendations = data.GlobalRecommendations ?? new List<Recommendation>();
                        personalRecommendations = data.PersonalRecommendations ?? new List<Recommendation>();
                    }
                }
            }
            catch (Exception ex)
            {
                logAction?.Invoke($"⚠️ Error cargando intereses: {ex.Message}");
            }
        }
        
        private void SaveInterests()
        {
            try
            {
                var data = new InterestsData
                {
                    LikedInterests = likedInterests,
                    HatedInterests = hatedInterests,
                    SimilarUsers = similarUsers,
                    GlobalRecommendations = globalRecommendations,
                    PersonalRecommendations = personalRecommendations
                };
                
                string path = Path.Combine(dataDir, "interests.json");
                var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                logAction?.Invoke($"⚠️ Error guardando intereses: {ex.Message}");
            }
        }
        
        private class InterestsData
        {
            public List<string> LikedInterests { get; set; }
            public List<string> HatedInterests { get; set; }
            public List<SimilarUser> SimilarUsers { get; set; }
            public List<Recommendation> GlobalRecommendations { get; set; }
            public List<Recommendation> PersonalRecommendations { get; set; }
        }
    }
}
