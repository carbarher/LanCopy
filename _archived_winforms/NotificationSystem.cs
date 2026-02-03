using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Drawing;
using System.Linq;

namespace SlskDown
{
    public class NotificationDropdown
    {
        private Func<int> getUnreadPrivateMessages;
        private Func<int> getWishlistMatches;
        private Func<int> getRoomMentions;
        private Action goToPrivateMessages;
        private Action goToWishlist;
        private Action goToRooms;
        private Action<string> logAction;
        
        public NotificationDropdown(
            Func<int> unreadPM,
            Func<int> wishlistMatches,
            Func<int> roomMentions,
            Action goPM,
            Action goWishlist,
            Action goRooms,
            Action<string> logger)
        {
            getUnreadPrivateMessages = unreadPM;
            getWishlistMatches = wishlistMatches;
            getRoomMentions = roomMentions;
            goToPrivateMessages = goPM;
            goToWishlist = goWishlist;
            goToRooms = goRooms;
            logAction = logger;
        }
        
        public ToolStripDropDownButton CreateNotificationButton()
        {
            var btn = new ToolStripDropDownButton
            {
                Text = "🔔",
                ToolTipText = "Notifications",
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                Font = new Font("Segoe UI", 12),
                ForeColor = Color.White,
                AutoSize = true
            };
            
            btn.DropDownOpening += (s, e) =>
            {
                btn.DropDownItems.Clear();
                
                int totalNotifications = 0;
                
                int unreadPM = getUnreadPrivateMessages?.Invoke() ?? 0;
                if (unreadPM > 0)
                {
                    var item = new ToolStripMenuItem($"💬 {unreadPM} mensaje(s) privado(s)");
                    item.Click += (s2, e2) => goToPrivateMessages?.Invoke();
                    btn.DropDownItems.Add(item);
                    totalNotifications += unreadPM;
                }
                
                int wishlistMatches = getWishlistMatches?.Invoke() ?? 0;
                if (wishlistMatches > 0)
                {
                    var item = new ToolStripMenuItem($"🎯 {wishlistMatches} wishlist match(es)");
                    item.Click += (s2, e2) => goToWishlist?.Invoke();
                    btn.DropDownItems.Add(item);
                    totalNotifications += wishlistMatches;
                }
                
                int roomMentions = getRoomMentions?.Invoke() ?? 0;
                if (roomMentions > 0)
                {
                    var item = new ToolStripMenuItem($"💬 {roomMentions} mención(es) en rooms");
                    item.Click += (s2, e2) => goToRooms?.Invoke();
                    btn.DropDownItems.Add(item);
                    totalNotifications += roomMentions;
                }
                
                if (btn.DropDownItems.Count == 0)
                {
                    var item = new ToolStripMenuItem("Sin notificaciones");
                    item.Enabled = false;
                    btn.DropDownItems.Add(item);
                }
                else
                {
                    btn.DropDownItems.Add(new ToolStripSeparator());
                    var clearItem = new ToolStripMenuItem("✓ Marcar todas como leídas");
                    clearItem.Click += (s2, e2) =>
                    {
                        logAction?.Invoke("✓ Todas las notificaciones marcadas como leídas");
                    };
                    btn.DropDownItems.Add(clearItem);
                }
                
                btn.Text = totalNotifications > 0 ? $"🔔 {totalNotifications}" : "🔔";
            };
            
            return btn;
        }
    }
    
    public class FolderDownloadDialog
    {
        public class DownloadOptions
        {
            public string TargetPath { get; set; }
            public List<string> SelectedFiles { get; set; }
            public bool Success { get; set; }
        }
        
        public static DownloadOptions ShowDialog(string folderName, List<FileInfo> files, string defaultPath)
        {
            var result = new DownloadOptions
            {
                TargetPath = defaultPath,
                SelectedFiles = new List<string>(),
                Success = false
            };
            
            var form = new Form
            {
                Text = $"Download Folder: {folderName}",
                Size = new Size(700, 600),
                StartPosition = FormStartPosition.CenterParent,
                BackColor = Color.FromArgb(30, 30, 30),
                FormBorderStyle = FormBorderStyle.Sizable,
                MinimumSize = new Size(600, 500)
            };
            
            var lblTarget = new Label 
            { 
                Text = "Target folder:",
                Location = new Point(10, 10),
                AutoSize = true,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10)
            };
            
            var txtTarget = new TextBox 
            { 
                Location = new Point(10, 35),
                Width = 570,
                Height = 25,
                Text = defaultPath,
                BackColor = Color.FromArgb(50, 50, 50),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10)
            };
            
            var btnBrowse = new Button 
            { 
                Text = "...",
                Location = new Point(590, 33),
                Width = 80,
                Height = 27,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            
            btnBrowse.Click += (s, e) =>
            {
                using (var fbd = new FolderBrowserDialog())
                {
                    fbd.SelectedPath = txtTarget.Text;
                    if (fbd.ShowDialog() == DialogResult.OK)
                    {
                        txtTarget.Text = fbd.SelectedPath;
                    }
                }
            };
            
            var lblFiles = new Label 
            { 
                Text = $"Files to download ({files.Count} files):",
                Location = new Point(10, 75),
                AutoSize = true,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10)
            };
            
            var lvFiles = new ListView
            {
                Location = new Point(10, 100),
                Size = new Size(660, 380),
                View = View.Details,
                CheckBoxes = true,
                FullRowSelect = true,
                BackColor = Color.FromArgb(40, 40, 40),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 9)
            };
            
            lvFiles.Columns.Add("File", 450);
            lvFiles.Columns.Add("Size", 100);
            lvFiles.Columns.Add("Extension", 100);
            
            long totalSize = 0;
            foreach (var file in files.OrderBy(f => f.FileName))
            {
                var item = new ListViewItem(file.FileName);
                item.Checked = true;
                item.SubItems.Add(FormatSize(file.Size));
                item.SubItems.Add(System.IO.Path.GetExtension(file.FileName));
                item.Tag = file;
                lvFiles.Items.Add(item);
                totalSize += file.Size;
            }
            
            var lblStats = new Label
            {
                Text = $"Total: {files.Count} files, {FormatSize(totalSize)}",
                Location = new Point(10, 490),
                AutoSize = true,
                ForeColor = Color.LightGray,
                Font = new Font("Segoe UI", 9)
            };
            
            var btnSelectAll = new Button
            {
                Text = "Select All",
                Location = new Point(350, 485),
                Width = 100,
                Height = 30,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnSelectAll.Click += (s, e) =>
            {
                foreach (ListViewItem item in lvFiles.Items)
                {
                    item.Checked = true;
                }
            };
            
            var btnSelectNone = new Button
            {
                Text = "Select None",
                Location = new Point(460, 485),
                Width = 100,
                Height = 30,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnSelectNone.Click += (s, e) =>
            {
                foreach (ListViewItem item in lvFiles.Items)
                {
                    item.Checked = false;
                }
            };
            
            var btnDownload = new Button 
            { 
                Text = "Download",
                Location = new Point(460, 525),
                Width = 100,
                Height = 35,
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10, FontStyle.Bold)
            };
            btnDownload.Click += (s, e) =>
            {
                result.TargetPath = txtTarget.Text;
                result.SelectedFiles = lvFiles.Items.Cast<ListViewItem>()
                    .Where(i => i.Checked)
                    .Select(i => ((FileInfo)i.Tag).FileName)
                    .ToList();
                result.Success = true;
                form.DialogResult = DialogResult.OK;
                form.Close();
            };
            
            var btnCancel = new Button 
            { 
                Text = "Cancel",
                Location = new Point(570, 525),
                Width = 100,
                Height = 35,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10)
            };
            btnCancel.Click += (s, e) =>
            {
                form.DialogResult = DialogResult.Cancel;
                form.Close();
            };
            
            form.Controls.AddRange(new Control[] 
            { 
                lblTarget, txtTarget, btnBrowse, 
                lblFiles, lvFiles, lblStats,
                btnSelectAll, btnSelectNone,
                btnDownload, btnCancel 
            });
            
            form.ShowDialog();
            
            return result;
        }
        
        private static string FormatSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
        
        public class FileInfo
        {
            public string FileName { get; set; }
            public long Size { get; set; }
        }
    }
}
