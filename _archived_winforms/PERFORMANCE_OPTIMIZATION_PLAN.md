# ⚡ Plan de Optimización de Performance - SlskDown v4.2

## 🎯 Objetivo
Maximizar rendimiento usando SQLite + caché en memoria de forma inteligente.

---

## 📊 ESTRATEGIA: Sistema Híbrido

### Principio
- **Memoria (RAM)**: Operaciones frecuentes, datos recientes (últimas 1000 descargas)
- **SQLite (Disco)**: Almacenamiento permanente, búsquedas complejas, análisis histórico

---

## 🚀 FASE 1: Optimizar Historial de Descargas

### Problema Actual
```csharp
// ❌ LENTO: Lista en memoria sin límite
private List<DownloadHistory> downloadHistory = new List<DownloadHistory>(); // Puede crecer infinitamente
private HashSet<string> downloadHistoryCache = new HashSet<string>(); // Solo nombres, sin metadata

// Operaciones:
- Agregar: O(1) pero consume RAM
- Buscar: O(n) en lista, O(1) en HashSet (solo nombre)
- Persistir: Serializar TODO a JSON (lento con 10K+ items)
```

### Solución Optimizada
```csharp
// ✅ RÁPIDO: LRU Cache + SQLite
private LRUCache<string, DownloadHistory> recentDownloads; // Últimas 1000 en RAM
private SlskDatabase database; // Todo en SQLite

// Operaciones:
- Agregar: O(1) en cache + async write a SQLite
- Buscar reciente: O(1) en cache
- Buscar histórico: O(log n) en SQLite con índices
- Persistir: Automático en SQLite (no serialización)
```

---

## 🔧 IMPLEMENTACIÓN

### Paso 1: LRU Cache para Descargas Recientes
```csharp
// Agregar en MainForm.cs (línea ~1750)
private LRUCache<string, DownloadHistory> recentDownloads = new LRUCache<string, DownloadHistory>(1000);

// Reemplazar downloadHistory.Add() con:
private async Task AddToDownloadHistoryAsync(DownloadHistory item)
{
    // 1. Agregar a caché en memoria (instantáneo)
    recentDownloads.Add(item.FileName, item);
    
    // 2. Guardar en SQLite (async, no bloquea)
    _ = Task.Run(async () =>
    {
        try
        {
            if (database != null)
            {
                var record = new DownloadRecord
                {
                    FileName = item.FileName,
                    Author = item.Author,
                    Username = item.Username,
                    SizeBytes = item.SizeBytes,
                    Status = item.Status,
                    DownloadedAt = item.DownloadedAt,
                    Speed = item.SpeedMBps,
                    MD5Hash = item.Hash
                };
                await database.InsertDownloadAsync(record);
            }
        }
        catch (Exception ex)
        {
            AutoLog($"⚠️ Error guardando en DB: {ex.Message}");
        }
    });
}

// Verificar si ya fue descargado (híbrido)
private async Task<bool> WasAlreadyDownloadedAsync(string fileName)
{
    // 1. Buscar en caché (instantáneo)
    if (recentDownloads.TryGetValue(fileName, out _))
        return true;
    
    // 2. Buscar en SQLite (rápido con índices)
    if (database != null)
    {
        var downloads = await database.GetDownloadsByFileNameAsync(fileName);
        return downloads.Any();
    }
    
    return false;
}
```

---

### Paso 2: Índices en SQLite
```sql
-- Ya implementado en SlskDatabase.cs
CREATE INDEX IF NOT EXISTS idx_downloads_filename ON Downloads(FileName);
CREATE INDEX IF NOT EXISTS idx_downloads_author ON Downloads(Author);
CREATE INDEX IF NOT EXISTS idx_downloads_date ON Downloads(DownloadedAt);
CREATE INDEX IF NOT EXISTS idx_downloads_md5 ON Downloads(MD5Hash);

-- Búsquedas ahora son O(log n) en lugar de O(n)
```

---

### Paso 3: Caché de Provider Stats
```csharp
// Problema: providerStats en memoria se pierde al cerrar
// Solución: Persistir en SQLite

private async Task UpdateProviderStatsOptimizedAsync(string username, bool success, double speed, long bytes)
{
    // 1. Actualizar en memoria (instantáneo)
    lock (providerStatsLock)
    {
        if (!providerStats.ContainsKey(username))
            providerStats[username] = new ProviderStats { Username = username };
        
        var stats = providerStats[username];
        stats.TotalDownloads++;
        if (success) stats.SuccessfulDownloads++;
        else stats.FailedDownloads++;
        stats.AverageSpeed = (stats.AverageSpeed * (stats.TotalDownloads - 1) + speed) / stats.TotalDownloads;
        stats.TotalBytesDownloaded += bytes;
        stats.LastDownload = DateTime.UtcNow;
    }
    
    // 2. Guardar en SQLite (async, cada 10 actualizaciones)
    if (providerStats[username].TotalDownloads % 10 == 0)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                var rating = new SourceRating
                {
                    Username = username,
                    AverageSpeed = providerStats[username].AverageSpeed,
                    SuccessRate = providerStats[username].SuccessRate,
                    TotalDownloads = providerStats[username].TotalDownloads,
                    FailedDownloads = providerStats[username].FailedDownloads,
                    LastSeen = DateTime.UtcNow
                };
                await database.UpsertSourceRatingAsync(rating);
            }
            catch { }
        });
    }
}
```

---

## 📈 MEJORAS DE RENDIMIENTO ESPERADAS

| Operación | Antes | Después | Mejora |
|-----------|-------|---------|--------|
| Agregar descarga | 1ms + 50ms (JSON) | 0.1ms + async | **50x** |
| Verificar duplicado | 10ms (lista) | 0.01ms (cache) | **1000x** |
| Buscar por autor | 100ms (JSON) | 5ms (SQLite) | **20x** |
| Cargar historial | 2s (10K items) | 50ms (índices) | **40x** |
| Uso de RAM | 50MB (10K items) | 5MB (1K cache) | **10x menos** |

---

## 🎯 FASE 2: Optimizar Búsquedas

### SearchResultsDatabase (ya implementado)
```csharp
// Para búsquedas con >10K resultados
- Guardar en SQLite en lugar de memoria
- Virtual ListView para mostrar millones de items
- Paginación automática
```

### Caché de Búsquedas
```csharp
// Guardar resultados de búsquedas frecuentes
private async Task<List<SearchResult>> SearchWithCacheAsync(string query)
{
    // 1. Verificar caché (5 minutos)
    var cached = await database.GetCachedSearchResultsAsync(query);
    if (cached.Any())
    {
        AutoLog($"💾 Resultados desde caché: {cached.Count}");
        return cached;
    }
    
    // 2. Buscar en red
    var results = await PerformSearchAsync(query);
    
    // 3. Guardar en caché
    await database.CacheSearchResultsAsync(query, results);
    
    return results;
}
```

---

## 🔥 FASE 3: Optimizaciones Avanzadas

### 3.1 Connection Pooling SQLite
```csharp
// SlskDatabase.cs - usar pool de conexiones
private static readonly SemaphoreSlim connectionSemaphore = new SemaphoreSlim(5, 5);

public async Task<T> ExecuteWithPoolAsync<T>(Func<SqliteConnection, Task<T>> action)
{
    await connectionSemaphore.WaitAsync();
    try
    {
        return await action(connection);
    }
    finally
    {
        connectionSemaphore.Release();
    }
}
```

### 3.2 Batch Inserts
```csharp
// En lugar de insertar 1 por 1, hacer lotes
private List<DownloadRecord> pendingInserts = new List<DownloadRecord>();

private async Task FlushPendingInsertsAsync()
{
    if (pendingInserts.Count == 0) return;
    
    using var transaction = connection.BeginTransaction();
    foreach (var record in pendingInserts)
    {
        await database.InsertDownloadAsync(record);
    }
    transaction.Commit();
    pendingInserts.Clear();
}

// Llamar cada 100 inserts o cada 5 segundos
```

### 3.3 Índices Compuestos
```sql
-- Para búsquedas complejas
CREATE INDEX IF NOT EXISTS idx_downloads_author_date 
    ON Downloads(Author, DownloadedAt DESC);

CREATE INDEX IF NOT EXISTS idx_downloads_status_date 
    ON Downloads(Status, DownloadedAt DESC);

-- Ahora búsquedas como "descargas de Asimov en último mes" son instantáneas
```

---

## 📊 MÉTRICAS A MONITOREAR

```csharp
// Agregar en ShowPerformanceStats()
stats.AppendLine("💾 SQLITE PERFORMANCE:");
stats.AppendLine($"   • Cache hits: {cacheHits:N0} ({cacheHitRate:F1}%)");
stats.AppendLine($"   • DB queries: {dbQueries:N0}");
stats.AppendLine($"   • Avg query time: {avgQueryTime:F2}ms");
stats.AppendLine($"   • DB size: {FormatFileSize(dbSize)}");
stats.AppendLine($"   • Items in cache: {recentDownloads.Count}/1000");
```

---

## ✅ CHECKLIST DE IMPLEMENTACIÓN

### Día 1 (HOY)
- [ ] Implementar LRUCache para downloadHistory
- [ ] Reemplazar downloadHistory.Add() con AddToDownloadHistoryAsync()
- [ ] Agregar método WasAlreadyDownloadedAsync()
- [ ] Optimizar UpdateProviderStats con batch writes
- [ ] Agregar métricas de performance

### Día 2
- [ ] Implementar caché de búsquedas
- [ ] Connection pooling en SlskDatabase
- [ ] Batch inserts para operaciones masivas
- [ ] Índices compuestos adicionales

### Día 3
- [ ] Tests de carga (10K, 100K descargas)
- [ ] Optimizar queries lentas
- [ ] Documentación de performance
- [ ] Cleanup de código antiguo

---

## 🎉 RESULTADO FINAL

**Antes:**
- 10K descargas = 50MB RAM + 2s carga + búsquedas lentas
- JSON serialization cada cambio (lento)
- Sin análisis histórico eficiente

**Después:**
- 10K descargas = 5MB RAM + 50ms carga + búsquedas instantáneas
- SQLite con índices (rápido)
- Análisis avanzado disponible
- **Performance 10-50x mejor**

---

**¿Empezamos con Día 1?** 🚀
