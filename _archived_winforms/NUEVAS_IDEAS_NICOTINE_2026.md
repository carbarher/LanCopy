# 💡 Nuevas Ideas de Nicotine+ para SlskDown

**Fecha**: 4 de enero de 2026  
**Basado en**: Nicotine+ v3.3.10  
**Estado**: Ideas adicionales no implementadas aún

---

## 🎯 Resumen Ejecutivo

Tras completar la integración de los componentes principales de Nicotine+ (Connection Pool, Event Bus, Statistics, etc.), he identificado **15+ ideas adicionales** del código de Nicotine+ que podrían mejorar aún más SlskDown.

---

## 🚀 Ideas de Alto Impacto

### **1. Sistema de Wishlist Inteligente** ⭐⭐⭐⭐⭐

**Qué hace Nicotine+**:
```python
# pynicotine/search.py
class WishList:
    def __init__(self):
        self.searches = {}  # {search_id: WishListSearch}
        self.interval = 60  # Buscar cada 60 minutos
        
    def add_wish(self, term):
        search = WishListSearch(term)
        search.auto_download = True  # Descargar automáticamente
        search.notify = True  # Notificar nuevos resultados
        self.searches[term] = search
        
    def check_results(self, results):
        for result in results:
            if self.is_new_result(result):
                if search.auto_download:
                    self.download_file(result)
                if search.notify:
                    self.notify_user(result)
```

**Beneficios**:
- Búsquedas automáticas periódicas
- Descarga automática de nuevos resultados
- Notificaciones de nuevos archivos
- Filtros personalizables por wishlist

**Implementación en SlskDown**:
```csharp
public class IntelligentWishlist
{
    private readonly Dictionary<string, WishlistItem> items;
    private readonly Timer searchTimer;
    
    public class WishlistItem
    {
        public string SearchTerm { get; set; }
        public bool AutoDownload { get; set; }
        public bool NotifyNewResults { get; set; }
        public TimeSpan SearchInterval { get; set; }
        public HashSet<string> SeenResults { get; set; }
        public List<SearchFilter> Filters { get; set; }
    }
    
    public async Task CheckWishlistAsync()
    {
        foreach (var item in items.Values)
        {
            var results = await SearchAsync(item.SearchTerm);
            var newResults = results.Where(r => !item.SeenResults.Contains(r.Id));
            
            foreach (var result in newResults)
            {
                if (item.AutoDownload && PassesFilters(result, item.Filters))
                {
                    await DownloadAsync(result);
                }
                
                if (item.NotifyNewResults)
                {
                    NotifyUser($"Nuevo resultado para '{item.SearchTerm}': {result.FileName}");
                }
                
                item.SeenResults.Add(result.Id);
            }
        }
    }
}
```

**Impacto**: ⭐⭐⭐⭐⭐ (Automatización completa de búsquedas recurrentes)

---

### **2. Sistema de Priorización Dinámica de Descargas** ⭐⭐⭐⭐⭐

**Qué hace Nicotine+**:
```python
# pynicotine/downloads.py
def _calc_download_queue_priority(self, transfer):
    """Calcula prioridad dinámica basada en múltiples factores"""
    priority = 0
    
    # Factor 1: Prioridad manual del usuario
    priority += transfer.user_priority * 1000
    
    # Factor 2: Velocidad histórica del proveedor
    user_speed = self.get_user_average_speed(transfer.user)
    priority += user_speed * 10
    
    # Factor 3: Tamaño del archivo (pequeños primero)
    if transfer.size < 10 * 1024 * 1024:  # < 10MB
        priority += 500
    
    # Factor 4: Tiempo en cola (FIFO con peso)
    queue_time = time.time() - transfer.queued_at
    priority += queue_time / 60  # +1 por minuto
    
    # Factor 5: Tasa de éxito del proveedor
    success_rate = self.get_user_success_rate(transfer.user)
    priority += success_rate * 100
    
    return priority
```

**Beneficios**:
- Descargas más inteligentes y eficientes
- Aprovecha mejor los proveedores rápidos
- Completa archivos pequeños primero
- Balance entre FIFO y eficiencia

**Implementación en SlskDown**:
```csharp
public class DynamicDownloadPrioritizer
{
    public double CalculatePriority(DownloadTask task)
    {
        double priority = 0;
        
        // Factor 1: Prioridad manual
        priority += (int)task.Priority * 1000;
        
        // Factor 2: Velocidad histórica
        var userStats = transferStats.GetUserStats(task.File.Username);
        priority += userStats.AverageSpeed * 10;
        
        // Factor 3: Tamaño (pequeños primero)
        if (task.File.SizeBytes < 10 * 1024 * 1024)
            priority += 500;
        
        // Factor 4: Tiempo en cola
        var queueTime = DateTime.UtcNow - task.QueuedAt;
        priority += queueTime.TotalMinutes;
        
        // Factor 5: Tasa de éxito
        priority += userStats.SuccessRate * 100;
        
        // Factor 6: Disponibilidad del proveedor
        if (IsUserOnline(task.File.Username))
            priority += 200;
        
        return priority;
    }
    
    public void ReorderQueue()
    {
        lock (downloadQueueLock)
        {
            downloadQueue.Sort((a, b) => 
                CalculatePriority(b).CompareTo(CalculatePriority(a)));
        }
    }
}
```

**Impacto**: ⭐⭐⭐⭐⭐ (Mejora drástica en eficiencia de descargas)

---

### **3. Sistema de Banned Users y Auto-Ban** ⭐⭐⭐⭐

**Qué hace Nicotine+**:
```python
# pynicotine/users.py
class UserManager:
    def __init__(self):
        self.banned_users = set()
        self.auto_ban_config = {
            'max_failures': 5,
            'time_window': 3600,  # 1 hora
            'ban_duration': 86400  # 24 horas
        }
        self.user_failures = {}  # {username: [(timestamp, reason)]}
    
    def record_failure(self, username, reason):
        if username not in self.user_failures:
            self.user_failures[username] = []
        
        self.user_failures[username].append((time.time(), reason))
        
        # Limpiar fallos antiguos
        cutoff = time.time() - self.auto_ban_config['time_window']
        self.user_failures[username] = [
            (ts, r) for ts, r in self.user_failures[username] 
            if ts > cutoff
        ]
        
        # Auto-ban si excede umbral
        if len(self.user_failures[username]) >= self.auto_ban_config['max_failures']:
            self.ban_user(username, self.auto_ban_config['ban_duration'])
            log.info(f"Auto-banned {username} for {len(self.user_failures[username])} failures")
```

**Beneficios**:
- Evita perder tiempo con usuarios problemáticos
- Auto-ban temporal basado en fallos
- Libera recursos para usuarios confiables

**Implementación en SlskDown**:
```csharp
public class UserBanManager
{
    private readonly HashSet<string> bannedUsers;
    private readonly Dictionary<string, List<(DateTime, string)>> userFailures;
    
    public class BanConfig
    {
        public int MaxFailures { get; set; } = 5;
        public TimeSpan TimeWindow { get; set; } = TimeSpan.FromHours(1);
        public TimeSpan BanDuration { get; set; } = TimeSpan.FromHours(24);
    }
    
    public void RecordFailure(string username, string reason)
    {
        if (!userFailures.ContainsKey(username))
            userFailures[username] = new List<(DateTime, string)>();
        
        userFailures[username].Add((DateTime.UtcNow, reason));
        
        // Limpiar fallos antiguos
        var cutoff = DateTime.UtcNow - config.TimeWindow;
        userFailures[username] = userFailures[username]
            .Where(f => f.Item1 > cutoff)
            .ToList();
        
        // Auto-ban si excede umbral
        if (userFailures[username].Count >= config.MaxFailures)
        {
            BanUser(username, config.BanDuration);
            Log($"🚫 Auto-banned {username} por {userFailures[username].Count} fallos");
        }
    }
    
    public bool IsUserBanned(string username)
    {
        return bannedUsers.Contains(username);
    }
}
```

**Impacto**: ⭐⭐⭐⭐ (Mejora eficiencia evitando usuarios problemáticos)

---

### **4. Sistema de Upload Slots Dinámicos** ⭐⭐⭐⭐

**Qué hace Nicotine+**:
```python
# pynicotine/uploads.py
class UploadManager:
    def __init__(self):
        self.base_slots = 2
        self.privileged_slots = 1
        self.friend_slots = 1
        
    def get_available_slots(self, username):
        """Slots dinámicos según tipo de usuario"""
        if self.is_friend(username):
            return self.base_slots + self.friend_slots
        elif self.is_privileged(username):
            return self.base_slots + self.privileged_slots
        else:
            return self.base_slots
    
    def adjust_slots_by_bandwidth(self):
        """Ajusta slots según ancho de banda disponible"""
        total_upload_speed = sum(u.speed for u in self.active_uploads)
        max_bandwidth = self.config.max_upload_speed
        
        if total_upload_speed < max_bandwidth * 0.7:
            # Tenemos ancho de banda disponible, aumentar slots
            self.base_slots = min(self.base_slots + 1, 10)
        elif total_upload_speed > max_bandwidth * 0.95:
            # Saturado, reducir slots
            self.base_slots = max(self.base_slots - 1, 1)
```

**Beneficios**:
- Mejor utilización del ancho de banda
- Prioriza amigos y usuarios privilegiados
- Ajuste automático según carga

**Impacto**: ⭐⭐⭐⭐ (Importante para sharing mode)

---

### **5. Sistema de Retry Inteligente con Backoff Exponencial** ⭐⭐⭐⭐

**Qué hace Nicotine+**:
```python
# pynicotine/downloads.py
def _calculate_retry_delay(self, transfer):
    """Backoff exponencial con jitter"""
    base_delay = 60  # 1 minuto
    max_delay = 3600  # 1 hora
    
    # Exponencial: 1min, 2min, 4min, 8min, 16min, 32min, 60min
    delay = min(base_delay * (2 ** transfer.retry_count), max_delay)
    
    # Agregar jitter aleatorio ±20%
    jitter = random.uniform(0.8, 1.2)
    delay *= jitter
    
    # Ajustar según razón del fallo
    if transfer.last_error == "User offline":
        delay *= 2  # Esperar más si usuario offline
    elif transfer.last_error == "Queue full":
        delay *= 1.5  # Esperar más si cola llena
    elif transfer.last_error == "Connection timeout":
        delay *= 0.5  # Reintentar más rápido si timeout
    
    return delay
```

**Beneficios**:
- Evita saturar el servidor con reintentos
- Ajusta delay según tipo de error
- Jitter previene thundering herd

**Implementación en SlskDown**:
```csharp
public class IntelligentRetryStrategy
{
    public TimeSpan CalculateRetryDelay(DownloadTask task)
    {
        var baseDelay = TimeSpan.FromMinutes(1);
        var maxDelay = TimeSpan.FromHours(1);
        
        // Backoff exponencial
        var delay = TimeSpan.FromSeconds(
            Math.Min(baseDelay.TotalSeconds * Math.Pow(2, task.RetryCount), 
                    maxDelay.TotalSeconds));
        
        // Jitter ±20%
        var jitter = Random.Shared.NextDouble() * 0.4 + 0.8;
        delay = TimeSpan.FromSeconds(delay.TotalSeconds * jitter);
        
        // Ajustar según error
        delay = task.LastFailureReason switch
        {
            DownloadFailureReason.Connection => delay * 0.5,
            DownloadFailureReason.QueueFull => delay * 1.5,
            DownloadFailureReason.Timeout => delay * 0.7,
            _ => delay
        };
        
        return delay;
    }
}
```

**Impacto**: ⭐⭐⭐⭐ (Mejora tasa de éxito de reintentos)

---

## 💡 Ideas de Impacto Medio

### **6. Sistema de Partial File Resume** ⭐⭐⭐

**Qué hace**: Reanudar descargas parciales desde donde se quedaron

```csharp
public class PartialFileManager
{
    public async Task<long> GetResumePosition(string filePath)
    {
        var partialPath = filePath + ".partial";
        if (File.Exists(partialPath))
        {
            var info = new FileInfo(partialPath);
            return info.Length;
        }
        return 0;
    }
    
    public async Task ResumeDownload(DownloadTask task)
    {
        var resumePosition = await GetResumePosition(task.LocalPath);
        if (resumePosition > 0)
        {
            task.BytesDownloaded = resumePosition;
            Log($"📥 Reanudando desde {resumePosition:N0} bytes");
        }
    }
}
```

**Impacto**: ⭐⭐⭐ (Ahorra tiempo y ancho de banda)

---

### **7. Sistema de User Notes y Ratings** ⭐⭐⭐

**Qué hace**: Permite al usuario agregar notas y ratings a otros usuarios

```csharp
public class UserNotesManager
{
    public class UserNote
    {
        public string Username { get; set; }
        public string Note { get; set; }
        public int Rating { get; set; }  // 1-5 estrellas
        public DateTime CreatedAt { get; set; }
        public List<string> Tags { get; set; }  // "fast", "reliable", "slow"
    }
    
    public void AddNote(string username, string note, int rating)
    {
        var userNote = new UserNote
        {
            Username = username,
            Note = note,
            Rating = rating,
            CreatedAt = DateTime.UtcNow
        };
        SaveNote(userNote);
    }
}
```

**Impacto**: ⭐⭐⭐ (Ayuda a recordar experiencias con usuarios)

---

### **8. Sistema de Search History con Sugerencias** ⭐⭐⭐

**Qué hace**: Historial de búsquedas con autocompletado inteligente

```csharp
public class SearchHistoryManager
{
    private readonly List<SearchHistoryItem> history;
    
    public List<string> GetSuggestions(string partialQuery)
    {
        return history
            .Where(h => h.Query.StartsWith(partialQuery, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(h => h.Frequency)
            .ThenByDescending(h => h.LastUsed)
            .Select(h => h.Query)
            .Take(10)
            .ToList();
    }
    
    public void RecordSearch(string query, int resultsCount)
    {
        var item = history.FirstOrDefault(h => h.Query == query);
        if (item != null)
        {
            item.Frequency++;
            item.LastUsed = DateTime.UtcNow;
        }
        else
        {
            history.Add(new SearchHistoryItem
            {
                Query = query,
                Frequency = 1,
                LastUsed = DateTime.UtcNow,
                AverageResults = resultsCount
            });
        }
    }
}
```

**Impacto**: ⭐⭐⭐ (Mejora UX de búsquedas)

---

### **9. Sistema de Download Verification (Checksums)** ⭐⭐⭐

**Qué hace**: Verifica integridad de archivos descargados

```csharp
public class DownloadVerifier
{
    public async Task<bool> VerifyDownload(DownloadTask task)
    {
        if (string.IsNullOrEmpty(task.ExpectedChecksum))
            return true;  // No hay checksum para verificar
        
        var actualChecksum = await CalculateChecksumAsync(task.LocalPath);
        
        if (actualChecksum != task.ExpectedChecksum)
        {
            Log($"❌ Checksum mismatch: {task.File.FileName}");
            task.Status = DownloadStatus.Corrupted;
            return false;
        }
        
        return true;
    }
    
    private async Task<string> CalculateChecksumAsync(string filePath)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hash = await sha256.ComputeHashAsync(stream);
        return BitConverter.ToString(hash).Replace("-", "");
    }
}
```

**Impacto**: ⭐⭐⭐ (Garantiza integridad de archivos)

---

### **10. Sistema de Bandwidth Scheduler** ⭐⭐⭐

**Qué hace**: Programa límites de ancho de banda por horario

```csharp
public class BandwidthScheduler
{
    public class Schedule
    {
        public DayOfWeek Day { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public int MaxDownloadKBps { get; set; }
        public int MaxUploadKBps { get; set; }
    }
    
    public int GetCurrentDownloadLimit()
    {
        var now = DateTime.Now;
        var schedule = schedules.FirstOrDefault(s => 
            s.Day == now.DayOfWeek &&
            now.TimeOfDay >= s.StartTime &&
            now.TimeOfDay <= s.EndTime);
        
        return schedule?.MaxDownloadKBps ?? defaultLimit;
    }
}
```

**Impacto**: ⭐⭐⭐ (Control fino de ancho de banda)

---

## 🔧 Ideas de Mejora Técnica

### **11. Connection Keep-Alive con Heartbeat** ⭐⭐⭐

```csharp
public class ConnectionKeepAlive
{
    private readonly Timer heartbeatTimer;
    
    public void StartHeartbeat(Connection connection)
    {
        heartbeatTimer = new Timer(async _ =>
        {
            if (DateTime.UtcNow - connection.LastActivity > TimeSpan.FromMinutes(2))
            {
                await SendPingAsync(connection);
            }
        }, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
    }
}
```

**Impacto**: ⭐⭐⭐ (Mantiene conexiones vivas)

---

### **12. Adaptive Chunk Size** ⭐⭐⭐

```csharp
public class AdaptiveChunkSizer
{
    public int CalculateOptimalChunkSize(double currentSpeed)
    {
        // Chunks más grandes para velocidades altas
        if (currentSpeed > 5 * 1024 * 1024)  // > 5 MB/s
            return 256 * 1024;  // 256 KB
        else if (currentSpeed > 1 * 1024 * 1024)  // > 1 MB/s
            return 128 * 1024;  // 128 KB
        else
            return 64 * 1024;   // 64 KB
    }
}
```

**Impacto**: ⭐⭐⭐ (Optimiza throughput)

---

### **13. Download Queue Persistence con Compression** ⭐⭐

```csharp
public async Task SaveQueueCompressedAsync()
{
    var json = JsonSerializer.Serialize(downloadQueue);
    var bytes = Encoding.UTF8.GetBytes(json);
    
    using var output = File.Create(queuePath + ".gz");
    using var gzip = new GZipStream(output, CompressionLevel.Optimal);
    await gzip.WriteAsync(bytes);
    
    Log($"💾 Cola guardada: {bytes.Length:N0} → {output.Length:N0} bytes");
}
```

**Impacto**: ⭐⭐ (Reduce tamaño de archivos de estado)

---

### **14. User Avatar Cache** ⭐⭐

```csharp
public class UserAvatarCache
{
    private readonly Dictionary<string, byte[]> cache;
    
    public async Task<byte[]> GetAvatarAsync(string username)
    {
        if (cache.TryGetValue(username, out var avatar))
            return avatar;
        
        avatar = await DownloadAvatarAsync(username);
        cache[username] = avatar;
        return avatar;
    }
}
```

**Impacto**: ⭐⭐ (Mejora UX visual)

---

### **15. Search Result Deduplication** ⭐⭐

```csharp
public class SearchDeduplicator
{
    public List<SearchResult> Deduplicate(List<SearchResult> results)
    {
        return results
            .GroupBy(r => new { r.FileName, r.SizeBytes })
            .Select(g => g.OrderByDescending(r => r.UserSpeed).First())
            .ToList();
    }
}
```

**Impacto**: ⭐⭐ (Limpia resultados duplicados)

---

## 📊 Resumen de Prioridades

### **Implementar Ahora** (Alto Impacto, Esfuerzo Medio)
1. ✅ Wishlist Inteligente
2. ✅ Priorización Dinámica
3. ✅ Banned Users Auto-Ban
4. ✅ Retry Inteligente

### **Implementar Pronto** (Impacto Medio, Esfuerzo Bajo)
5. ✅ Partial File Resume
6. ✅ User Notes
7. ✅ Search History
8. ✅ Download Verification

### **Implementar Después** (Mejoras Técnicas)
9. ✅ Connection Keep-Alive
10. ✅ Adaptive Chunk Size
11. ✅ Queue Compression

---

## 🎯 Conclusión

Nicotine+ tiene **20+ años de desarrollo** y estas ideas representan aprendizajes valiosos de la comunidad Soulseek. Implementar estas mejoras haría de SlskDown un cliente **más robusto, eficiente e inteligente**.

**Próximo paso recomendado**: Implementar el **Wishlist Inteligente** ya que automatizaría completamente las búsquedas recurrentes del usuario.

---

**Fecha**: 4 de enero de 2026  
**Versión**: SlskDown v2.0 - Nicotine+ Enhanced Edition  
**Estado**: 📋 Ideas documentadas, listas para implementación
