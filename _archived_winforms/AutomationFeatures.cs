using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;

namespace SlskDown
{
    public class AutomationFeatures
    {
        private QueueManagementSystem queueManagement;
        private PrivilegedUsersManager privilegedUsersManager;
        private BuddyAutoBrowseSystem buddyAutoBrowse;
        private Action<string> logAction;
        private Timer autoPrioritizeTimer;
        private Timer autoBrowseTimer;
        private Func<List<object>> getDownloadQueue;
        private Func<List<string>> getBuddies;
        private Action saveQueue;
        
        public AutomationFeatures(
            QueueManagementSystem queue,
            PrivilegedUsersManager privileged,
            BuddyAutoBrowseSystem browse,
            Action<string> logger,
            Func<List<object>> queueGetter,
            Func<List<string>> buddiesGetter,
            Action queueSaver)
        {
            queueManagement = queue;
            privilegedUsersManager = privileged;
            buddyAutoBrowse = browse;
            logAction = logger;
            getDownloadQueue = queueGetter;
            getBuddies = buddiesGetter;
            saveQueue = queueSaver;
        }
        
        // ═══════════════════════════════════════════════════════════════
        // AUTO-PRIORIZACIÓN INTELIGENTE
        // ═══════════════════════════════════════════════════════════════
        
        public void EnableAutoPrioritization(int intervalMinutes = 5)
        {
            autoPrioritizeTimer = new Timer(intervalMinutes * 60 * 1000);
            autoPrioritizeTimer.Elapsed += (s, e) => AutoPrioritize();
            autoPrioritizeTimer.Start();
            
            logAction?.Invoke($"🤖 Auto-priorización habilitada (cada {intervalMinutes} min)");
        }
        
        public void DisableAutoPrioritization()
        {
            autoPrioritizeTimer?.Stop();
            autoPrioritizeTimer?.Dispose();
            logAction?.Invoke("🤖 Auto-priorización deshabilitada");
        }
        
        public void AutoPrioritize()
        {
            try
            {
                var queue = getDownloadQueue();
                if (queue == null || queue.Count == 0) return;
                
                int prioritized = 0;
                
                foreach (var item in queue)
                {
                    var task = item as dynamic;
                    if (task == null) continue;
                    
                    var currentPriority = (QueuePriority)(task.Priority ?? (int)QueuePriority.Normal);
                    QueuePriority newPriority = currentPriority;
                    
                    // Regla 1: Archivos pequeños (<10MB) → High
                    if (task.Size < 10 * 1024 * 1024)
                    {
                        newPriority = QueuePriority.High;
                    }
                    
                    // Regla 2: Usuarios privilegiados → High
                    if (privilegedUsersManager.IsPrivileged(task.Username?.ToString() ?? ""))
                    {
                        newPriority = QueuePriority.High;
                    }
                    
                    // Regla 3: Archivos .epub → Critical
                    string filename = task.FileName?.ToString() ?? "";
                    if (filename.EndsWith(".epub", StringComparison.OrdinalIgnoreCase))
                    {
                        newPriority = QueuePriority.Critical;
                    }
                    
                    // Regla 4: Archivos .mobi → Critical
                    if (filename.EndsWith(".mobi", StringComparison.OrdinalIgnoreCase))
                    {
                        newPriority = QueuePriority.Critical;
                    }
                    
                    // Regla 5: Archivos muy grandes (>500MB) → Low
                    if (task.Size > 500 * 1024 * 1024)
                    {
                        newPriority = QueuePriority.Low;
                    }
                    
                    // Aplicar nueva prioridad si cambió
                    if (newPriority != currentPriority)
                    {
                        queueManagement.SetPriority(task, newPriority);
                        prioritized++;
                    }
                }
                
                if (prioritized > 0)
                {
                    saveQueue();
                    logAction?.Invoke($"🤖 Auto-priorización: {prioritized} archivos repriorizados");
                }
            }
            catch (Exception ex)
            {
                logAction?.Invoke($"❌ Error en auto-priorización: {ex.Message}");
            }
        }
        
        // ═══════════════════════════════════════════════════════════════
        // AUTO-BROWSE PROGRAMADO
        // ═══════════════════════════════════════════════════════════════
        
        public void EnableScheduledAutoBrowse(int intervalHours = 6)
        {
            autoBrowseTimer = new Timer(intervalHours * 60 * 60 * 1000);
            autoBrowseTimer.Elapsed += async (s, e) => await ScheduledAutoBrowse();
            autoBrowseTimer.Start();
            
            logAction?.Invoke($"🤖 Auto-browse programado habilitado (cada {intervalHours}h)");
        }
        
        public void DisableScheduledAutoBrowse()
        {
            autoBrowseTimer?.Stop();
            autoBrowseTimer?.Dispose();
            logAction?.Invoke("🤖 Auto-browse programado deshabilitado");
        }
        
        public async Task ScheduledAutoBrowse()
        {
            try
            {
                var buddies = getBuddies();
                if (buddies == null || buddies.Count == 0) return;
                
                int browsed = 0;
                
                foreach (var buddy in buddies)
                {
                    if (buddyAutoBrowse.IsAutoBrowseEnabled(buddy))
                    {
                        await buddyAutoBrowse.OnBuddyOnline(buddy);
                        browsed++;
                        
                        // Delay entre browses para no saturar
                        await Task.Delay(5000);
                    }
                }
                
                logAction?.Invoke($"🤖 Auto-browse programado: {browsed} buddies procesados");
            }
            catch (Exception ex)
            {
                logAction?.Invoke($"❌ Error en auto-browse programado: {ex.Message}");
            }
        }
        
        // ═══════════════════════════════════════════════════════════════
        // AUTO-LIMPIEZA DE CACHÉS
        // ═══════════════════════════════════════════════════════════════
        
        public void AutoCleanupCaches()
        {
            try
            {
                buddyAutoBrowse.ClearOldCaches();
                logAction?.Invoke("🧹 Auto-limpieza de cachés completada");
            }
            catch (Exception ex)
            {
                logAction?.Invoke($"❌ Error en auto-limpieza: {ex.Message}");
            }
        }
        
        // ═══════════════════════════════════════════════════════════════
        // AUTO-ORDENAMIENTO DE COLA
        // ═══════════════════════════════════════════════════════════════
        
        public void AutoSortQueue()
        {
            try
            {
                var queue = getDownloadQueue();
                if (queue == null || queue.Count == 0) return;
                
                var sorted = queueManagement.SortByPriority(queue);
                queue.Clear();
                queue.AddRange(sorted);
                
                saveQueue();
                logAction?.Invoke("🤖 Cola ordenada automáticamente por prioridad");
            }
            catch (Exception ex)
            {
                logAction?.Invoke($"❌ Error ordenando cola: {ex.Message}");
            }
        }
        
        // ═══════════════════════════════════════════════════════════════
        // ESTADÍSTICAS DE AUTOMATIZACIÓN
        // ═══════════════════════════════════════════════════════════════
        
        public Dictionary<string, object> GetAutomationStats()
        {
            return new Dictionary<string, object>
            {
                { "AutoPrioritizeEnabled", autoPrioritizeTimer?.Enabled ?? false },
                { "AutoBrowseEnabled", autoBrowseTimer?.Enabled ?? false },
                { "QueueSize", getDownloadQueue()?.Count ?? 0 },
                { "BuddiesCount", getBuddies()?.Count ?? 0 }
            };
        }
    }
}
