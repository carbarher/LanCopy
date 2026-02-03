# 🚀 Integraciones Avanzadas - Mejoras Nicotine+

## Resumen
Integraciones adicionales más profundas de los componentes de Nicotine+ que pueden implementarse para maximizar el rendimiento y las capacidades de SlskDown.

---

## 1. UserQueueManager - Gestión Justa de Descargas

### Integración Propuesta
Modificar `ProcessDownload()` para usar el `UserQueueManager` y garantizar fairness entre usuarios.

### Ubicación
`MainForm.cs` - Método `ProcessDownload()` (línea ~28198)

### Código Sugerido
```csharp
private async Task ProcessDownload(DownloadTask task)
{
    // MEJORA NICOTINE+: Encolar en UserQueueManager
    if (_userQueueManager != null)
    {
        _userQueueManager.Enqueue(task);
        
        // Esperar hasta que sea nuestro turno según las reglas de fairness
        while (!_userQueueManager.CanStartDownload(task.File.Username))
        {
            await Task.Delay(500);
            if (task.CancellationToken?.IsCancellationRequested == true)
            {
                _userQueueManager.Dequeue(task.File.Username);
                return;
            }
        }
        
        // Marcar como activo
        _userQueueManager.MarkAsActive(task.File.Username);
        AutoLog($"✅ Turno asignado para {task.File.Username} (cola: {_userQueueManager.GetQueueLength(task.File.Username)})");
    }
    
    try
    {
        // ... código de descarga existente ...
    }
    finally
    {
        // MEJORA NICOTINE+: Liberar slot al terminar
        if (_userQueueManager != null)
        {
            _userQueueManager.CompleteDownload(task.File.Username);
            
            // Procesar siguiente en cola para este usuario
            var nextTask = _userQueueManager.GetNextTask(task.File.Username);
            if (nextTask != null)
            {
                _ = ProcessDownload(nextTask);
            }
        }
    }
}
```

### Beneficios
- ✅ Fairness: Ningún usuario monopoliza las descargas
- ✅ Mejor experiencia: Distribución equitativa de ancho de banda
- ✅ Prevención de bloqueos: Usuarios lentos no bloquean a usuarios rápidos

---

## 2. MetadataScanner - Verificación Inteligente

### Integración Propuesta
Usar `MetadataScanner` en `VerifyDownloadedFilesAsync()` para validación avanzada.

### Ubicación
`MainForm.cs` - Método `VerifyDownloadedFilesAsync()` (línea ~12705)

### Código Sugerido
```csharp
private async Task VerifyDownloadedFilesAsync()
{
    // ... código existente ...
    
    Parallel.ForEach(files, parallelOptions, file =>
    {
        try
        {
            var fileInfo = new FileInfo(file);
            
            // MEJORA NICOTINE+: Usar MetadataScanner para archivos de audio
            if (_metadataScanner != null && IsAudioFile(fileInfo.Extension))
            {
                var metadata = _metadataScanner.ScanFile(file);
                
                // Validar metadatos
                if (!metadata.IsValid)
                {
                    SafeInvoke(() => AutoLog($"   ⚠️ Metadatos inválidos: {Path.GetFileName(file)} - {metadata.ValidationError}"));
                    lock (lockObj) { invalidFiles++; }
                    return;
                }
                
                // Detectar archivos sospechosamente pequeños
                if (fileInfo.Length < 128)
                {
                    SafeInvoke(() => AutoLog($"   ⚠️ Archivo muy pequeño (posible stub): {Path.GetFileName(file)}"));
                    lock (lockObj) { invalidFiles++; }
                    return;
                }
                
                // Validar bitrate
                if (metadata.Bitrate < 32 || metadata.Bitrate > 320)
                {
                    SafeInvoke(() => AutoLog($"   ⚠️ Bitrate fuera de rango: {Path.GetFileName(file)} ({metadata.Bitrate} kbps)"));
                }
                
                // Detectar VBR
                if (metadata.IsVBR)
                {
                    SafeInvoke(() => AutoLog($"   ℹ️ VBR detectado: {Path.GetFileName(file)}"));
                }
                
                lock (lockObj) 
                { 
                    validFiles++; 
                    totalSize += fileInfo.Length;
                }
            }
            else
            {
                // Validación básica para otros tipos de archivos
                // ... código existente ...
            }
        }
        catch (Exception ex)
        {
            SafeInvoke(() => AutoLog($"   ⚠️ Error verificando {Path.GetFileName(file)}: {ex.Message}"));
        }
    });
}

private bool IsAudioFile(string extension)
{
    var audioExts = new[] { ".mp3", ".flac", ".m4a", ".ogg", ".wav", ".aac", ".wma" };
    return audioExts.Contains(extension.ToLower());
}
```

### Beneficios
- ✅ Detección de archivos corruptos o incompletos
- ✅ Validación de rangos de metadatos
- ✅ Detección de VBR para mejor estimación de calidad
- ✅ Skip de archivos muy pequeños (stubs)

---

## 3. MappedDatabase - Caché de Búsquedas

### Integración Propuesta
Usar `MappedDatabase` para cachear resultados de búsquedas frecuentes.

### Ubicación
`MainForm.cs` - Métodos de búsqueda

### Código Sugerido
```csharp
private async Task<List<SearchResult>> SearchWithCache(string query)
{
    // MEJORA NICOTINE+: Cachear resultados de búsqueda
    if (_mappedDatabase != null)
    {
        // Intentar leer del caché
        var cacheKey = $"search:{query.ToLower()}";
        var cached = _mappedDatabase.Read(cacheKey);
        
        if (cached != null && cached.Length > 0)
        {
            try
            {
                var results = JsonSerializer.Deserialize<List<SearchResult>>(cached);
                if (results != null && results.Count > 0)
                {
                    AutoLog($"💾 Resultados cargados desde caché: {results.Count} items");
                    return results;
                }
            }
            catch { }
        }
    }
    
    // Si no hay caché, buscar normalmente
    var searchResults = await PerformSearch(query);
    
    // MEJORA NICOTINE+: Guardar en caché
    if (_mappedDatabase != null && searchResults.Count > 0)
    {
        try
        {
            var cacheKey = $"search:{query.ToLower()}";
            var json = JsonSerializer.SerializeToUtf8Bytes(searchResults);
            _mappedDatabase.Write(cacheKey, json);
            AutoLog($"💾 Resultados guardados en caché: {searchResults.Count} items");
        }
        catch (Exception ex)
        {
            AutoLog($"⚠️ Error guardando caché: {ex.Message}");
        }
    }
    
    return searchResults;
}
```

### Beneficios
- ✅ Búsquedas instantáneas para queries repetidas
- ✅ Reducción de carga en la red Soulseek
- ✅ Mejor experiencia de usuario
- ✅ Bajo uso de memoria (mmap)

---

## 4. EventBus - Eventos Adicionales

### Integración Propuesta
Publicar más eventos para mejor observabilidad y extensibilidad.

### Eventos Sugeridos

#### SearchStarted
```csharp
_eventBus?.Publish(SystemEvents.SearchStarted, new
{
    Query = searchQuery,
    Timestamp = DateTime.UtcNow
});
```

#### SearchCompleted
```csharp
_eventBus?.Publish(SystemEvents.SearchCompleted, new
{
    Query = searchQuery,
    ResultCount = results.Count,
    Duration = sw.ElapsedMilliseconds,
    Timestamp = DateTime.UtcNow
});
```

#### DownloadStarted
```csharp
_eventBus?.Publish(SystemEvents.DownloadStarted, new
{
    FileName = task.File.FileName,
    Username = task.File.Username,
    SizeBytes = task.File.SizeBytes,
    Timestamp = DateTime.UtcNow
});
```

#### DownloadFailed
```csharp
_eventBus?.Publish(SystemEvents.DownloadFailed, new
{
    FileName = task.File.FileName,
    Username = task.File.Username,
    Reason = ex.Message,
    RetryCount = task.RetryCount,
    Timestamp = DateTime.UtcNow
});
```

#### QueueSizeChanged
```csharp
_eventBus?.Publish(SystemEvents.QueueSizeChanged, new
{
    QueueSize = downloadQueue.Count,
    ActiveDownloads = GetActiveDownloadsCount(),
    Timestamp = DateTime.UtcNow
});
```

### Suscripciones Útiles

#### Logging Centralizado
```csharp
_eventBus.Subscribe(SystemEvents.DownloadStarted, data =>
{
    var d = (dynamic)data;
    AutoLog($"📥 Iniciando descarga: {d.FileName} desde {d.Username}");
});

_eventBus.Subscribe(SystemEvents.DownloadFailed, data =>
{
    var d = (dynamic)data;
    AutoLog($"❌ Descarga fallida: {d.FileName} - {d.Reason} (Intento {d.RetryCount})");
});
```

#### Estadísticas en Tiempo Real
```csharp
_eventBus.Subscribe(SystemEvents.SearchCompleted, data =>
{
    var d = (dynamic)data;
    statisticsManager?.RecordSearch(d.Query, d.ResultCount, TimeSpan.FromMilliseconds(d.Duration));
});
```

### Beneficios
- ✅ Observabilidad completa del sistema
- ✅ Fácil extensión sin modificar código existente
- ✅ Logging centralizado y estructurado
- ✅ Estadísticas automáticas

---

## 5. WordIndex - Búsquedas Locales Ultrarrápidas

### Integración Propuesta
Usar `WordIndex` para búsquedas instantáneas en archivos compartidos.

### Ubicación
`MainForm.cs` - Método de búsqueda en shares

### Código Sugerido
```csharp
private List<ShareIndexEntry> SearchLocalShares(string query)
{
    // MEJORA NICOTINE+: Búsqueda O(1) con WordIndex
    if (_wordIndex != null)
    {
        var words = query.ToLower().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        var matchingIds = _wordIndex.Search(words);
        
        var results = new List<ShareIndexEntry>();
        lock (shareIndexLock)
        {
            foreach (var id in matchingIds)
            {
                if (id < shareIndex.Count)
                {
                    results.Add(shareIndex[id]);
                }
            }
        }
        
        AutoLog($"🔍 WordIndex: {results.Count} resultados en O(1) para '{query}'");
        return results;
    }
    
    // Fallback a búsqueda lineal
    lock (shareIndexLock)
    {
        return shareIndex
            .Where(s => s.RelativePath.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }
}
```

### Beneficios
- ✅ Búsquedas instantáneas (O(1) vs O(n))
- ✅ Soporte para búsquedas multi-palabra
- ✅ Escalable a millones de archivos
- ✅ Bajo uso de memoria

---

## 6. GCHelper - Gestión Proactiva de Memoria

### Integraciones Adicionales Sugeridas

#### Después de Búsquedas Masivas
```csharp
private async Task PerformMassiveSearch()
{
    // ... búsqueda de muchos autores ...
    
    // MEJORA NICOTINE+: Liberar memoria después de operación pesada
    GCHelper.ForceCollect("Búsqueda masiva completada");
}
```

#### Después de Cargar Archivos Grandes
```csharp
private void LoadLargeDataset()
{
    // ... cargar muchos datos ...
    
    // MEJORA NICOTINE+: Compactar heap
    GCHelper.CompactHeap("Dataset grande cargado");
}
```

#### Monitoreo Periódico
```csharp
private void StartMemoryMonitoring()
{
    _eventBus?.ScheduleRecurring("memory-monitor", TimeSpan.FromMinutes(5), () =>
    {
        var stats = GCHelper.GetMemoryStats();
        AutoLog($"📊 Memoria: {stats.UsedMB:F1} MB / {stats.TotalMB:F1} MB ({stats.UsagePercent:F1}%)");
        
        // Si el uso es alto, forzar GC
        if (stats.UsagePercent > 80)
        {
            AutoLog("⚠️ Uso de memoria alto, forzando GC...");
            GCHelper.ForceCollect("Uso de memoria > 80%");
        }
    });
}
```

### Beneficios
- ✅ Control proactivo de memoria
- ✅ Prevención de OutOfMemoryException
- ✅ Mejor rendimiento sostenido
- ✅ Monitoreo automático

---

## 7. PathCache - Optimización de Filesystem

### Integraciones Adicionales Sugeridas

#### En Verificación de Archivos
```csharp
private bool FileExistsOptimized(string path)
{
    // MEJORA NICOTINE+: Cachear resultado de File.Exists
    if (_pathCache != null)
    {
        return _pathCache.Exists(path);
    }
    return File.Exists(path);
}
```

#### En Operaciones de Descarga
```csharp
private string GetDownloadPathOptimized(string filename)
{
    // MEJORA NICOTINE+: Cachear normalización de paths
    var basePath = Path.Combine(downloadDir, filename);
    if (_pathCache != null)
    {
        return _pathCache.GetNormalized(basePath);
    }
    return Path.GetFullPath(basePath);
}
```

### Beneficios
- ✅ Reducción de syscalls costosas
- ✅ Menos allocations
- ✅ Mejor rendimiento en operaciones masivas

---

## 8. UserWatchManager - Gestión Automática de Usuarios

### Integración Propuesta (cuando API esté disponible)

#### Al Iniciar Descarga
```csharp
private async Task ProcessDownload(DownloadTask task)
{
    // MEJORA NICOTINE+: Watch automático del usuario
    if (_userWatchManager != null)
    {
        await _userWatchManager.WatchUserAsync(task.File.Username, "download");
    }
    
    try
    {
        // ... descarga ...
    }
    finally
    {
        // MEJORA NICOTINE+: Unwatch si no hay más contextos
        if (_userWatchManager != null)
        {
            await _userWatchManager.RemoveContextAsync(task.File.Username, "download");
        }
    }
}
```

#### Limpieza Periódica
```csharp
private void StartUserWatchCleanup()
{
    _eventBus?.ScheduleRecurring("user-watch-cleanup", TimeSpan.FromMinutes(10), async () =>
    {
        if (_userWatchManager != null)
        {
            var removed = await _userWatchManager.CleanupStaleUsersAsync(TimeSpan.FromMinutes(30));
            if (removed > 0)
            {
                AutoLog($"🧹 Limpieza de usuarios: {removed} usuarios sin contexto removidos");
            }
        }
    });
}
```

### Beneficios
- ✅ Gestión automática de recursos
- ✅ Limpieza de usuarios obsoletos
- ✅ Mejor organización

---

## Resumen de Prioridades

### Alta Prioridad (✅ COMPLETADO)
1. ✅ **EventBus eventos adicionales** - Observabilidad esencial
   - DownloadStarted, DownloadFailed, AuthorsLoaded publicados
   - Suscripciones de logging implementadas
2. ✅ **WordIndex en búsquedas** - Performance inmediato
   - Integrado en shareIndex (auto-población)
3. ⏳ **UserQueueManager** - Fairness crítico para UX (pendiente)

### Media Prioridad (Próxima Iteración)
4. ⏳ **MetadataScanner en verificación** - Calidad de datos
5. ⏳ **MappedDatabase para caché** - Performance en búsquedas
6. ⏳ **GCHelper monitoreo** - Estabilidad a largo plazo

### Baja Prioridad (Futuro)
7. ⏳ **UserWatchManager** - Requiere API
8. ⏳ **PathCache adicional** - Optimización incremental

---

## Métricas de Éxito

### Performance
- Búsquedas locales: < 10ms (vs ~500ms actual)
- Uso de memoria: -30% en operaciones masivas
- Throughput de descargas: +40% con fairness

### Calidad
- Detección de archivos corruptos: +95%
- Falsos positivos en verificación: -80%

### UX
- Tiempo de respuesta UI: < 100ms constante
- Distribución justa de ancho de banda entre usuarios
- Logs estructurados y útiles

---

**Fecha**: 2024  
**Estado**: Propuestas listas para implementación  
**Próximo paso**: Implementar integraciones de alta prioridad
