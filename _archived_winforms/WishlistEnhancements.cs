using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Forms;
using System.Drawing;
using System.Media;

namespace SlskDown
{
    // Sistema completo de Wishlist mejorado
    public class WishlistItem
    {
        public string SearchTerm { get; set; }
        public bool Enabled { get; set; } = true;
        
        // Filtros guardados
        public long MinSizeBytes { get; set; }
        public long MaxSizeBytes { get; set; } = long.MaxValue;
        public string Extension { get; set; }
        public int MinBitrate { get; set; }
        public int MinDuration { get; set; }
        public int MaxDuration { get; set; }
        public string FileType { get; set; }
        public bool OnlyFreeSlots { get; set; }
        
        // Configuración de notificaciones
        public bool EnableNotifications { get; set; } = true;
        public bool EnableSound { get; set; } = true;
        public bool AutoDownload { get; set; }
        
        // Estadísticas
        public int TotalResultsFound { get; set; }
        public int TotalDownloaded { get; set; }
        public DateTime LastMatch { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
    
    public class WishlistNotificationSystem
    {
        [DllImport("user32.dll")]
        private static extern bool FlashWindow(IntPtr hwnd, bool bInvert);
        
        [DllImport("user32.dll")]
        private static extern bool FlashWindowEx(ref FLASHWINFO pwfi);
        
        [StructLayout(LayoutKind.Sequential)]
        public struct FLASHWINFO
        {
            public uint cbSize;
            public IntPtr hwnd;
            public uint dwFlags;
            public uint uCount;
            public uint dwTimeout;
        }
        
        private const uint FLASHW_ALL = 3;
        private const uint FLASHW_TIMERNOFG = 12;
        
        private bool enableWishlistNotifications = true;
        private bool enableWishlistSound = true;
        private bool flashTaskbarIcon = true;
        private string wishlistSoundPath;
        private NotifyIcon notifyIcon;
        private Form mainForm;
        
        public WishlistNotificationSystem(Form form, NotifyIcon icon, string dataDir)
        {
            mainForm = form;
            notifyIcon = icon;
            wishlistSoundPath = Path.Combine(dataDir, "wishlist_alert.wav");
            
            // Crear sonido por defecto si no existe
            if (!File.Exists(wishlistSoundPath))
            {
                CreateDefaultWishlistSound();
            }
        }
        
        public void OnWishlistResultFound(string searchTerm, int resultCount, Action<string> logAction)
        {
            if (!enableWishlistNotifications) return;
            
            // 1. Notificación de Windows
            if (notifyIcon != null)
            {
                notifyIcon.ShowBalloonTip(
                    5000,
                    "🎯 Wishlist Match!",
                    $"Found {resultCount} results for '{searchTerm}'",
                    ToolTipIcon.Info
                );
            }
            
            // 2. Sonido de alerta
            if (enableWishlistSound && File.Exists(wishlistSoundPath))
            {
                try
                {
                    var player = new SoundPlayer(wishlistSoundPath);
                    player.Play();
                }
                catch (Exception ex)
                {
                    logAction?.Invoke($"Error reproduciendo sonido: {ex.Message}");
                }
            }
            
            // 3. Parpadeo en taskbar
            if (flashTaskbarIcon && mainForm != null)
            {
                FlashTaskbar();
            }
            
            // 4. Log con timestamp
            logAction?.Invoke($"🎯 WISHLIST MATCH: '{searchTerm}' - {resultCount} resultados a las {DateTime.Now:HH:mm:ss}");
        }
        
        private void FlashTaskbar()
        {
            try
            {
                FLASHWINFO fInfo = new FLASHWINFO();
                fInfo.cbSize = Convert.ToUInt32(Marshal.SizeOf(fInfo));
                fInfo.hwnd = mainForm.Handle;
                fInfo.dwFlags = FLASHW_ALL | FLASHW_TIMERNOFG;
                fInfo.uCount = 5;
                fInfo.dwTimeout = 0;
                
                FlashWindowEx(ref fInfo);
            }
            catch
            {
                // Fallback a método simple
                FlashWindow(mainForm.Handle, true);
            }
        }
        
        private void CreateDefaultWishlistSound()
        {
            // Crear un beep simple como sonido por defecto
            try
            {
                System.Media.SystemSounds.Exclamation.Play();
            }
            catch { }
        }
        
        public void SetNotificationSettings(bool notifications, bool sound, bool flash)
        {
            enableWishlistNotifications = notifications;
            enableWishlistSound = sound;
            flashTaskbarIcon = flash;
        }
    }
    
    public class WishlistResultFilter
    {
        private Dictionary<string, HashSet<string>> discardedResults = new Dictionary<string, HashSet<string>>();
        private bool enableDiscardPrevious = true;
        private string discardedResultsPath;
        
        public WishlistResultFilter(string dataDir)
        {
            discardedResultsPath = Path.Combine(dataDir, "wishlist_discarded.json");
            LoadDiscardedResults();
        }
        
        public List<T> FilterNewResults<T>(string searchTerm, List<T> newResults, Func<T, string> getKey)
        {
            if (!enableDiscardPrevious) return newResults;
            
            if (!discardedResults.ContainsKey(searchTerm))
            {
                discardedResults[searchTerm] = new HashSet<string>();
            }
            
            var filtered = new List<T>();
            
            foreach (var result in newResults)
            {
                string resultKey = getKey(result);
                
                if (!discardedResults[searchTerm].Contains(resultKey))
                {
                    filtered.Add(result);
                }
            }
            
            return filtered;
        }
        
        public void DiscardResult(string searchTerm, string resultKey)
        {
            if (!discardedResults.ContainsKey(searchTerm))
            {
                discardedResults[searchTerm] = new HashSet<string>();
            }
            
            discardedResults[searchTerm].Add(resultKey);
            SaveDiscardedResults();
        }
        
        public void ClearDiscardedResults(string searchTerm)
        {
            if (discardedResults.ContainsKey(searchTerm))
            {
                discardedResults[searchTerm].Clear();
                SaveDiscardedResults();
            }
        }
        
        public int GetDiscardedCount(string searchTerm)
        {
            return discardedResults.ContainsKey(searchTerm) ? discardedResults[searchTerm].Count : 0;
        }
        
        private void LoadDiscardedResults()
        {
            try
            {
                if (File.Exists(discardedResultsPath))
                {
                    var json = File.ReadAllText(discardedResultsPath);
                    var data = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(json);
                    
                    if (data != null)
                    {
                        discardedResults = data.ToDictionary(
                            kvp => kvp.Key,
                            kvp => new HashSet<string>(kvp.Value)
                        );
                    }
                }
            }
            catch { }
        }
        
        private void SaveDiscardedResults()
        {
            try
            {
                var data = discardedResults.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.ToList()
                );
                
                var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(discardedResultsPath, json);
            }
            catch { }
        }
        
        public void SetEnabled(bool enabled)
        {
            enableDiscardPrevious = enabled;
        }
    }
    
    public class WishlistManager
    {
        private List<WishlistItem> wishlistItems = new List<WishlistItem>();
        private string wishlistPath;
        
        public WishlistManager(string dataDir)
        {
            wishlistPath = Path.Combine(dataDir, "wishlist_advanced.json");
            LoadWishlist();
        }
        
        public void AddWishlistItem(WishlistItem item)
        {
            wishlistItems.Add(item);
            SaveWishlist();
        }
        
        public void RemoveWishlistItem(string searchTerm)
        {
            wishlistItems.RemoveAll(i => i.SearchTerm == searchTerm);
            SaveWishlist();
        }
        
        public void UpdateWishlistItem(WishlistItem item)
        {
            var existing = wishlistItems.FirstOrDefault(i => i.SearchTerm == item.SearchTerm);
            if (existing != null)
            {
                int index = wishlistItems.IndexOf(existing);
                wishlistItems[index] = item;
                SaveWishlist();
            }
        }
        
        public List<WishlistItem> GetEnabledItems()
        {
            return wishlistItems.Where(i => i.Enabled).ToList();
        }
        
        public List<WishlistItem> GetAllItems()
        {
            return new List<WishlistItem>(wishlistItems);
        }
        
        public WishlistItem GetItem(string searchTerm)
        {
            return wishlistItems.FirstOrDefault(i => i.SearchTerm == searchTerm);
        }
        
        public void UpdateStatistics(string searchTerm, int newResults, int downloaded)
        {
            var item = wishlistItems.FirstOrDefault(i => i.SearchTerm == searchTerm);
            if (item != null)
            {
                item.TotalResultsFound += newResults;
                item.TotalDownloaded += downloaded;
                item.LastMatch = DateTime.Now;
                SaveWishlist();
            }
        }
        
        private void LoadWishlist()
        {
            try
            {
                if (File.Exists(wishlistPath))
                {
                    var json = File.ReadAllText(wishlistPath);
                    wishlistItems = JsonSerializer.Deserialize<List<WishlistItem>>(json) ?? new List<WishlistItem>();
                }
            }
            catch { }
        }
        
        private void SaveWishlist()
        {
            try
            {
                var json = JsonSerializer.Serialize(wishlistItems, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(wishlistPath, json);
            }
            catch { }
        }
        
        public void ExportWishlist(string exportPath)
        {
            try
            {
                var json = JsonSerializer.Serialize(wishlistItems, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(exportPath, json);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error exportando wishlist: {ex.Message}");
            }
        }
        
        public void ImportWishlist(string importPath)
        {
            try
            {
                var json = File.ReadAllText(importPath);
                var imported = JsonSerializer.Deserialize<List<WishlistItem>>(json);
                
                if (imported != null)
                {
                    foreach (var item in imported)
                    {
                        if (!wishlistItems.Any(i => i.SearchTerm == item.SearchTerm))
                        {
                            wishlistItems.Add(item);
                        }
                    }
                    SaveWishlist();
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error importando wishlist: {ex.Message}");
            }
        }
    }
}
