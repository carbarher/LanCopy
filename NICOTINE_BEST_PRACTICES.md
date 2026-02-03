# 📚 Mejores Prácticas de Nicotine+ para SlskDown

## Análisis de Gestión de Conexiones, Reconexiones, Timers y Políticas

Basado en el análisis del código fuente y issues de Nicotine+, este documento detalla las mejores prácticas que podemos implementar en SlskDown.

---

## 🔌 1. GESTIÓN DE CONEXIONES

### 1.1 Reconexión Automática al Servidor
**Estado en Nicotine+**: Issue #2168 - Solicitado por la comunidad

**Problema identificado**:
- Cuando se pierde la conexión (VPN, red inestable), el usuario debe reconectar manualmente
- Nicotine+ actualmente NO tiene reconexión automática implementada

**Solución propuesta para SlskDown**:
```csharp
// Sistema de reconexión automática con backoff exponencial
private int reconnectAttempts = 0;
private const int MAX_RECONNECT_ATTEMPTS = 10;
private Timer reconnectTimer;
private bool autoReconnectEnabled = true;

private async void OnConnectionLost()
{
    if (!autoReconnectEnabled) return;
    
    reconnectAttempts++;
    if (reconnectAttempts > MAX_RECONNECT_ATTEMPTS)
    {
        Log("❌ Máximo de intentos de reconexión alcanzado");
        reconnectAttempts = 0;
        return;
    }
    
    // Backoff exponencial: 5s, 10s, 20s, 40s, 80s...
    int delaySeconds = Math.Min(5 * (int)Math.Pow(2, reconnectAttempts - 1), 300);
    Log($"🔄 Reintentando conexión en {delaySeconds}s (intento {reconnectAttempts}/{MAX_RECONNECT_ATTEMPTS})");
    
    await Task.Delay(delaySeconds * 1000);
    await ConnectAsync();
}

private void OnConnectionSuccess()
{
    reconnectAttempts = 0; // Reset contador
    Log("✅ Conexión establecida exitosamente");
}
```

**Configuración recomendada**:
- ✅ Auto-reconexión habilitada por defecto
- ✅ Backoff exponencial para evitar spam al servidor
- ✅ Límite de 10 intentos antes de rendirse
- ✅ Delay máximo de 5 minutos entre intentos

---

## ⬇️ 2. POLÍTICAS DE DESCARGA

### 2.1 Auto-Resume/Retry de Descargas
**Estado en Nicotine+**: Issue #2555 - Feature solicitada

**Problema identificado**:
- Archivos con estado "Too many files" o "User logged off" no se reintentan automáticamente
- Usuario debe presionar "Resume" manualmente

**Solución propuesta para SlskDown**:
```csharp
// Sistema de auto-retry con intervalo configurable
private Timer autoRetryTimer;
private int autoRetryIntervalMinutes = 5; // Configurable por usuario
private bool autoRetryEnabled = true;

private void InitializeAutoRetry()
{
    autoRetryTimer = new Timer(autoRetryIntervalMinutes * 60 * 1000);
    autoRetryTimer.Elapsed += async (s, e) => await AutoRetryDownloads();
    autoRetryTimer.Start();
}

private async Task AutoRetryDownloads()
{
    if (!autoRetryEnabled) return;
    
    var failedDownloads = downloads.Where(d => 
        d.Status == "User logged off" ||
        d.Status == "Too many files" ||
        d.Status == "Connection timeout" ||
        d.Status == "Cannot connect"
    ).ToList();
    
    if (failedDownloads.Count > 0)
    {
        Log($"🔄 Auto-retry: Reintentando {failedDownloads.Count} descargas fallidas");
        
        foreach (var download in failedDownloads)
        {
            // Verificar si el usuario está online
            if (download.Status == "User logged off")
            {
                bool isOnline = await CheckUserOnline(download.Username);
                if (!isOnline) continue;
            }
            
            // Verificar slots disponibles
            if (download.Status == "Too many files")
            {
                var userInfo = await GetUserInfo(download.Username);
                if (userInfo.FreeSlots == 0) continue;
            }
            
            await RetryDownload(download);
            await Task.Delay(1000); // Delay entre reintentos
        }
    }
}
```

**Configuración recomendada**:
- ✅ Auto-retry cada 5 minutos (configurable: 1-60 min)
- ✅ Verificar estado del usuario antes de reintentar
- ✅ Verificar slots disponibles
- ✅ Botón "Auto-Retry ON/OFF" en la UI

### 2.2 Retry Automático para Connection Timeout
**Estado en Nicotine+**: Issue #2939 - Feature solicitada

**Problema identificado**:
- "Connection timeout" requiere retry manual
- Muchas veces el retry manual funciona

**Solución propuesta para SlskDown**:
```csharp
// Retry exponencial para connection timeout
private Dictionary<string, int> downloadRetryCount = new Dictionary<string, int>();
private const int MAX_TIMEOUT_RETRIES = 5;

private async Task<bool> DownloadWithRetry(DownloadItem download)
{
    string key = $"{download.Username}_{download.Filename}";
    
    if (!downloadRetryCount.ContainsKey(key))
        downloadRetryCount[key] = 0;
    
    while (downloadRetryCount[key] < MAX_TIMEOUT_RETRIES)
    {
        try
        {
            var result = await AttemptDownload(download);
            
            if (result.Success)
            {
                downloadRetryCount.Remove(key);
                return true;
            }
            
            if (result.Status == "Connection timeout")
            {
                downloadRetryCount[key]++;
                int delay = Math.Min(10 * downloadRetryCount[key], 60); // 10s, 20s, 30s...
                Log($"⏱️ Connection timeout, reintentando en {delay}s (intento {downloadRetryCount[key]}/{MAX_TIMEOUT_RETRIES})");
                await Task.Delay(delay * 1000);
                continue;
            }
            
            // Otro tipo de error, no reintentar
            return false;
        }
        catch (Exception ex)
        {
            Log($"❌ Error en descarga: {ex.Message}");
            return false;
        }
    }
    
    Log($"❌ Máximo de reintentos alcanzado para {download.Filename}");
    downloadRetryCount.Remove(key);
    return false;
}
```

**Configuración recomendada**:
- ✅ Máximo 5 reintentos automáticos para timeout
- ✅ Delay incremental: 10s, 20s, 30s, 40s, 50s
- ✅ Limpiar contador después de éxito o fallo final

### 2.3 Gestión de Cola de Descargas
**Estado en Nicotine+**: Issue #1865 - Optimización de archivos pequeños

**Problema identificado**:
- Round-robin lento para muchos archivos pequeños
- Un archivo cada pocos segundos

**Solución propuesta para SlskDown**:
```csharp
// Política de cola inteligente
private int maxParallelDownloadsPerUser = 3; // Configurable
private int maxTotalParallelDownloads = 10;
private bool batchSmallFiles = true;
private long smallFileThresholdBytes = 5 * 1024 * 1024; // 5 MB

private async Task ProcessDownloadQueue()
{
    var activeDownloads = downloads.Where(d => d.Status == "Downloading").ToList();
    
    if (activeDownloads.Count >= maxTotalParallelDownloads)
        return;
    
    var queuedDownloads = downloads.Where(d => d.Status == "Queued")
        .OrderBy(d => d.Priority)
        .ThenBy(d => d.QueuePosition)
        .ToList();
    
    // Agrupar archivos pequeños del mismo usuario
    if (batchSmallFiles)
    {
        var smallFilesByUser = queuedDownloads
            .Where(d => d.FileSize < smallFileThresholdBytes)
            .GroupBy(d => d.Username)
            .ToList();
        
        foreach (var userGroup in smallFilesByUser)
        {
            int userActiveDownloads = activeDownloads.Count(d => d.Username == userGroup.Key);
            int slotsAvailable = maxParallelDownloadsPerUser - userActiveDownloads;
            
            if (slotsAvailable > 0)
            {
                var filesToDownload = userGroup.Take(slotsAvailable).ToList();
                foreach (var file in filesToDownload)
                {
                    await StartDownload(file);
                }
            }
        }
    }
    
    // Procesar archivos grandes normalmente
    foreach (var download in queuedDownloads)
    {
        if (activeDownloads.Count >= maxTotalParallelDownloads)
            break;
        
        int userActiveDownloads = activeDownloads.Count(d => d.Username == download.Username);
        if (userActiveDownloads < maxParallelDownloadsPerUser)
        {
            await StartDownload(download);
            activeDownloads.Add(download);
        }
    }
}
```

**Configuración recomendada**:
- ✅ 3 descargas paralelas por usuario (configurable: 1-5)
- ✅ 10 descargas paralelas totales (configurable: 5-20)
- ✅ Batch de archivos pequeños (<5MB) del mismo usuario
- ✅ Prioridad: Priority > QueuePosition

---

## 🔍 3. POLÍTICAS DE BÚSQUEDA

### 3.1 Timeout de Búsqueda
**Estado en Nicotine+**: Configurable en settings

**Problema identificado**:
- Búsquedas que tardan demasiado bloquean recursos
- No hay timeout configurable en SlskDown

**Solución propuesta para SlskDown**:
```csharp
// Sistema de timeout configurable para búsquedas
private int searchTimeoutSeconds = 30; // Configurable: 10-120s
private Dictionary<int, CancellationTokenSource> activeSearches = new Dictionary<int, CancellationTokenSource>();

private async Task<List<SearchResult>> SearchWithTimeout(string query)
{
    int searchToken = GenerateSearchToken();
    var cts = new CancellationTokenSource(TimeSpan.FromSeconds(searchTimeoutSeconds));
    activeSearches[searchToken] = cts;
    
    try
    {
        var results = await SearchAsync(query, cts.Token);
        Log($"✅ Búsqueda completada: {results.Count} resultados en {searchTimeoutSeconds}s");
        return results;
    }
    catch (OperationCanceledException)
    {
        Log($"⏱️ Búsqueda cancelada por timeout ({searchTimeoutSeconds}s)");
        return new List<SearchResult>();
    }
    finally
    {
        activeSearches.Remove(searchToken);
        cts.Dispose();
    }
}
```

**Configuración recomendada**:
- ✅ Timeout por defecto: 30 segundos
- ✅ Configurable: 10-120 segundos
- ✅ Cancelación limpia de búsquedas

### 3.2 Límite de Resultados de Búsqueda
**Estado en Nicotine+**: Configurable (response limit)

**Solución propuesta para SlskDown**:
```csharp
// Límites configurables para búsquedas
private int maxSearchResults = 500; // Configurable: 100-2000
private int maxResultsPerUser = 50; // Configurable: 10-200

private List<SearchResult> FilterSearchResults(List<SearchResult> results)
{
    // Limitar resultados por usuario
    var filteredResults = results
        .GroupBy(r => r.Username)
        .SelectMany(g => g.Take(maxResultsPerUser))
        .Take(maxSearchResults)
        .ToList();
    
    Log($"📊 Resultados filtrados: {filteredResults.Count}/{results.Count}");
    return filteredResults;
}
```

**Configuración recomendada**:
- ✅ Máximo 500 resultados totales (configurable)
- ✅ Máximo 50 resultados por usuario (configurable)
- ✅ Evita sobrecarga de UI y memoria

---

## ⏱️ 4. TIMERS Y TAREAS PERIÓDICAS

### 4.1 Sistema de Timers Recomendado
```csharp
// Timers principales del sistema
private void InitializeTimers()
{
    // Timer 1: Auto-retry descargas (5 min)
    autoRetryTimer = new Timer(5 * 60 * 1000);
    autoRetryTimer.Elapsed += async (s, e) => await AutoRetryDownloads();
    autoRetryTimer.Start();
    
    // Timer 2: Verificar estado de usuarios (2 min)
    userStatusTimer = new Timer(2 * 60 * 1000);
    userStatusTimer.Elapsed += async (s, e) => await UpdateUserStatuses();
    userStatusTimer.Start();
    
    // Timer 3: Limpieza de logs antiguos (1 hora)
    logCleanupTimer = new Timer(60 * 60 * 1000);
    logCleanupTimer.Elapsed += (s, e) => CleanupOldLogs();
    logCleanupTimer.Start();
    
    // Timer 4: Guardar estadísticas (10 min)
    statsTimer = new Timer(10 * 60 * 1000);
    statsTimer.Elapsed += (s, e) => SaveStats();
    statsTimer.Start();
    
    // Timer 5: Verificar conexión al servidor (30s)
    connectionCheckTimer = new Timer(30 * 1000);
    connectionCheckTimer.Elapsed += async (s, e) => await CheckServerConnection();
    connectionCheckTimer.Start();
    
    // Timer 6: Actualizar UI (1s)
    uiUpdateTimer = new Timer(1000);
    uiUpdateTimer.Elapsed += (s, e) => UpdateUI();
    uiUpdateTimer.Start();
}

private async Task CheckServerConnection()
{
    if (client == null || !client.State.HasFlag(SoulseekClientStates.Connected))
    {
        Log("⚠️ Conexión perdida, intentando reconectar...");
        await OnConnectionLost();
    }
}
```

**Timers recomendados**:
- ✅ Auto-retry descargas: 5 minutos
- ✅ Verificar usuarios: 2 minutos
- ✅ Limpieza logs: 1 hora
- ✅ Guardar stats: 10 minutos
- ✅ Check conexión: 30 segundos
- ✅ Actualizar UI: 1 segundo

---

## 🎯 5. POLÍTICAS DE PRIORIDAD

### 5.1 Sistema de Prioridad de Descargas
```csharp
public enum DownloadPriority
{
    Low = 0,
    Normal = 1,
    High = 2,
    Critical = 3
}

private void PrioritizeDownloads()
{
    // Criterios de priorización automática
    foreach (var download in downloads)
    {
        // 1. Archivos pequeños = Mayor prioridad (descargan rápido)
        if (download.FileSize < 10 * 1024 * 1024 && priorityBySize)
            download.Priority = DownloadPriority.High;
        
        // 2. Usuarios con pocos slots = Mayor prioridad (aprovechar oportunidad)
        if (download.UserFreeSlots <= 2)
            download.Priority = DownloadPriority.High;
        
        // 3. Archivos casi completos = Mayor prioridad (terminar primero)
        if (download.BytesDownloaded > download.FileSize * 0.9)
            download.Priority = DownloadPriority.Critical;
        
        // 4. Usuarios lentos = Menor prioridad
        if (download.AverageSpeed < 50 * 1024) // <50 KB/s
            download.Priority = DownloadPriority.Low;
    }
}
```

---

## 📊 6. MONITOREO Y ESTADÍSTICAS

### 6.1 Sistema de Métricas
```csharp
public class ConnectionMetrics
{
    public int TotalConnections { get; set; }
    public int FailedConnections { get; set; }
    public int TimeoutConnections { get; set; }
    public int SuccessfulReconnections { get; set; }
    public TimeSpan AverageConnectionTime { get; set; }
    public DateTime LastConnectionLost { get; set; }
    public int ConsecutiveFailures { get; set; }
}

public class DownloadMetrics
{
    public int TotalDownloads { get; set; }
    public int CompletedDownloads { get; set; }
    public int FailedDownloads { get; set; }
    public int AutoRetriedDownloads { get; set; }
    public long TotalBytesDownloaded { get; set; }
    public double AverageSpeed { get; set; }
    public TimeSpan TotalDownloadTime { get; set; }
}

private void LogMetrics()
{
    Log($"📊 MÉTRICAS DE CONEXIÓN:");
    Log($"   Conexiones totales: {connectionMetrics.TotalConnections}");
    Log($"   Conexiones fallidas: {connectionMetrics.FailedConnections}");
    Log($"   Reconexiones exitosas: {connectionMetrics.SuccessfulReconnections}");
    Log($"   Tasa de éxito: {(1 - (double)connectionMetrics.FailedConnections / connectionMetrics.TotalConnections) * 100:F2}%");
    
    Log($"📊 MÉTRICAS DE DESCARGA:");
    Log($"   Descargas completadas: {downloadMetrics.CompletedDownloads}/{downloadMetrics.TotalDownloads}");
    Log($"   Auto-retries exitosos: {downloadMetrics.AutoRetriedDownloads}");
    Log($"   Velocidad promedio: {FormatSpeed(downloadMetrics.AverageSpeed)}");
    Log($"   Total descargado: {FormatSize(downloadMetrics.TotalBytesDownloaded)}");
}
```

---

## 🔧 7. CONFIGURACIÓN RECOMENDADA PARA SLSKDOWN

### 7.1 Valores por Defecto Óptimos
```json
{
  "connection": {
    "autoReconnect": true,
    "maxReconnectAttempts": 10,
    "reconnectBackoffSeconds": [5, 10, 20, 40, 80, 160, 300],
    "connectionCheckIntervalSeconds": 30,
    "connectionTimeoutSeconds": 30
  },
  "downloads": {
    "autoRetryEnabled": true,
    "autoRetryIntervalMinutes": 5,
    "maxTimeoutRetries": 5,
    "maxParallelDownloadsPerUser": 3,
    "maxTotalParallelDownloads": 10,
    "batchSmallFiles": true,
    "smallFileThresholdMB": 5,
    "priorityBySize": true,
    "priorityBySlots": true
  },
  "searches": {
    "searchTimeoutSeconds": 30,
    "maxSearchResults": 500,
    "maxResultsPerUser": 50,
    "responseLimit": 100,
    "fileLimit": 50
  },
  "timers": {
    "autoRetryIntervalMinutes": 5,
    "userStatusCheckMinutes": 2,
    "logCleanupHours": 1,
    "statsUpdateMinutes": 10,
    "uiUpdateSeconds": 1
  }
}
```

---

## 📝 8. PLAN DE IMPLEMENTACIÓN PARA SLSKDOWN

### Fase 1: Conexiones (Alta Prioridad)
- [ ] Implementar reconexión automática con backoff exponencial
- [ ] Timer de verificación de conexión (30s)
- [ ] Métricas de conexión
- [ ] UI: Indicador de estado de conexión en tiempo real

### Fase 2: Descargas (Alta Prioridad)
- [ ] Sistema de auto-retry cada 5 minutos
- [ ] Retry automático para connection timeout (5 intentos)
- [ ] Gestión inteligente de cola (batch de archivos pequeños)
- [ ] Sistema de prioridades automático
- [ ] UI: Botón "Auto-Retry ON/OFF"

### Fase 3: Búsquedas (Media Prioridad)
- [ ] Timeout configurable para búsquedas
- [ ] Límites de resultados por usuario
- [ ] Cancelación limpia de búsquedas
- [ ] UI: Configuración de timeouts en Settings

### Fase 4: Timers (Media Prioridad)
- [ ] Implementar todos los timers recomendados
- [ ] Sistema de métricas completo
- [ ] Limpieza automática de recursos

### Fase 5: Configuración (Baja Prioridad)
- [ ] Panel de configuración avanzada
- [ ] Exportar/importar configuración
- [ ] Perfiles de configuración (Agresivo, Balanceado, Conservador)

---

## 🎓 CONCLUSIONES

### Lecciones Clave de Nicotine+:
1. **Reconexión automática es CRÍTICA** - Muchos usuarios la solicitan
2. **Auto-retry de descargas ahorra tiempo** - Evita monitoreo manual
3. **Timeouts configurables son esenciales** - Diferentes redes necesitan diferentes valores
4. **Batch de archivos pequeños mejora eficiencia** - Round-robin es lento
5. **Métricas ayudan a diagnosticar problemas** - Visibilidad del sistema

### Ventajas Competitivas para SlskDown:
- ✅ Implementar features que Nicotine+ aún no tiene
- ✅ Mejor experiencia "fire and forget"
- ✅ Menos intervención manual del usuario
- ✅ Mayor robustez ante problemas de red
- ✅ Mejor aprovechamiento de recursos

---

**Documento creado**: 2026-01-10
**Basado en**: Nicotine+ Issues #2168, #2555, #2939, #2958, #1865
**Para**: SlskDown v1.0+
