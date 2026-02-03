# 🔧 Guía de Integración - Mejoras de Nicotine+

## 📋 Resumen

Esta guía muestra cómo integrar los 9 componentes de Nicotine+ en el código existente de SlskDown con ejemplos prácticos y listos para usar.

---

## 1️⃣ Integrar TransferConfiguration

### **Paso 1: Agregar campo en DownloadManager**

```csharp
// En DownloadManager.cs, agregar después de línea 22:
using SlskDown.Core.Configuration;

// Agregar campo privado:
private readonly TransferConfiguration transferConfig;
```

### **Paso 2: Inicializar en constructor**

```csharp
// En constructor de DownloadManager:
public DownloadManager(DownloadManagerConfig config, ...)
{
    this.config = config;
    
    // NUEVO: Inicializar configuración de transferencias
    this.transferConfig = LoadTransferConfiguration();
    
    // ... resto del constructor
}

private TransferConfiguration LoadTransferConfiguration()
{
    var configPath = Path.Combine(config.DataDirectory, "transfer_config.json");
    
    if (File.Exists(configPath))
    {
        try
        {
            var json = File.ReadAllText(configPath);
            return JsonSerializer.Deserialize<TransferConfiguration>(json);
        }
        catch
        {
            // Usar configuración por defecto si falla la carga
        }
    }
    
    // Configuración optimizada para velocidad
    return TransferConfiguration.CreateSpeedOptimized();
}
```

### **Paso 3: Usar configuración en operaciones**

```csharp
// Reemplazar valores hardcoded por configuración:

// ANTES:
private const int MAX_RETRIES = 3;
private const int MAX_PARALLEL_DOWNLOADS = 3;

// DESPUÉS:
private int MaxRetries => transferConfig.MaxRetries;
private int MaxParallelDownloads => transferConfig.MaxParallelDownloads;

// En método de descarga:
if (task.RetryCount >= transferConfig.MaxRetries)
{
    // Máximo de reintentos alcanzado
}

// Timeout de stall:
var stallTimeout = task.File.Network == "eMule" 
    ? transferConfig.EMuleStallTimeout 
    : transferConfig.StallTimeout;
```

---

## 2️⃣ Integrar TransferStatistics

### **Paso 1: Agregar campo en DownloadManager**

```csharp
using SlskDown.Core.Statistics;

// Agregar campo:
private readonly TransferStatistics transferStats;
```

### **Paso 2: Inicializar en constructor**

```csharp
public DownloadManager(DownloadManagerConfig config, ...)
{
    // ...
    this.transferStats = new TransferStatistics();
}
```

### **Paso 3: Registrar eventos de transferencia**

```csharp
// Al iniciar descarga:
private async Task StartDownloadAsync(DownloadTask task)
{
    // NUEVO: Registrar inicio
    transferStats.RecordTransferStart(task.Username, task.File.Network ?? "Soulseek");
    
    // ... código de descarga existente
}

// Durante progreso:
private void OnTransferProgress(DownloadTask task, long currentOffset, long lastOffset, double speed)
{
    // NUEVO: Actualizar estadísticas
    transferStats.UpdateProgress(
        task.Username, 
        task.File.Network ?? "Soulseek",
        currentOffset,
        lastOffset,
        speed
    );
    
    // ... resto del código
}

// Al completar exitosamente:
private void OnTransferCompleted(DownloadTask task)
{
    var duration = DateTime.UtcNow - (task.StartedAt ?? DateTime.UtcNow);
    
    // NUEVO: Registrar éxito
    transferStats.RecordTransferSuccess(
        task.Username,
        task.File.Network ?? "Soulseek",
        task.File.SizeBytes,
        duration
    );
}

// Al fallar:
private void OnTransferFailed(DownloadTask task, string reason)
{
    // NUEVO: Registrar fallo
    transferStats.RecordTransferFailure(
        task.Username,
        task.File.Network ?? "Soulseek",
        reason
    );
}
```

### **Paso 4: Obtener estadísticas para UI**

```csharp
// Método público para MainForm:
public Dictionary<string, object> GetTransferStatistics()
{
    var globalStats = transferStats.GetGlobalStats();
    var topUsers = transferStats.GetTopUsersBySpeed(10);
    
    return new Dictionary<string, object>
    {
        ["TotalBytes"] = globalStats.TotalBytesTransferred,
        ["TotalTransfers"] = globalStats.TotalTransfers,
        ["SuccessRate"] = globalStats.SuccessRate * 100,
        ["AverageSpeed"] = globalStats.AverageSpeed,
        ["TopUsers"] = topUsers
    };
}
```

---

## 3️⃣ Integrar UserQueueManager

### **Paso 1: Agregar campo en DownloadManager**

```csharp
using SlskDown.Core.Queue;

// Agregar campo:
private readonly UserQueueManager queueManager;
```

### **Paso 2: Inicializar en constructor**

```csharp
public DownloadManager(DownloadManagerConfig config, ...)
{
    // ...
    // Límite por defecto de 50 transferencias por usuario
    this.queueManager = new UserQueueManager(defaultQueueLimit: 50);
}
```

### **Paso 3: Verificar límites antes de agregar a cola**

```csharp
// En método AddToQueue o similar:
public bool TryAddToQueue(DownloadTask task)
{
    // NUEVO: Verificar límite de cola del usuario
    if (!queueManager.CanQueueTransfer(task.Username))
    {
        var limit = queueManager.GetQueueLimit(task.Username);
        var current = queueManager.GetQueueSize(task.Username);
        
        Log($"⚠️ Cola de {task.Username} llena ({current}/{limit})");
        
        task.Status = DownloadStatus.QueueFull;
        task.ErrorMessage = $"Cola del usuario llena ({current}/{limit})";
        return false;
    }
    
    // Agregar a cola
    lock (downloadQueueLock)
    {
        downloadQueue.Add(task);
        queueManager.IncrementQueueSize(task.Username);
    }
    
    return true;
}
```

### **Paso 4: Actualizar límites desde mensajes de Soulseek**

```csharp
// Cuando se recibe información de slots del usuario:
private void OnUserInfoReceived(string username, int uploadSlots)
{
    // NUEVO: Actualizar límite de cola basado en slots del usuario
    // Respetar límites del usuario para mejor relación con comunidad
    queueManager.UpdateUserQueueLimit(username, uploadSlots * 2);
    
    Log($"📊 Límite de cola para {username}: {uploadSlots * 2}");
}
```

### **Paso 5: Decrementar al completar/fallar**

```csharp
// Al completar o fallar una transferencia:
private void OnTransferFinished(DownloadTask task)
{
    // NUEVO: Liberar espacio en cola del usuario
    queueManager.DecrementQueueSize(task.Username);
    
    var available = queueManager.GetAvailableQueueSpace(task.Username);
    Log($"📊 Espacio disponible en cola de {task.Username}: {available}");
}
```

---

## 4️⃣ Integrar TransferStatusHelper en UI

### **Paso 1: Agregar using en MainForm.cs**

```csharp
using SlskDown.UI;
using SlskDown.Models;
```

### **Paso 2: Actualizar método de actualización de UI**

```csharp
// En MainForm.cs, método UpdateDownloadListView o similar:
private void UpdateDownloadItem(ListViewItem item, DownloadTask task)
{
    // ANTES:
    // item.SubItems[3].Text = task.Status.ToString();
    
    // DESPUÉS: Usar mensajes amigables
    item.SubItems[3].Text = TransferStatusHelper.GetUserFriendlyStatus(task);
    
    // NUEVO: Aplicar color por estado
    var color = TransferStatusHelper.GetStatusColor(task.Status);
    item.SubItems[3].ForeColor = color;
    
    // ... resto de columnas
}
```

### **Paso 3: Agregar tooltips detallados**

```csharp
// En MainForm.cs, evento MouseHover de ListView:
private void lvDownloads_MouseMove(object sender, MouseEventArgs e)
{
    var hitTest = lvDownloads.HitTest(e.Location);
    if (hitTest.Item != null)
    {
        var task = hitTest.Item.Tag as DownloadTask;
        if (task != null)
        {
            // NUEVO: Generar tooltip detallado
            var tooltip = TransferStatusHelper.GenerateTransferTooltip(task);
            toolTip1.SetToolTip(lvDownloads, tooltip);
        }
    }
}
```

---

## 5️⃣ Integrar NetworkEventBus

### **Paso 1: Agregar campo global en MainForm**

```csharp
using SlskDown.Core.Events;

// Campo privado:
private readonly NetworkEventBus eventBus;
```

### **Paso 2: Inicializar en constructor**

```csharp
public MainForm()
{
    InitializeComponent();
    
    // NUEVO: Inicializar event bus
    this.eventBus = new NetworkEventBus();
    
    // Suscribirse a eventos
    SetupEventHandlers();
}

private void SetupEventHandlers()
{
    // Eventos de transferencia
    eventBus.Subscribe<TransferStartedMessage>(OnTransferStarted);
    eventBus.Subscribe<TransferProgressMessage>(OnTransferProgress);
    eventBus.Subscribe<TransferCompletedMessage>(OnTransferCompleted);
    eventBus.Subscribe<TransferFailedMessage>(OnTransferFailed);
    
    // Eventos de servidor
    eventBus.Subscribe<ServerConnectedMessage>(OnServerConnected);
    eventBus.Subscribe<ServerDisconnectedMessage>(OnServerDisconnected);
    
    // Eventos de búsqueda
    eventBus.Subscribe<SearchResultsMessage>(OnSearchResults);
}
```

### **Paso 3: Publicar eventos desde DownloadManager**

```csharp
// En DownloadManager, pasar eventBus en constructor:
public DownloadManager(
    DownloadManagerConfig config, 
    NetworkEventBus eventBus,  // NUEVO
    ...)
{
    this.eventBus = eventBus;
}

// Publicar eventos:
private async Task StartDownloadAsync(DownloadTask task)
{
    // ... iniciar descarga
    
    // NUEVO: Publicar evento
    eventBus.Publish(new TransferStartedMessage
    {
        TransferId = task.Id,
        Username = task.Username,
        FileName = task.FileName,
        FileSize = task.File.SizeBytes,
        StartedAt = DateTime.UtcNow
    });
}

private void OnProgressUpdate(DownloadTask task, long bytes, double speed)
{
    // NUEVO: Publicar progreso
    eventBus.Publish(new TransferProgressMessage
    {
        TransferId = task.Id,
        Username = task.Username,
        FileName = task.FileName,
        BytesTransferred = bytes,
        TotalBytes = task.File.SizeBytes,
        Speed = speed,
        Progress = (double)bytes / task.File.SizeBytes * 100
    });
}
```

### **Paso 4: Handlers en MainForm**

```csharp
// Handlers de eventos (thread-safe con Invoke):
private void OnTransferStarted(TransferStartedMessage msg)
{
    if (InvokeRequired)
    {
        Invoke(new Action(() => OnTransferStarted(msg)));
        return;
    }
    
    Log($"▶️ Iniciada: {msg.FileName} desde {msg.Username}");
    UpdateDownloadsList();
}

private void OnTransferCompleted(TransferCompletedMessage msg)
{
    if (InvokeRequired)
    {
        Invoke(new Action(() => OnTransferCompleted(msg)));
        return;
    }
    
    Log($"✅ Completada: {msg.FileName} ({FormatSpeed(msg.AverageSpeed)})");
    UpdateDownloadsList();
    ShowNotification($"Descarga completada: {msg.FileName}");
}

private void OnTransferFailed(TransferFailedMessage msg)
{
    if (InvokeRequired)
    {
        Invoke(new Action(() => OnTransferFailed(msg)));
        return;
    }
    
    Log($"❌ Fallida: {msg.FileName} - {msg.ErrorMessage}");
    UpdateDownloadsList();
}
```

---

## 6️⃣ Integrar TransferCleanup

### **Paso 1: Reemplazar código de abort manual**

```csharp
// ANTES (código manual de abort):
private async Task AbortDownload(DownloadTask task)
{
    task.CancellationTokenSource?.Cancel();
    task.FileStream?.Dispose();
    task.Status = DownloadStatus.Cancelled;
    // ... más código manual
}

// DESPUÉS (usar TransferCleanup):
using SlskDown.Core.Transfers;

private async Task AbortDownload(DownloadTask task)
{
    await TransferCleanup.AbortTransferAsync(
        task,
        TransferStatus.Cancelled,
        reason: "Usuario canceló la descarga",
        logger: Log
    );
    
    // Cleanup adicional específico de la app
    await CleanupUserResourcesIfNeeded(task.Username);
}
```

### **Paso 2: Validar archivos parciales al reanudar**

```csharp
// Al reanudar descarga:
private async Task ResumeDownload(DownloadTask task)
{
    // NUEVO: Validar integridad de archivo parcial
    if (!TransferCleanup.ValidatePartialFile(task, Log))
    {
        Log($"⚠️ Archivo parcial corrupto, reiniciando desde cero: {task.FileName}");
        task.CurrentByteOffset = 0;
    }
    
    // ... continuar con descarga
}
```

### **Paso 3: Limpieza periódica de archivos temporales**

```csharp
// En MainForm o DownloadManager, timer periódico:
private void CleanupTimer_Tick(object sender, EventArgs e)
{
    // NUEVO: Limpiar archivos temporales antiguos
    TransferCleanup.CleanupTemporaryFiles(
        config.DownloadDirectory,
        logger: Log
    );
}
```

---

## 7️⃣ Integrar Estados y Errores Granulares

### **Paso 1: Usar TransferStatus en lugar de DownloadStatus**

```csharp
// Migrar gradualmente de DownloadStatus a TransferStatus:
using SlskDown.Models;

// En DownloadTask, agregar propiedad:
public TransferStatus TransferStatus { get; set; }

// Mapear entre enums durante transición:
private TransferStatus MapToTransferStatus(DownloadStatus oldStatus)
{
    return oldStatus switch
    {
        DownloadStatus.Queued => TransferStatus.Queued,
        DownloadStatus.Downloading => TransferStatus.Transferring,
        DownloadStatus.Completed => TransferStatus.Finished,
        DownloadStatus.Failed => TransferStatus.NetworkError,
        DownloadStatus.Cancelled => TransferStatus.Cancelled,
        _ => TransferStatus.Unknown
    };
}
```

### **Paso 2: Usar TransferError para clasificación**

```csharp
// Al capturar excepciones:
try
{
    await DownloadFileAsync(task);
}
catch (Exception ex)
{
    // NUEVO: Clasificar error automáticamente
    var error = TransferError.FromException(ex);
    
    task.TransferStatus = MapFailureReasonToStatus(error.Reason);
    task.ErrorMessage = error.GetUserFriendlyMessage();
    
    // Decidir si reintentar basado en clasificación
    if (error.IsRetryable && task.RetryCount < transferConfig.MaxRetries)
    {
        task.TransferStatus = TransferStatus.RetryScheduled;
        task.RetryAt = DateTime.UtcNow.Add(error.SuggestedRetryDelay);
        
        Log($"🔄 Reintento programado en {error.SuggestedRetryDelay.TotalMinutes:F0} min");
    }
    else
    {
        Log($"❌ Error no retryable: {error.Reason}");
    }
}

private TransferStatus MapFailureReasonToStatus(TransferFailureReason reason)
{
    return reason switch
    {
        TransferFailureReason.ConnectionTimeout => TransferStatus.ConnectionTimeout,
        TransferFailureReason.UserLoggedOff => TransferStatus.UserLoggedOff,
        TransferFailureReason.UserBusy => TransferStatus.UserBusy,
        TransferFailureReason.QueueFull => TransferStatus.QueueFull,
        TransferFailureReason.FileNotShared => TransferStatus.FileNotShared,
        TransferFailureReason.DiskFull => TransferStatus.DiskFull,
        _ => TransferStatus.NetworkError
    };
}
```

### **Paso 3: Clasificar rechazos de Soulseek**

```csharp
// Al recibir rechazo de transferencia:
private void OnTransferRejected(string username, string filename, string message)
{
    // NUEVO: Clasificar rechazo
    var error = TransferError.FromSoulseekRejection(message);
    
    var task = FindTask(username, filename);
    if (task != null)
    {
        task.TransferStatus = MapFailureReasonToStatus(error.Reason);
        task.ErrorMessage = error.GetUserFriendlyMessage();
        
        if (error.IsRetryable)
        {
            ScheduleRetry(task, error.SuggestedRetryDelay);
        }
    }
}
```

---

## 8️⃣ Integrar SoulseekConnectionPool

### **Paso 1: Crear instancia global del pool**

```csharp
using SlskDown.Core.Protocol;

// En MainForm o clase de gestión de red:
private SoulseekConnectionPool connectionPool;

private void InitializeNetworking()
{
    // NUEVO: Inicializar pool de conexiones
    connectionPool = new SoulseekConnectionPool(
        maxConnectionsPerUser: 3,
        idleTimeout: TimeSpan.FromMinutes(5)
    );
    
    // Timer para limpieza periódica
    var cleanupTimer = new System.Threading.Timer(_ =>
    {
        connectionPool.CleanupIdleConnections();
    }, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
}
```

### **Paso 2: Usar pool en operaciones de red**

```csharp
// ANTES (crear conexión nueva cada vez):
private async Task<Stream> ConnectToPeer(string username)
{
    var endpoint = await GetPeerEndpoint(username);
    var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
    await socket.ConnectAsync(endpoint);
    return new NetworkStream(socket);
}

// DESPUÉS (usar pool):
private async Task<Stream> ConnectToPeer(string username)
{
    var endpoint = await GetPeerEndpoint(username);
    
    // NUEVO: Obtener o crear conexión desde pool
    var connection = await connectionPool.GetOrCreateConnectionAsync(
        username,
        endpoint,
        connectionFactory: async (ep) =>
        {
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            await socket.ConnectAsync(ep);
            return new NetworkStream(socket);
        }
    );
    
    return connection;
}
```

### **Paso 3: Obtener estadísticas del pool**

```csharp
// Para mostrar en UI o logs:
private void ShowPoolStatistics()
{
    var stats = connectionPool.GetStatistics();
    
    Log($"📊 Pool de conexiones:");
    Log($"   Total: {stats.TotalConnections}");
    Log($"   Activas: {stats.ActiveConnections}");
    Log($"   Idle: {stats.IdleConnections}");
    Log($"   Hits: {stats.CacheHits}");
    Log($"   Misses: {stats.CacheMisses}");
    Log($"   Hit Rate: {stats.HitRate:P1}");
}
```

---

## 9️⃣ Ejemplo Completo de Integración

```csharp
// Ejemplo completo de descarga usando todos los componentes:

public class EnhancedDownloadManager
{
    private readonly TransferConfiguration config;
    private readonly TransferStatistics stats;
    private readonly UserQueueManager queueManager;
    private readonly NetworkEventBus eventBus;
    private readonly SoulseekConnectionPool connectionPool;
    
    public async Task<bool> DownloadFileAsync(DownloadTask task)
    {
        // 1. Verificar límite de cola
        if (!queueManager.CanQueueTransfer(task.Username))
        {
            task.TransferStatus = TransferStatus.QueueFull;
            return false;
        }
        
        // 2. Validar archivo parcial si existe
        if (!TransferCleanup.ValidatePartialFile(task, Log))
        {
            task.CurrentByteOffset = 0;
        }
        
        // 3. Registrar inicio
        stats.RecordTransferStart(task.Username, task.Network);
        queueManager.IncrementQueueSize(task.Username);
        
        // 4. Publicar evento
        eventBus.Publish(new TransferStartedMessage
        {
            TransferId = task.Id,
            Username = task.Username,
            FileName = task.FileName,
            StartedAt = DateTime.UtcNow
        });
        
        try
        {
            // 5. Obtener conexión del pool
            var endpoint = await GetPeerEndpoint(task.Username);
            var connection = await connectionPool.GetOrCreateConnectionAsync(
                task.Username, endpoint, CreateConnection);
            
            // 6. Descargar con progreso
            task.TransferStatus = TransferStatus.Transferring;
            var startTime = DateTime.UtcNow;
            
            await DownloadWithProgress(connection, task, (bytes, speed) =>
            {
                // Actualizar estadísticas
                stats.UpdateProgress(task.Username, task.Network, 
                    bytes, task.LastByteOffset ?? 0, speed);
                
                // Publicar progreso
                eventBus.Publish(new TransferProgressMessage
                {
                    TransferId = task.Id,
                    BytesTransferred = bytes,
                    Speed = speed
                });
            });
            
            // 7. Completado exitosamente
            var duration = DateTime.UtcNow - startTime;
            task.TransferStatus = TransferStatus.Finished;
            
            stats.RecordTransferSuccess(task.Username, task.Network, 
                task.FileSize, duration);
            
            eventBus.Publish(new TransferCompletedMessage
            {
                TransferId = task.Id,
                CompletedAt = DateTime.UtcNow,
                Duration = duration
            });
            
            return true;
        }
        catch (Exception ex)
        {
            // 8. Clasificar error
            var error = TransferError.FromException(ex);
            task.TransferStatus = MapFailureReasonToStatus(error.Reason);
            task.ErrorMessage = error.GetUserFriendlyMessage();
            
            stats.RecordTransferFailure(task.Username, task.Network, 
                error.Reason.ToString());
            
            eventBus.Publish(new TransferFailedMessage
            {
                TransferId = task.Id,
                ErrorMessage = error.Message,
                FailureReason = error.Reason.ToString()
            });
            
            // 9. Decidir reintento
            if (error.IsRetryable && task.RetryCount < config.MaxRetries)
            {
                task.TransferStatus = TransferStatus.RetryScheduled;
                task.RetryAt = DateTime.UtcNow.Add(error.SuggestedRetryDelay);
                return false;
            }
            
            // 10. Cleanup robusto
            await TransferCleanup.AbortTransferAsync(task, 
                task.TransferStatus, error.Message, Log);
            
            return false;
        }
        finally
        {
            // 11. Liberar espacio en cola
            queueManager.DecrementQueueSize(task.Username);
        }
    }
}
```

---

## ✅ Checklist de Integración

- [ ] TransferConfiguration cargada y usada en DownloadManager
- [ ] TransferStatistics registrando todos los eventos
- [ ] UserQueueManager verificando límites de cola
- [ ] TransferStatusHelper usado en toda la UI
- [ ] NetworkEventBus publicando y recibiendo eventos
- [ ] TransferCleanup usado en abort/cancel
- [ ] Estados granulares (TransferStatus) en uso
- [ ] Errores clasificados (TransferError) automáticamente
- [ ] SoulseekConnectionPool reutilizando conexiones
- [ ] Tooltips detallados en ListView de descargas

---

## 📊 Verificación de Funcionamiento

### **Logs Esperados**

```
[Inicio]
📊 Pool de conexiones inicializado (max 3 por usuario)
✅ TransferConfiguration cargada (SpeedOptimized)
✅ TransferStatistics inicializado
✅ UserQueueManager inicializado (límite 50)

[Durante Descarga]
▶️ Iniciada: libro.epub desde user123
📊 Espacio en cola de user123: 49/50
⚡ Velocidad: 1.25 MB/s (progreso: 45.2%)
✅ Completada: libro.epub (1.25 MB/s promedio)
📊 Estadísticas user123: 3 éxitos, 0 fallos, 1.18 MB/s promedio

[En Caso de Error]
❌ Error: Connection timeout
🔄 Reintento programado en 2 min (intento 2/3)
📊 Pool: 5 conexiones (3 activas, 2 idle), hit rate: 67%
```

---

**Fecha**: 2025-01-04  
**Versión**: SlskDown v2.0 - Nicotine+ Enhanced  
**Estado**: ✅ Guía completa lista para implementación
