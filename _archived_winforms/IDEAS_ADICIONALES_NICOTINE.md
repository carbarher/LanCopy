# Ideas Adicionales de Nicotine+ y Protocolo Soulseek
## Mejoras de Prioridad Media-Baja

Basado en el análisis exhaustivo de la documentación del protocolo Soulseek y el código de Nicotine+, aquí hay **ideas adicionales** que podrían mejorar SlskDown:

---

## 🎵 1. Metadatos de Audio Mejorados

### Problema Actual
SlskDown probablemente solo usa bitrate y duración básicos.

### Mejora del Protocolo Soulseek
El protocolo define **6 tipos de atributos de archivo**:
- `0`: Bitrate
- `1`: Duration (duración)
- `2`: VBR (variable bitrate)
- `3`: (no documentado)
- `4`: Sample Rate (frecuencia de muestreo)
- `5`: Bit Depth (profundidad de bits)

### Implementación Sugerida

```csharp
// Models/AudioMetadata.cs
public class AudioMetadata
{
    public int? Bitrate { get; set; }           // Atributo 0
    public int? Duration { get; set; }          // Atributo 1
    public bool IsVBR { get; set; }             // Atributo 2
    public int? SampleRate { get; set; }        // Atributo 4 (44100, 48000, etc.)
    public int? BitDepth { get; set; }          // Atributo 5 (16, 24, 32 bits)
    
    public string Quality => GetQualityDescription();
    
    private string GetQualityDescription()
    {
        // Lossless (FLAC, WAV, APE)
        if (SampleRate.HasValue && BitDepth.HasValue)
        {
            return $"{SampleRate/1000}kHz/{BitDepth}bit";
        }
        
        // Lossy (MP3, OGG, etc.)
        if (Bitrate.HasValue)
        {
            var vbr = IsVBR ? " VBR" : "";
            return $"{Bitrate}kbps{vbr}";
        }
        
        return "Unknown";
    }
}
```

### Filtros Avanzados

```csharp
// En búsqueda, permitir filtrar por calidad
public class SearchFilters
{
    public int? MinBitrate { get; set; }        // Ej: 320 kbps mínimo
    public int? MinSampleRate { get; set; }     // Ej: 44100 Hz mínimo
    public int? MinBitDepth { get; set; }       // Ej: 16 bits mínimo
    public bool LosslessOnly { get; set; }      // Solo FLAC/WAV/APE
    public bool ExcludeVBR { get; set; }        // Excluir VBR
}
```

**Beneficio:** Búsquedas más precisas de audio de alta calidad.

---

## 📊 2. Sistema de Posición en Cola Mejorado

### Protocolo: PlaceInQueueRequest/Response

**Peer Code 51:** `PlaceInQueueRequest`
```
Send: string filename
```

**Peer Code 44:** `PlaceInQueueResponse`
```
Receive: string filename, uint32 place
```

### Problema Actual
SlskDown probablemente no solicita proactivamente la posición en cola.

### Mejora Sugerida

```csharp
// Core/QueuePositionTracker.cs
public class QueuePositionTracker
{
    private readonly Dictionary<string, QueueInfo> queuePositions = new();
    private readonly Timer refreshTimer;
    
    public class QueueInfo
    {
        public string Username { get; set; }
        public string Filename { get; set; }
        public int Position { get; set; }
        public DateTime LastUpdated { get; set; }
        public TimeSpan EstimatedWait { get; set; }
    }
    
    public QueuePositionTracker()
    {
        // Actualizar posiciones cada 2 minutos (como Nicotine+)
        refreshTimer = new Timer(_ => RefreshAllPositions(), null, 
            TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(2));
    }
    
    public async Task RequestQueuePositionAsync(string username, string filename)
    {
        // Enviar PlaceInQueueRequest (Peer Code 51)
        await client.SendPeerMessageAsync(username, 
            new PlaceInQueueRequest(filename));
    }
    
    public void UpdatePosition(string username, string filename, int position)
    {
        var key = $"{username}|{filename}";
        queuePositions[key] = new QueueInfo
        {
            Username = username,
            Filename = filename,
            Position = position,
            LastUpdated = DateTime.Now,
            EstimatedWait = EstimateWaitTime(position)
        };
        
        // Notificar UI
        OnQueuePositionUpdated?.Invoke(this, queuePositions[key]);
    }
    
    private TimeSpan EstimateWaitTime(int position)
    {
        // Estimación: ~3 minutos por archivo en cola
        return TimeSpan.FromMinutes(position * 3);
    }
}
```

**UI Mejorada:**
```
Archivo: song.mp3
Usuario: john_doe
Estado: En cola - Posición #5 (~15 min de espera)
```

**Beneficio:** Usuario sabe cuánto tiempo falta para que empiece su descarga.

---

## 🔄 3. Uso Correcto de QueueUpload (Peer Code 43)

### Protocolo Moderno vs Legacy

**Método Moderno (Nicotine+ >= 3.0.3, SoulseekQt):**
```
1. Cliente → Proveedor: QueueUpload (Peer Code 43)
2. Proveedor → Cliente: TransferRequest (Peer Code 40)
3. Cliente → Proveedor: TransferResponse (aceptar)
4. Proveedor inicia transferencia
```

**Método Legacy (slskd, Seeker):**
```
1. Cliente → Proveedor: TransferRequest direction=0 (Peer Code 40)
2. Proveedor → Cliente: TransferResponse
3. Proveedor inicia transferencia
```

### Verificación en SlskDown

```csharp
// Verificar qué método usa Soulseek.NET
// Si usa legacy, considerar implementar fallback

public async Task RequestDownloadAsync(string username, string filename)
{
    try
    {
        // Intentar método moderno primero
        await client.QueueUploadAsync(username, filename);
        Log($"✅ QueueUpload enviado (método moderno)");
    }
    catch (NotSupportedException)
    {
        // Fallback a método legacy
        await client.TransferRequestAsync(username, filename, direction: 0);
        Log($"⚠️ TransferRequest enviado (método legacy)");
    }
}
```

**Beneficio:** Mayor compatibilidad con diferentes clientes Soulseek.

---

## 🌐 4. Red Distribuida (Distributed Network)

### Protocolo: Distributed Messages

Nicotine+ participa en la **red distribuida** de Soulseek para búsquedas más rápidas:

**Distributed Code 3:** `DistribSearch`
```
Receive: uint32 unknown, string username, uint32 token, string query
```

**Distributed Code 0:** `DistribPing`
```
Send/Receive: uint32 ping
```

### Concepto
- Clientes actúan como nodos de búsqueda distribuida
- Búsquedas se propagan por la red P2P
- Más rápido que solo búsqueda centralizada

### Implementación (Avanzada)

```csharp
// Core/DistributedNetwork.cs
public class DistributedNetworkNode
{
    private readonly List<string> childNodes = new();
    private readonly List<string> parentNodes = new();
    private int branchLevel = 0;
    
    public async Task JoinDistributedNetworkAsync()
    {
        // Enviar AcceptChildren al servidor
        await client.AcceptChildrenAsync(true);
        
        // Recibir PossibleParents del servidor
        // Conectar a padres
        // Aceptar hijos
    }
    
    public async Task PropagateSearchAsync(string query, uint token)
    {
        // Enviar búsqueda a nodos hijos
        foreach (var child in childNodes)
        {
            await client.SendDistributedSearchAsync(child, query, token);
        }
    }
}
```

**Beneficio:** Búsquedas más rápidas, menos carga en servidor central.

**Complejidad:** Alta - requiere gestión de conexiones P2P adicionales.

---

## 🔐 5. Ofuscación de Conexiones

### Protocolo: Obfuscation Types

```
0 = No obfuscation
1 = Obfuscated connection
```

### Concepto
Algunos ISPs bloquean tráfico P2P. La ofuscación ayuda a evitar esto.

### Implementación

```csharp
// Verificar si Soulseek.NET soporta ofuscación
var options = new SoulseekClientOptions
{
    EnableConnectionObfuscation = true,  // Si existe
    // ...
};
```

**Beneficio:** Evita bloqueos de ISP en tráfico P2P.

---

## 📝 6. Mensajes de Usuario Mejorados

### Protocolo: MessageUser (Server Code 22)

```
Send: string username, string message
Receive: uint32 message_id, uint32 timestamp, string username, string message, bool is_admin
```

### Mejora Sugerida

```csharp
// UI/ChatManager.cs
public class ChatManager
{
    private readonly Dictionary<string, List<ChatMessage>> conversations = new();
    
    public class ChatMessage
    {
        public uint MessageId { get; set; }
        public DateTime Timestamp { get; set; }
        public string Username { get; set; }
        public string Message { get; set; }
        public bool IsAdmin { get; set; }
        public bool IsRead { get; set; }
    }
    
    public async Task SendMessageAsync(string username, string message)
    {
        await client.SendPrivateMessageAsync(username, message);
        
        // Guardar en historial
        AddToConversation(username, new ChatMessage
        {
            Timestamp = DateTime.Now,
            Username = "Me",
            Message = message
        });
    }
    
    public void OnMessageReceived(string username, string message, uint messageId)
    {
        AddToConversation(username, new ChatMessage
        {
            MessageId = messageId,
            Timestamp = DateTime.Now,
            Username = username,
            Message = message,
            IsRead = false
        });
        
        // Notificar UI
        ShowNotification($"Mensaje de {username}", message);
    }
}
```

**UI:**
- Pestaña "Mensajes" con conversaciones
- Notificaciones de mensajes nuevos
- Historial persistente

**Beneficio:** Comunicación con otros usuarios (pedir reuploads, agradecer, etc.)

---

## 🎯 7. Recomendaciones y Similares

### Protocolo: Recommendations

**Server Code 54:** `Recommendations`
```
Receive: list of (string recommendation, int32 score)
```

**Server Code 50:** `SimilarRecommendations`
```
Send: string item
Receive: list of (string recommendation, int32 score)
```

### Implementación

```csharp
// Services/RecommendationService.cs
public class RecommendationService
{
    public async Task<List<Recommendation>> GetRecommendationsAsync()
    {
        // Obtener recomendaciones del servidor basadas en descargas
        var recommendations = await client.GetRecommendationsAsync();
        return recommendations;
    }
    
    public async Task<List<string>> GetSimilarArtistsAsync(string artist)
    {
        // Obtener artistas similares
        var similar = await client.GetSimilarRecommendationsAsync(artist);
        return similar;
    }
}
```

**UI:**
- Sección "Descubre" con recomendaciones
- "Artistas similares" en búsqueda de autores

**Beneficio:** Descubrimiento de contenido nuevo.

---

## 📊 8. Estadísticas de Usuario

### Protocolo: GetUserStats (Server Code 36)

```
Send: string username
Receive: string username, uint32 avg_speed, uint64 uploads, uint32 shared_file_count, uint32 shared_folder_count
```

### Implementación

```csharp
// Models/UserStats.cs
public class UserStats
{
    public string Username { get; set; }
    public int AverageSpeed { get; set; }      // KB/s
    public long TotalUploads { get; set; }
    public int SharedFileCount { get; set; }
    public int SharedFolderCount { get; set; }
    
    public string SpeedDescription => $"{AverageSpeed} KB/s promedio";
    public string LibrarySize => $"{SharedFileCount:N0} archivos en {SharedFolderCount} carpetas";
}

// Usar en búsqueda para priorizar usuarios con buenas stats
public int CalculateUserScore(UserStats stats)
{
    int score = 0;
    
    // Velocidad alta = más puntos
    if (stats.AverageSpeed > 1000) score += 50;
    else if (stats.AverageSpeed > 500) score += 30;
    else if (stats.AverageSpeed > 100) score += 10;
    
    // Muchos uploads = usuario confiable
    if (stats.TotalUploads > 10000) score += 30;
    else if (stats.TotalUploads > 1000) score += 20;
    
    // Biblioteca grande = más variedad
    if (stats.SharedFileCount > 10000) score += 20;
    
    return score;
}
```

**Beneficio:** Priorizar descargas de usuarios rápidos y confiables.

---

## 🏆 Priorización de Ideas

### Alta Prioridad (Implementar Pronto)
1. **Metadatos de audio mejorados** (#1) - Mejora calidad de búsquedas
2. **Sistema de posición en cola** (#2) - Mejor UX
3. **Estadísticas de usuario** (#8) - Priorización inteligente

### Media Prioridad (Considerar)
4. **QueueUpload correcto** (#3) - Compatibilidad
5. **Mensajes de usuario** (#6) - Comunicación

### Baja Prioridad (Futuro)
6. **Red distribuida** (#4) - Complejo, beneficio marginal
7. **Ofuscación** (#5) - Solo si hay problemas de ISP
8. **Recomendaciones** (#7) - Nice to have

---

## 💡 Implementación Rápida Sugerida

### Mejora Inmediata: Metadatos de Audio

```csharp
// En resultados de búsqueda, mostrar calidad de audio
public string GetAudioQualityBadge(File file)
{
    var attrs = file.Attributes;
    
    // FLAC/Lossless
    if (attrs.ContainsKey(4) && attrs.ContainsKey(5))
    {
        return $"🎵 {attrs[4]/1000}kHz/{attrs[5]}bit";
    }
    
    // MP3/Lossy
    if (attrs.ContainsKey(0))
    {
        var vbr = attrs.ContainsKey(2) && attrs[2] == 1 ? " VBR" : "";
        return $"🎵 {attrs[0]}kbps{vbr}";
    }
    
    return "";
}
```

**En UI de búsqueda:**
```
[📄] song.mp3 - 5.2 MB - 🎵 320kbps - user123
[📄] song.flac - 28.4 MB - 🎵 44.1kHz/16bit - audiophile99
```

---

## 📚 Referencias

- **Protocolo Soulseek:** https://nicotine-plus.org/doc/SLSKPROTOCOL.html
- **Código Nicotine+:** https://github.com/nicotine-plus/nicotine-plus
- **Documentación Desarrollo:** https://nicotine-plus.org/doc/DEVELOPING.html

---

## ✅ Conclusión

Hay **muchas más ideas** del protocolo Soulseek y Nicotine+ que pueden mejorar SlskDown:

- **Corto plazo:** Metadatos de audio, posición en cola, stats de usuario
- **Medio plazo:** Mensajes, QueueUpload correcto
- **Largo plazo:** Red distribuida, ofuscación, recomendaciones

**Recomendación:** Empezar con metadatos de audio (#1) ya que es fácil de implementar y mejora significativamente la experiencia de búsqueda.
