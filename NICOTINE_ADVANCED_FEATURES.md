# 🚀 Características Avanzadas de Nicotine+ para SlskDown

## Análisis de Características Adicionales No Implementadas

Basado en el análisis de issues y discusiones de Nicotine+, este documento detalla características avanzadas adicionales que podemos implementar en SlskDown.

---

## 🔔 1. SISTEMA DE WISHLIST MEJORADO

### 1.1 Notificaciones Visuales y Sonoras
**Estado en Nicotine+**: Issue #2221, #2551, #2840 - Solicitado por la comunidad

**Problema identificado**:
- Nicotine+ abre silenciosamente una pestaña cuando encuentra resultados de wishlist
- SoulseekQt muestra icono parpadeante en taskbar
- Fácil perder resultados importantes si la ventana está minimizada

**Solución propuesta para SlskDown**:
```csharp
// Sistema de notificaciones para wishlist
public class WishlistNotificationSystem
{
    private bool enableWishlistNotifications = true;
    private bool enableWishlistSound = true;
    private bool flashTaskbarIcon = true;
    private string wishlistSoundPath = "wishlist_alert.wav";
    
    public void OnWishlistResultFound(string searchTerm, List<SearchResult> results)
    {
        if (!enableWishlistNotifications) return;
        
        // 1. Notificación de Windows
        if (notifyIcon != null)
        {
            notifyIcon.ShowBalloonTip(
                5000,
                "🎯 Wishlist Match!",
                $"Found {results.Count} results for '{searchTerm}'",
                ToolTipIcon.Info
            );
        }
        
        // 2. Sonido de alerta
        if (enableWishlistSound && File.Exists(wishlistSoundPath))
        {
            var player = new System.Media.SoundPlayer(wishlistSoundPath);
            player.Play();
        }
        
        // 3. Parpadeo en taskbar (Windows)
        if (flashTaskbarIcon)
        {
            FlashWindow(this.Handle, true);
        }
        
        // 4. Highlight en la pestaña
        HighlightWishlistTab(searchTerm);
        
        // 5. Log con timestamp
        Log($"🎯 WISHLIST MATCH: '{searchTerm}' - {results.Count} resultados a las {DateTime.Now:HH:mm:ss}");
    }
    
    [DllImport("user32.dll")]
    private static extern bool FlashWindow(IntPtr hwnd, bool bInvert);
    
    private void HighlightWishlistTab(string searchTerm)
    {
        // Cambiar color de la pestaña a naranja brillante
        var tab = FindWishlistTab(searchTerm);
        if (tab != null)
        {
            tab.BackColor = Color.Orange;
            tab.Font = new Font(tab.Font, FontStyle.Bold);
        }
    }
}
```

**Configuración recomendada**:
- ✅ Notificación de Windows activada por defecto
- ✅ Sonido de alerta configurable
- ✅ Parpadeo de taskbar activado por defecto
- ✅ Highlight de pestaña con color naranja

### 1.2 Filtro "Discard Previous Results"
**Estado en Nicotine+**: Discussion #1604 - Feature solicitada

**Problema identificado**:
- Wishlist muestra los mismos resultados repetidamente
- Usuario ya descartó esos resultados como irrelevantes
- SoulseekQt tiene feature "Discard Previous Results"

**Solución propuesta para SlskDown**:
```csharp
// Sistema de descarte de resultados previos
public class WishlistResultFilter
{
    private Dictionary<string, HashSet<string>> discardedResults = new Dictionary<string, HashSet<string>>();
    private bool enableDiscardPrevious = true;
    
    public List<SearchResult> FilterNewResults(string searchTerm, List<SearchResult> newResults)
    {
        if (!enableDiscardPrevious) return newResults;
        
        if (!discardedResults.ContainsKey(searchTerm))
        {
            discardedResults[searchTerm] = new HashSet<string>();
        }
        
        var filtered = new List<SearchResult>();
        
        foreach (var result in newResults)
        {
            string resultKey = $"{result.Username}|{result.Filename}|{result.FileSize}";
            
            if (!discardedResults[searchTerm].Contains(resultKey))
            {
                filtered.Add(result);
            }
        }
        
        Log($"🔍 Wishlist '{searchTerm}': {filtered.Count} nuevos de {newResults.Count} totales");
        return filtered;
    }
    
    public void DiscardResult(string searchTerm, SearchResult result)
    {
        if (!discardedResults.ContainsKey(searchTerm))
        {
            discardedResults[searchTerm] = new HashSet<string>();
        }
        
        string resultKey = $"{result.Username}|{result.Filename}|{result.FileSize}";
        discardedResults[searchTerm].Add(resultKey);
        
        SaveDiscardedResults();
    }
    
    public void ClearDiscardedResults(string searchTerm)
    {
        if (discardedResults.ContainsKey(searchTerm))
        {
            discardedResults[searchTerm].Clear();
            Log($"🗑️ Resultados descartados limpiados para '{searchTerm}'");
        }
    }
}
```

**Configuración recomendada**:
- ✅ Activado por defecto
- ✅ Botón "Discard" en menú contextual de resultados
- ✅ Botón "Clear Discarded" para resetear
- ✅ Persistencia en archivo JSON

### 1.3 Wishlist con Filtros Guardados
**Estado en Nicotine+**: Issue #1887 - Feature solicitada

**Problema identificado**:
- Wishlist solo guarda el texto de búsqueda
- No guarda filtros (tamaño, extensión, bitrate, etc.)
- Usuario debe reconfigurar filtros cada vez

**Solución propuesta para SlskDown**:
```csharp
// Wishlist con configuración completa de filtros
public class WishlistItem
{
    public string SearchTerm { get; set; }
    public bool Enabled { get; set; }
    
    // Filtros guardados
    public long MinSizeBytes { get; set; }
    public long MaxSizeBytes { get; set; }
    public string Extension { get; set; }
    public int MinBitrate { get; set; }
    public int MinDuration { get; set; }
    public int MaxDuration { get; set; }
    public string FileType { get; set; } // Audio, Video, Imagen, etc.
    public bool OnlyFreeSlots { get; set; }
    
    // Configuración de notificaciones
    public bool EnableNotifications { get; set; }
    public bool EnableSound { get; set; }
    public bool AutoDownload { get; set; } // Descargar automáticamente
    
    // Estadísticas
    public int TotalResultsFound { get; set; }
    public int TotalDownloaded { get; set; }
    public DateTime LastMatch { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class WishlistManager
{
    private List<WishlistItem> wishlistItems = new List<WishlistItem>();
    
    public void AddWishlistItem(WishlistItem item)
    {
        wishlistItems.Add(item);
        SaveWishlist();
        Log($"✅ Wishlist agregado: '{item.SearchTerm}' con filtros personalizados");
    }
    
    public async Task ExecuteWishlistSearches()
    {
        foreach (var item in wishlistItems.Where(i => i.Enabled))
        {
            var results = await SearchWithFilters(item);
            
            if (results.Count > 0)
            {
                item.TotalResultsFound += results.Count;
                item.LastMatch = DateTime.Now;
                
                if (item.AutoDownload)
                {
                    await AutoDownloadWishlistResults(results);
                }
                
                OnWishlistResultFound(item.SearchTerm, results);
            }
        }
    }
}
```

**Configuración recomendada**:
- ✅ Guardar filtros completos con cada wishlist
- ✅ Auto-descarga opcional
- ✅ Estadísticas por wishlist
- ✅ Exportar/importar wishlist

---

## 🚫 2. SISTEMA DE FILTRADO AVANZADO

### 2.1 Banned Phrases (Frases Prohibidas)
**Estado en Nicotine+**: Issue #2854 - Server code 160

**Problema identificado**:
- Servidor envía lista de términos prohibidos (código 160)
- Cliente debe bloquear resultados con esos términos
- Protección contra contenido ilegal

**Solución propuesta para SlskDown**:
```csharp
// Sistema de frases prohibidas
public class BannedPhrasesFilter
{
    private HashSet<string> bannedPhrases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private HashSet<string> userBannedPhrases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    
    public void LoadServerBannedPhrases(List<string> phrases)
    {
        bannedPhrases.Clear();
        foreach (var phrase in phrases)
        {
            bannedPhrases.Add(phrase.ToLower());
        }
        Log($"🚫 Cargadas {bannedPhrases.Count} frases prohibidas del servidor");
    }
    
    public void AddUserBannedPhrase(string phrase)
    {
        userBannedPhrases.Add(phrase.ToLower());
        SaveUserBannedPhrases();
        Log($"🚫 Frase prohibida agregada: '{phrase}'");
    }
    
    public bool IsResultBanned(SearchResult result)
    {
        string filename = result.Filename.ToLower();
        
        // Verificar frases del servidor
        foreach (var phrase in bannedPhrases)
        {
            if (filename.Contains(phrase))
            {
                return true;
            }
        }
        
        // Verificar frases del usuario
        foreach (var phrase in userBannedPhrases)
        {
            if (filename.Contains(phrase))
            {
                return true;
            }
        }
        
        return false;
    }
    
    public List<SearchResult> FilterResults(List<SearchResult> results)
    {
        var filtered = results.Where(r => !IsResultBanned(r)).ToList();
        
        int blocked = results.Count - filtered.Count;
        if (blocked > 0)
        {
            Log($"🚫 Bloqueados {blocked} resultados por frases prohibidas");
        }
        
        return filtered;
    }
}
```

**Configuración recomendada**:
- ✅ Respeta lista del servidor (código 160)
- ✅ Lista personalizada del usuario
- ✅ Filtrado automático en búsquedas
- ✅ UI para gestionar frases prohibidas

### 2.2 Filtro por País/IP
**Estado en Nicotine+**: Issue #1762 - Ban/Block IP

**Solución propuesta para SlskDown**:
```csharp
// Sistema de filtrado por país/IP
public class GeoIPFilter
{
    private HashSet<string> bannedCountries = new HashSet<string>();
    private HashSet<string> bannedIPRanges = new HashSet<string>();
    private bool enableGeoFiltering = false;
    
    public void BanCountry(string countryCode)
    {
        bannedCountries.Add(countryCode.ToUpper());
        SaveConfig();
        Log($"🌍 País bloqueado: {countryCode}");
    }
    
    public void BanIPRange(string ipRange)
    {
        bannedIPRanges.Add(ipRange);
        SaveConfig();
        Log($"🔒 Rango IP bloqueado: {ipRange}");
    }
    
    public bool IsUserBanned(string username, string ipAddress)
    {
        if (!enableGeoFiltering) return false;
        
        // Verificar país (requiere API de GeoIP)
        string country = GetCountryFromIP(ipAddress);
        if (bannedCountries.Contains(country))
        {
            return true;
        }
        
        // Verificar rango IP
        foreach (var range in bannedIPRanges)
        {
            if (IsIPInRange(ipAddress, range))
            {
                return true;
            }
        }
        
        return false;
    }
}
```

---

## ⚡ 3. GESTIÓN DE ANCHO DE BANDA

### 3.1 Límites de Velocidad Dinámicos
**Estado en Nicotine+**: Issue #1987, #3033

**Problema identificado**:
- Límites de velocidad estáticos no aprovechan ancho de banda disponible
- Usuario con puerto bloqueado no usa todo su slot
- Necesidad de toggles rápidos para límites

**Solución propuesta para SlskDown**:
```csharp
// Sistema de límites de velocidad dinámicos
public class BandwidthManager
{
    private int globalUploadLimit = 0; // 0 = sin límite
    private int globalDownloadLimit = 0;
    private int alternativeUploadLimit = 0;
    private int alternativeDownloadLimit = 0;
    private bool useAlternativeLimits = false;
    
    // Modo basado en ancho de banda (no en slots)
    private bool useBandwidthMode = true;
    private int bandwidthThresholdKBps = 50; // Si usuario usa <50KB/s, liberar slot
    
    public void UpdateTransferSpeeds()
    {
        if (!useBandwidthMode) return;
        
        var activeUploads = uploads.Where(u => u.Status == "Uploading").ToList();
        
        foreach (var upload in activeUploads)
        {
            // Si usuario usa poco ancho de banda, permitir otro upload
            if (upload.CurrentSpeed < bandwidthThresholdKBps * 1024)
            {
                Log($"⚡ Usuario {upload.Username} usa poco ancho de banda ({FormatSpeed(upload.CurrentSpeed)}), liberando slot");
                // Permitir siguiente upload en cola
                ProcessUploadQueue();
            }
        }
    }
    
    public void ToggleAlternativeLimits()
    {
        useAlternativeLimits = !useAlternativeLimits;
        
        if (useAlternativeLimits)
        {
            ApplySpeedLimits(alternativeUploadLimit, alternativeDownloadLimit);
            Log($"🔄 Límites alternativos activados: ↑{alternativeUploadLimit}KB/s ↓{alternativeDownloadLimit}KB/s");
        }
        else
        {
            ApplySpeedLimits(globalUploadLimit, globalDownloadLimit);
            Log($"🔄 Límites globales activados: ↑{globalUploadLimit}KB/s ↓{globalDownloadLimit}KB/s");
        }
    }
    
    // Botones en status bar
    public void CreateBandwidthToggles(StatusStrip statusBar)
    {
        var btnToggleUpload = new ToolStripButton("↑ Limit");
        btnToggleUpload.Click += (s, e) => ToggleUploadLimit();
        statusBar.Items.Add(btnToggleUpload);
        
        var btnToggleDownload = new ToolStripButton("↓ Limit");
        btnToggleDownload.Click += (s, e) => ToggleDownloadLimit();
        statusBar.Items.Add(btnToggleDownload);
        
        var btnToggleAlt = new ToolStripButton("⚡ Alt");
        btnToggleAlt.Click += (s, e) => ToggleAlternativeLimits();
        statusBar.Items.Add(btnToggleAlt);
    }
}
```

**Configuración recomendada**:
- ✅ Modo basado en ancho de banda (no slots fijos)
- ✅ Límites alternativos con toggle rápido
- ✅ Botones en status bar
- ✅ Threshold configurable (50 KB/s por defecto)

---

## 📊 4. ESTADÍSTICAS Y MONITOREO

### 4.1 Estadísticas de Usuario Detalladas
**Estado en Nicotine+**: Issue #941, #2082

**Solución propuesta para SlskDown**:
```csharp
// Sistema de estadísticas detalladas
public class UserStatistics
{
    public string Username { get; set; }
    
    // Estadísticas de descarga
    public int TotalDownloads { get; set; }
    public int CompletedDownloads { get; set; }
    public long TotalBytesDownloaded { get; set; }
    public double AverageDownloadSpeed { get; set; }
    public double FastestDownloadSpeed { get; set; }
    public double SlowestDownloadSpeed { get; set; }
    
    // Estadísticas de upload
    public int TotalUploads { get; set; }
    public int CompletedUploads { get; set; }
    public long TotalBytesUploaded { get; set; }
    public double AverageUploadSpeed { get; set; }
    
    // Fiabilidad
    public int ConnectionTimeouts { get; set; }
    public int ConnectionFailures { get; set; }
    public double ReliabilityScore => TotalDownloads > 0 
        ? (double)CompletedDownloads / TotalDownloads * 100 
        : 0;
    
    // Tiempos
    public DateTime FirstSeen { get; set; }
    public DateTime LastSeen { get; set; }
    public TimeSpan TotalConnectionTime { get; set; }
}

public class StatisticsManager
{
    private Dictionary<string, UserStatistics> userStats = new Dictionary<string, UserStatistics>();
    
    public void UpdateDownloadStats(string username, long bytes, double speed, bool completed)
    {
        if (!userStats.ContainsKey(username))
        {
            userStats[username] = new UserStatistics { Username = username, FirstSeen = DateTime.Now };
        }
        
        var stats = userStats[username];
        stats.TotalDownloads++;
        if (completed) stats.CompletedDownloads++;
        stats.TotalBytesDownloaded += bytes;
        stats.AverageDownloadSpeed = (stats.AverageDownloadSpeed + speed) / 2;
        stats.FastestDownloadSpeed = Math.Max(stats.FastestDownloadSpeed, speed);
        stats.LastSeen = DateTime.Now;
        
        SaveStatistics();
    }
    
    public List<UserStatistics> GetTopUsers(int count = 10)
    {
        return userStats.Values
            .OrderByDescending(s => s.TotalBytesDownloaded)
            .Take(count)
            .ToList();
    }
    
    public void ShowUserStatistics(string username)
    {
        if (!userStats.ContainsKey(username)) return;
        
        var stats = userStats[username];
        Log($"");
        Log($"📊 ESTADÍSTICAS DE {username}");
        Log($"═══════════════════════════════════════");
        Log($"Descargas: {stats.CompletedDownloads}/{stats.TotalDownloads} ({stats.ReliabilityScore:F1}%)");
        Log($"Total descargado: {FormatSize(stats.TotalBytesDownloaded)}");
        Log($"Velocidad promedio: {FormatSpeed(stats.AverageDownloadSpeed)}");
        Log($"Velocidad máxima: {FormatSpeed(stats.FastestDownloadSpeed)}");
        Log($"Uploads: {stats.CompletedUploads}/{stats.TotalUploads}");
        Log($"Total subido: {FormatSize(stats.TotalBytesUploaded)}");
        Log($"Primera vez visto: {stats.FirstSeen:yyyy-MM-dd HH:mm}");
        Log($"Última vez visto: {stats.LastSeen:yyyy-MM-dd HH:mm}");
        Log($"═══════════════════════════════════════");
    }
}
```

---

## 💬 5. SISTEMA DE CHAT Y MENSAJERÍA

### 5.1 Historial de Chats Persistente
**Estado en Nicotine+**: Issue #1509 - Feature solicitada

**Problema identificado**:
- Cerrar chat lo oculta completamente
- No hay lista de chats previos
- SoulseekQt mantiene lista de conversaciones

**Solución propuesta para SlskDown**:
```csharp
// Sistema de historial de chats
public class ChatHistoryManager
{
    private Dictionary<string, List<ChatMessage>> chatHistory = new Dictionary<string, List<ChatMessage>>();
    private HashSet<string> pinnedChats = new HashSet<string>();
    
    public class ChatMessage
    {
        public string Username { get; set; }
        public string Message { get; set; }
        public DateTime Timestamp { get; set; }
        public bool IsOutgoing { get; set; }
    }
    
    public void SaveMessage(string username, string message, bool isOutgoing)
    {
        if (!chatHistory.ContainsKey(username))
        {
            chatHistory[username] = new List<ChatMessage>();
        }
        
        chatHistory[username].Add(new ChatMessage
        {
            Username = username,
            Message = message,
            Timestamp = DateTime.Now,
            IsOutgoing = isOutgoing
        });
        
        SaveChatHistory();
    }
    
    public void CreateChatHistoryPanel()
    {
        var panel = new Panel { Dock = DockStyle.Left, Width = 200, BackColor = Color.FromArgb(35, 35, 35) };
        
        var lvChats = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            BackColor = Color.FromArgb(40, 40, 40),
            ForeColor = Color.White
        };
        
        lvChats.Columns.Add("Usuario", 150);
        lvChats.Columns.Add("Último", 50);
        
        foreach (var chat in chatHistory.OrderByDescending(c => c.Value.Last().Timestamp))
        {
            var item = new ListViewItem(chat.Key);
            var lastMsg = chat.Value.Last();
            item.SubItems.Add(GetRelativeTime(lastMsg.Timestamp));
            
            if (pinnedChats.Contains(chat.Key))
            {
                item.BackColor = Color.FromArgb(60, 60, 80);
                item.Font = new Font(item.Font, FontStyle.Bold);
            }
            
            lvChats.Items.Add(item);
        }
        
        panel.Controls.Add(lvChats);
    }
    
    private string GetRelativeTime(DateTime timestamp)
    {
        var diff = DateTime.Now - timestamp;
        if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m";
        if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h";
        return $"{(int)diff.TotalDays}d";
    }
}
```

---

## 🎯 6. PLAN DE IMPLEMENTACIÓN ADICIONAL

### Prioridad Alta
- [ ] **Wishlist con notificaciones** (sonido + taskbar flash)
- [ ] **Banned phrases filter** (código servidor 160)
- [ ] **Bandwidth mode** (basado en uso real, no slots)
- [ ] **Estadísticas de usuario** (fiabilidad, velocidades)

### Prioridad Media
- [ ] **Wishlist discard previous results**
- [ ] **Wishlist con filtros guardados**
- [ ] **Toggles de límites en status bar**
- [ ] **Chat history persistente**

### Prioridad Baja
- [ ] **Filtro por país/IP**
- [ ] **Dashboard de estadísticas avanzado**
- [ ] **Exportar/importar wishlist**

---

## 📋 RESUMEN DE MEJORAS ADICIONALES

### Ventajas sobre Nicotine+:
1. ✅ **Wishlist con notificaciones visuales y sonoras** (Nicotine+ solo abre pestaña)
2. ✅ **Discard previous results** (solicitado pero no implementado)
3. ✅ **Wishlist con filtros completos** (Nicotine+ solo guarda texto)
4. ✅ **Banned phrases automático** (código 160 del servidor)
5. ✅ **Bandwidth mode inteligente** (mejor que slots fijos)
6. ✅ **Estadísticas detalladas por usuario** (fiabilidad, velocidades)
7. ✅ **Chat history con lista persistente** (Nicotine+ oculta chats cerrados)

### Configuración Recomendada:
```json
{
  "wishlist": {
    "enableNotifications": true,
    "enableSound": true,
    "flashTaskbar": true,
    "discardPrevious": true,
    "autoDownload": false
  },
  "filters": {
    "enableBannedPhrases": true,
    "enableGeoFiltering": false,
    "bannedCountries": []
  },
  "bandwidth": {
    "useBandwidthMode": true,
    "bandwidthThreshold": 50,
    "enableAlternativeLimits": true
  },
  "statistics": {
    "trackUserStats": true,
    "showCompletedSpeeds": true
  },
  "chat": {
    "persistHistory": true,
    "maxHistoryDays": 90
  }
}
```

---

**Documento creado**: 2026-01-10
**Basado en**: Nicotine+ Issues #2221, #2551, #2840, #1604, #1887, #2854, #1762, #1987, #3033, #941, #2082, #1509
**Para**: SlskDown v1.0+
