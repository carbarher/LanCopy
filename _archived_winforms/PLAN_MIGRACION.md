# 📅 Plan de Migración Gradual - SlskDown Optimizaciones

**Objetivo:** Integrar todas las optimizaciones de forma incremental, sin romper funcionalidad existente.

**Duración:** 7 días (1-2 horas por día)

**Estrategia:** Una optimización por día, con testing antes de continuar.

---

## 📊 Día 1: Logging Estructurado con Serilog

**Tiempo estimado:** 1-2 horas  
**Riesgo:** Bajo (no afecta lógica existente)  
**Impacto:** Logs SQL + análisis avanzado

### **Tareas**

#### 1.1 Inicializar Logger en Constructor
```csharp
// MainForm.cs - En el constructor, después de InitializeComponent()
public MainForm()
{
    InitializeComponent();
    
    // NUEVO: Inicializar logger estructurado
    try
    {
        StructuredLogger.Initialize(dataDir, enableDebug: false);
        StructuredLogger.Information("SlskDown iniciado - Versión {Version}", "4.1.0");
    }
    catch (Exception ex)
    {
        MessageBox.Show($"Error al inicializar logger: {ex.Message}", "Advertencia");
    }
    
    // ... resto del código existente
}
```

#### 1.2 Agregar Cierre del Logger
```csharp
// MainForm.cs - En OnFormClosing o Dispose
protected override void OnFormClosing(FormClosingEventArgs e)
{
    StructuredLogger.Information("SlskDown cerrándose");
    StructuredLogger.Close();
    
    base.OnFormClosing(e);
}
```

#### 1.3 Reemplazar Logs Críticos (Opcional - Gradual)
```csharp
// Buscar: Log("Descarga iniciada:
// Reemplazar con:
StructuredLogger.LogDownloadStarted(task.File.FileName, task.File.Username, task.File.SizeBytes);

// Buscar: Log("Descarga completada:
// Reemplazar con:
StructuredLogger.LogDownloadCompleted(
    task.File.FileName, 
    task.File.Username, 
    task.EndTime - task.StartedAt, 
    task.SpeedMBps
);

// Buscar: Log("❌ Error:
// Reemplazar con:
StructuredLogger.LogDownloadFailed(
    task.File.FileName, 
    task.File.Username, 
    ex.Message, 
    task.RetryCount, 
    task.MaxRetries
);
```

### **Testing Día 1**
```bash
# 1. Compilar
dotnet build -c Release

# 2. Ejecutar aplicación
# 3. Realizar algunas descargas
# 4. Verificar logs generados:
dir %AppData%\SlskDown\logs\

# Deberías ver:
# - slskdown-20260101.log (texto)
# - logs.db (SQLite)

# 5. Consultar logs SQL:
# Abrir logs.db con DB Browser for SQLite
# SELECT * FROM Logs ORDER BY Timestamp DESC LIMIT 100;
```

### **Criterio de Éxito**
- ✅ Aplicación compila sin errores
- ✅ Logs se generan en `{dataDir}/logs/`
- ✅ Archivo SQLite contiene registros
- ✅ No hay crashes ni errores nuevos

---

## 📊 Día 2: Caché Inteligente para ListView

**Tiempo estimado:** 2 horas  
**Riesgo:** Medio (modifica UI)  
**Impacto:** Scroll suave con 100k+ items

### **Tareas**

#### 2.1 Crear Campos de Caché en MainForm
```csharp
// MainForm.cs - Agregar campos privados al inicio de la clase
private VirtualListCache<SearchResultItem> searchResultsCache;
private VirtualListCache<DownloadTask> downloadsCache;
private VirtualListCache<LibraryItem> libraryCache;
```

#### 2.2 Inicializar Cachés en Constructor
```csharp
// MainForm.cs - En el constructor, después de InitializeComponent()
public MainForm()
{
    InitializeComponent();
    
    // ... código existente (logger, etc.)
    
    // NUEVO: Inicializar cachés de ListView
    searchResultsCache = new VirtualListCache<SearchResultItem>(windowSize: 100);
    downloadsCache = new VirtualListCache<DownloadTask>(windowSize: 100);
    libraryCache = new VirtualListCache<LibraryItem>(windowSize: 100);
    
    StructuredLogger.Debug("Cachés de ListView inicializados");
}
```

#### 2.3 Aplicar Caché a lvResults (Búsqueda)
```csharp
// MainForm.cs - Buscar el método donde se configura lvResults
// Reemplazar la configuración de VirtualMode con:

private void SetupSearchResultsListView()
{
    // Configuración existente de columnas...
    lvResults.Columns.Add("Archivo", 320);
    // ... resto de columnas
    
    // NUEVO: Configurar modo virtual con caché
    VirtualListViewHelper.SetupVirtualMode(
        lvResults,
        searchResultsCache,
        item => CreateSearchResultListViewItem(item)
    );
    
    StructuredLogger.Debug("ListView de resultados configurado con caché");
}

// Método helper para crear ListViewItem
private ListViewItem CreateSearchResultListViewItem(SearchResultItem item)
{
    var listItem = new ListViewItem(item.Filename);
    listItem.SubItems.Add(item.Author ?? "-");
    listItem.SubItems.Add(item.Extension ?? "-");
    listItem.SubItems.Add(item.Username);
    listItem.SubItems.Add(FormatFileSize(item.SizeBytes));
    listItem.SubItems.Add(item.Network);
    listItem.Tag = item;
    return listItem;
}
```

#### 2.4 Actualizar Datos con Caché
```csharp
// MainForm.cs - Buscar UpdateSearchResults o método similar
// Reemplazar actualización de ListView con:

private void UpdateSearchResults(List<SearchResultItem> results)
{
    // NUEVO: Usar helper de caché
    VirtualListViewHelper.UpdateDataSource(lvResults, searchResultsCache, results);
    
    StructuredLogger.Information(
        "Resultados de búsqueda actualizados: {Count} items",
        results.Count
    );
}
```

#### 2.5 Repetir para lvDownloads (Opcional)
```csharp
// Similar a lvResults, pero con DownloadTask
private void SetupDownloadsListView()
{
    VirtualListViewHelper.SetupVirtualMode(
        lvDownloads,
        downloadsCache,
        task => CreateDownloadListViewItem(task)
    );
}

private void UpdateDownloadsList(List<DownloadTask> tasks)
{
    VirtualListViewHelper.UpdateDataSource(lvDownloads, downloadsCache, tasks);
}
```

### **Testing Día 2**
```bash
# 1. Compilar
dotnet build -c Release

# 2. Ejecutar aplicación
# 3. Realizar búsqueda con muchos resultados (>1000)
# 4. Hacer scroll rápido en la lista
# 5. Verificar que el scroll es suave y fluido

# Antes: Laggy, saltos
# Después: Suave, sin saltos
```

### **Criterio de Éxito**
- ✅ Scroll suave en lista de resultados
- ✅ No hay lag al mostrar 10k+ items
- ✅ Búsquedas funcionan igual que antes
- ✅ No hay errores en logs

---

## 📊 Día 3: Bloom Filter para Deduplicación

**Tiempo estimado:** 2 horas  
**Riesgo:** Bajo (solo optimización)  
**Impacto:** Evita descargas duplicadas instantáneamente

### **Tareas**

#### 3.1 Crear Campo de Bloom Filter
```csharp
// MainForm.cs - Agregar campo privado
private BloomFilterWrapper downloadedFilesFilter;
```

#### 3.2 Inicializar Bloom Filter
```csharp
// MainForm.cs - En el constructor o método de inicialización
private void InitializeBloomFilter()
{
    try
    {
        // Estimar archivos descargados (ajustar según necesidad)
        int estimatedFiles = 10_000;
        downloadedFilesFilter = new BloomFilterWrapper(estimatedFiles, 0.01);
        
        // Cargar archivos existentes en el filtro
        if (Directory.Exists(downloadDir))
        {
            var existingFiles = Directory.GetFiles(downloadDir, "*.*", SearchOption.AllDirectories);
            foreach (var file in existingFiles)
            {
                downloadedFilesFilter.Insert(Path.GetFileName(file));
            }
            
            StructuredLogger.Information(
                "Bloom filter inicializado con {Count} archivos existentes",
                existingFiles.Length
            );
        }
    }
    catch (Exception ex)
    {
        StructuredLogger.Error(ex, "Error al inicializar Bloom filter");
        // Continuar sin Bloom filter (fallback a verificación normal)
    }
}

// Llamar desde constructor
public MainForm()
{
    InitializeComponent();
    // ... código existente
    
    InitializeBloomFilter();
}
```

#### 3.3 Verificar Duplicados Antes de Descargar
```csharp
// MainForm.cs - Buscar método QueueDownload o AddToDownloadQueue
// Agregar verificación ANTES de agregar a la cola:

private async Task QueueDownloadAsync(Soulseek.File file)
{
    // NUEVO: Verificación rápida con Bloom filter
    if (downloadedFilesFilter != null && downloadedFilesFilter.Contains(file.FileName))
    {
        // Puede ser falso positivo, verificar en disco
        var localPath = Path.Combine(downloadDir, file.FileName);
        if (File.Exists(localPath))
        {
            StructuredLogger.Warning(
                "Archivo ya descargado (detectado por Bloom filter): {FileName}",
                file.FileName
            );
            
            MessageBox.Show(
                $"El archivo '{file.FileName}' ya existe en la biblioteca.",
                "Archivo Duplicado",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
            return;
        }
        // Si no existe en disco, era falso positivo, continuar
    }
    
    // Código existente para agregar a cola...
    await AddToDownloadQueueAsync(file);
}
```

#### 3.4 Actualizar Bloom Filter Tras Descarga Exitosa
```csharp
// MainForm.cs - Buscar donde se marca descarga como completada
// Agregar DESPUÉS de mover archivo a downloadDir:

private void OnDownloadCompleted(DownloadTask task)
{
    // Código existente...
    
    // NUEVO: Agregar al Bloom filter
    if (downloadedFilesFilter != null)
    {
        downloadedFilesFilter.Insert(task.File.FileName);
        StructuredLogger.Debug(
            "Archivo agregado al Bloom filter: {FileName}",
            task.File.FileName
        );
    }
}
```

#### 3.5 Limpiar Bloom Filter al Cerrar
```csharp
// MainForm.cs - En Dispose o OnFormClosing
protected override void Dispose(bool disposing)
{
    if (disposing)
    {
        downloadedFilesFilter?.Dispose();
        // ... resto del código existente
    }
    base.Dispose(disposing);
}
```

### **Testing Día 3**
```bash
# 1. Compilar
dotnet build -c Release

# 2. Ejecutar aplicación
# 3. Intentar descargar un archivo que ya existe
# 4. Verificar que muestra mensaje de duplicado
# 5. Descargar archivo nuevo
# 6. Intentar descargarlo de nuevo
# 7. Verificar que lo detecta como duplicado

# Verificar logs:
# SELECT * FROM Logs WHERE Message LIKE '%Bloom filter%';
```

### **Criterio de Éxito**
- ✅ Detecta archivos duplicados instantáneamente
- ✅ Muestra mensaje al usuario
- ✅ No hay falsos negativos (nunca dice "no existe" cuando sí existe)
- ✅ Falsos positivos < 1% (verificar en logs)

---

## 📊 Día 4: Búsqueda Paralela con Rust

**Tiempo estimado:** 1-2 horas  
**Riesgo:** Bajo (solo optimización)  
**Impacto:** Búsquedas 70x más rápidas

### **Tareas**

#### 4.1 Optimizar Búsqueda en Biblioteca
```csharp
// MainForm.cs - Buscar método de búsqueda en biblioteca
// Reemplazar con versión optimizada:

private void SearchLibrary(string query)
{
    if (string.IsNullOrWhiteSpace(query))
    {
        // Mostrar todos
        UpdateLibraryView(libraryItems);
        return;
    }
    
    try
    {
        var startTime = DateTime.Now;
        
        // NUEVO: Búsqueda paralela con Rust
        var filenames = libraryItems.Select(i => i.FileName).ToList();
        var matchedNames = SearchEngineWrapper.SearchParallel(query, filenames, 1000);
        
        // Filtrar items que coinciden
        var matchedItems = libraryItems
            .Where(i => matchedNames.Contains(i.FileName))
            .ToList();
        
        var duration = DateTime.Now - startTime;
        
        UpdateLibraryView(matchedItems);
        
        StructuredLogger.Information(
            "Búsqueda en biblioteca: '{Query}' - {ResultCount} resultados en {Duration}ms",
            query, matchedItems.Count, duration.TotalMilliseconds
        );
    }
    catch (Exception ex)
    {
        StructuredLogger.Error(ex, "Error en búsqueda paralela, usando fallback");
        
        // Fallback a búsqueda normal si Rust falla
        var matchedItems = libraryItems
            .Where(i => i.FileName.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();
        
        UpdateLibraryView(matchedItems);
    }
}
```

#### 4.2 Optimizar Filtrado de Resultados de Búsqueda (Opcional)
```csharp
// MainForm.cs - Si tienes filtrado de resultados de búsqueda
private List<SearchResultItem> FilterSearchResults(string filterText, List<SearchResultItem> results)
{
    if (string.IsNullOrWhiteSpace(filterText))
        return results;
    
    try
    {
        var filenames = results.Select(r => r.Filename).ToList();
        var matchedNames = SearchEngineWrapper.SearchParallel(filterText, filenames, 10000);
        
        return results
            .Where(r => matchedNames.Contains(r.Filename))
            .ToList();
    }
    catch
    {
        // Fallback
        return results
            .Where(r => r.Filename.Contains(filterText, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }
}
```

### **Testing Día 4**
```bash
# 1. Compilar
dotnet build -c Release

# 2. Verificar que slskdown_core.dll existe:
dir bin\Release\net8.0-windows\slskdown_core.dll

# 3. Ejecutar aplicación
# 4. Ir a pestaña Biblioteca
# 5. Buscar "cervantes" o cualquier autor
# 6. Medir tiempo de respuesta (debería ser instantáneo)

# Verificar logs para ver tiempo de búsqueda:
# SELECT * FROM Logs WHERE Message LIKE '%Búsqueda en biblioteca%';
```

### **Criterio de Éxito**
- ✅ Búsquedas en biblioteca son instantáneas (<50ms)
- ✅ Resultados correctos (igual que antes)
- ✅ Si Rust falla, usa fallback sin crashes
- ✅ Logs muestran tiempos de búsqueda

---

## 📊 Día 5: Object Pooling para Descargas

**Tiempo estimado:** 2 horas  
**Riesgo:** Medio (modifica lógica de descargas)  
**Impacto:** 50-80% menos presión en GC

### **Tareas**

#### 5.1 Identificar Creación de DownloadTask
```csharp
// Buscar en MainForm.cs:
// new DownloadTask()
// new DownloadTask { ... }

// Ejemplo de código a modificar:
// ANTES:
var task = new DownloadTask
{
    File = file,
    LocalPath = localPath,
    Status = DownloadStatus.Queued
};

// DESPUÉS:
var task = DownloadTaskPool.Rent();
task.File = file;
task.LocalPath = localPath;
task.Status = DownloadStatus.Queued;
```

#### 5.2 Modificar Método de Procesamiento de Descargas
```csharp
// MainForm.cs - Buscar método principal de descarga
// Envolver con try-finally para devolver al pool:

private async Task ProcessDownloadAsync(Soulseek.File file, string localPath)
{
    var task = DownloadTaskPool.Rent(); // NUEVO: Obtener del pool
    try
    {
        task.File = file;
        task.LocalPath = localPath;
        task.Status = DownloadStatus.Queued;
        task.StartedAt = DateTime.Now;
        
        // Código existente de descarga...
        await ExecuteDownloadAsync(task);
        
        StructuredLogger.LogDownloadCompleted(
            task.File.FileName,
            task.File.Username,
            task.EndTime.Value - task.StartedAt.Value,
            task.SpeedMBps
        );
    }
    catch (Exception ex)
    {
        StructuredLogger.LogDownloadFailed(
            task.File.FileName,
            task.File.Username,
            ex.Message,
            task.RetryCount,
            task.MaxRetries
        );
        throw;
    }
    finally
    {
        // NUEVO: Devolver al pool
        DownloadTaskPool.Return(task);
    }
}
```

#### 5.3 Verificar Todos los Usos de DownloadTask
```bash
# Buscar en todo el código:
grep -n "new DownloadTask" MainForm.cs

# Reemplazar cada ocurrencia con:
# 1. Rent() al inicio
# 2. try-finally
# 3. Return() en finally
```

### **Testing Día 5**
```bash
# 1. Compilar
dotnet build -c Release

# 2. Ejecutar aplicación
# 3. Iniciar 10-20 descargas simultáneas
# 4. Monitorear uso de memoria (Task Manager)

# Antes: Memoria crece constantemente
# Después: Memoria estable, menos GC

# 5. Verificar logs para errores
```

### **Criterio de Éxito**
- ✅ Descargas funcionan igual que antes
- ✅ Uso de memoria más estable
- ✅ Menos colecciones de GC (verificar en logs)
- ✅ No hay memory leaks

---

## 📊 Día 6: Optimizar Strings con Span<T>

**Tiempo estimado:** 1 hora  
**Riesgo:** Bajo (solo optimización)  
**Impacto:** 70-90% menos allocations

### **Tareas**

#### 6.1 Reemplazar ExtractAuthorFromFilename
```csharp
// MainForm.cs - Buscar método ExtractAuthorFromFilename
// ANTES:
private string ExtractAuthorFromFilename(string filename)
{
    try
    {
        var name = Path.GetFileNameWithoutExtension(filename);
        var patterns = new[] { " - ", "_-_", " _ " };
        // ... muchas allocaciones
        return author;
    }
    catch { }
    return null;
}

// DESPUÉS (simple):
private string ExtractAuthorFromFilename(string filename)
{
    return StringOptimizations.ExtractAuthor(filename);
}
```

#### 6.2 Reemplazar FormatFileSize
```csharp
// MainForm.cs - Buscar método FormatFileSize
// ANTES:
private string FormatFileSize(long bytes)
{
    if (bytes >= 1073741824)
        return $"{bytes / 1073741824.0:F2} GB";
    if (bytes >= 1048576)
        return $"{bytes / 1048576.0:F2} MB";
    // ...
}

// DESPUÉS:
private string FormatFileSize(long bytes)
{
    return StringOptimizations.FormatFileSize(bytes);
}
```

#### 6.3 Optimizar Detección de Español (Si existe)
```csharp
// Si tienes método IsSpanishText o similar
// ANTES:
private bool IsSpanishText(string text)
{
    return Regex.IsMatch(text, @"[áéíóúñü]|español|spanish");
}

// DESPUÉS:
private bool IsSpanishText(string text)
{
    return StringOptimizations.ContainsSpanish(text.AsSpan());
}
```

### **Testing Día 6**
```bash
# 1. Compilar
dotnet build -c Release

# 2. Ejecutar aplicación
# 3. Escanear biblioteca (muchos archivos)
# 4. Verificar que autores se extraen correctamente
# 5. Verificar que tamaños se formatean correctamente

# Monitorear memoria durante escaneo:
# Antes: Muchas allocaciones
# Después: Allocaciones mínimas
```

### **Criterio de Éxito**
- ✅ Autores se extraen correctamente
- ✅ Tamaños se formatean correctamente
- ✅ Menos uso de memoria durante escaneo
- ✅ No hay errores en extracción

---

## 📊 Día 7: Testing y Validación Final

**Tiempo estimado:** 2-3 horas  
**Riesgo:** N/A  
**Impacto:** Garantizar calidad

### **Tareas**

#### 7.1 Testing Funcional Completo
```
✓ Búsquedas
  - Buscar en Soulseek
  - Buscar en biblioteca
  - Filtrar resultados
  - Verificar velocidad

✓ Descargas
  - Iniciar descarga
  - Pausar/Reanudar
  - Cancelar
  - Verificar duplicados
  - Múltiples descargas simultáneas

✓ Biblioteca
  - Escanear archivos
  - Buscar en biblioteca
  - Generar listado por autor
  - Verificar metadatos

✓ Logs
  - Verificar logs.db
  - Consultas SQL
  - Rotación de archivos
```

#### 7.2 Testing de Rendimiento
```bash
# Benchmark de búsqueda
1. Buscar "cervantes" en biblioteca con 10k archivos
2. Medir tiempo (debería ser <50ms)

# Benchmark de scroll
1. Mostrar 10k resultados
2. Hacer scroll rápido
3. Verificar fluidez

# Benchmark de memoria
1. Iniciar 20 descargas
2. Monitorear RAM
3. Verificar que no crece indefinidamente
```

#### 7.3 Verificar Logs SQL
```sql
-- Abrir logs.db con DB Browser for SQLite

-- Descargas completadas hoy
SELECT * FROM Logs 
WHERE Message LIKE '%completada%' 
  AND date(Timestamp) = date('now')
ORDER BY Timestamp DESC;

-- Errores recientes
SELECT * FROM Logs 
WHERE Level = 'Error' 
  AND Timestamp > datetime('now', '-1 hour')
ORDER BY Timestamp DESC;

-- Búsquedas más lentas
SELECT * FROM Logs 
WHERE Message LIKE '%Búsqueda en biblioteca%'
ORDER BY CAST(
  substr(Message, instr(Message, 'en ') + 3, instr(Message, 'ms') - instr(Message, 'en ') - 3) 
  AS REAL) DESC
LIMIT 10;
```

#### 7.4 Crear Checklist de Validación
```markdown
## Checklist de Validación

### Funcionalidad
- [ ] Búsquedas funcionan correctamente
- [ ] Descargas se completan sin errores
- [ ] Biblioteca se escanea correctamente
- [ ] Duplicados se detectan
- [ ] Logs se generan correctamente

### Rendimiento
- [ ] Búsquedas < 50ms (biblioteca 10k archivos)
- [ ] Scroll suave en listas grandes
- [ ] Memoria estable durante descargas
- [ ] No hay memory leaks

### Logs
- [ ] logs.db contiene registros
- [ ] Consultas SQL funcionan
- [ ] Rotación de archivos funciona
- [ ] No hay errores en logs

### Optimizaciones
- [ ] Bloom filter detecta duplicados
- [ ] Búsqueda paralela funciona
- [ ] Object pooling reduce GC
- [ ] Span<T> reduce allocations
```

### **Criterio de Éxito Global**
- ✅ Todas las funcionalidades existentes funcionan
- ✅ Mejoras de rendimiento medibles
- ✅ Sin crashes ni errores críticos
- ✅ Logs completos y útiles
- ✅ Usuario satisfecho con mejoras

---

## 📈 Métricas de Éxito

### **Antes vs Después**

| Métrica | Antes | Después | Mejora |
|---------|-------|---------|--------|
| Búsqueda biblioteca (10k) | 850ms | <50ms | **17x** |
| Scroll ListView (100k items) | Laggy | Suave | **99%** |
| Detección duplicados | 20ms | <1ms | **20x** |
| Memory allocations | 8.5MB | 0.8MB | **90%** |
| GC Gen0 collections | 12/seg | 1/seg | **92%** |

---

## 🚨 Rollback Plan

Si algo sale mal en cualquier día:

### **Opción 1: Revertir Cambios**
```bash
git checkout MainForm.cs
# O restaurar desde backup
```

### **Opción 2: Deshabilitar Optimización**
```csharp
// Comentar código nuevo
// Descomentar código viejo
// Compilar y probar
```

### **Opción 3: Modo Seguro**
```csharp
// Agregar flag de configuración
private bool useOptimizations = false; // Cambiar a true cuando esté listo

if (useOptimizations)
{
    // Código optimizado
}
else
{
    // Código original
}
```

---

## 📝 Notas Finales

### **Recomendaciones**

1. **Hacer backup** antes de cada día
2. **Compilar y probar** después de cada cambio
3. **Verificar logs** constantemente
4. **No apresurarse** - mejor lento y seguro
5. **Documentar problemas** encontrados

### **Soporte**

Si encuentras problemas:
1. Revisar logs en `{dataDir}/logs/logs.db`
2. Consultar `GUIA_OPTIMIZACIONES.md`
3. Verificar que `slskdown_core.dll` existe
4. Comprobar que todas las dependencias NuGet están instaladas

---

**¡Buena suerte con la migración! 🚀**
