# 🗄️ Guía de Base de Datos SQLite - SlskDown

## Descripción General

SlskDown ahora utiliza **SQLite** como base de datos principal para almacenar:
- ✅ Historial de descargas
- ✅ Caché de resultados de búsqueda
- ✅ Puntuación de fuentes/usuarios
- ✅ Lista de vigilancia de autores (watchlist)
- ✅ Hashes de archivos (detección de duplicados)

---

## 📦 Estructura de la Base de Datos

### Archivo de Base de Datos
- **Ubicación**: `slskdown.db` (mismo directorio que el ejecutable)
- **Tamaño inicial**: ~100 KB
- **Crecimiento**: Depende del uso (típicamente 1-10 MB)

### Tablas Principales

#### 1. **Downloads** - Historial de Descargas
```sql
CREATE TABLE Downloads (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    FileName TEXT NOT NULL,
    Author TEXT,
    Username TEXT NOT NULL,
    SizeBytes INTEGER NOT NULL,
    Status TEXT NOT NULL,
    DownloadedAt DATETIME NOT NULL,
    FilePath TEXT,
    Speed REAL,
    Language TEXT,
    MD5Hash TEXT,
    SHA1Hash TEXT
);
```

**Índices**:
- `idx_downloads_author` - Búsqueda por autor
- `idx_downloads_username` - Búsqueda por usuario
- `idx_downloads_date` - Ordenamiento temporal
- `idx_downloads_status` - Filtrado por estado
- `idx_downloads_md5` - Detección de duplicados
- `idx_downloads_size` - Búsqueda por tamaño

#### 2. **SearchCache** - Caché de Búsquedas
```sql
CREATE TABLE SearchCache (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Query TEXT NOT NULL,
    Username TEXT NOT NULL,
    FileName TEXT NOT NULL,
    Size INTEGER NOT NULL,
    FilePath TEXT,
    Speed REAL,
    CachedAt DATETIME NOT NULL,
    ExpiresAt DATETIME NOT NULL
);
```

**TTL (Time To Live)**: 7 días por defecto

#### 3. **SourceRatings** - Puntuación de Usuarios
```sql
CREATE TABLE SourceRatings (
    Username TEXT PRIMARY KEY,
    AverageSpeed REAL NOT NULL DEFAULT 0,
    SuccessRate REAL NOT NULL DEFAULT 0,
    TotalDownloads INTEGER NOT NULL DEFAULT 0,
    FailedDownloads INTEGER NOT NULL DEFAULT 0,
    DisconnectionCount INTEGER NOT NULL DEFAULT 0,
    LastSeen DATETIME NOT NULL,
    Score REAL NOT NULL DEFAULT 0
);
```

**Cálculo de Score** (0-100):
- 40% Velocidad promedio
- 30% Tasa de éxito
- 20% Fiabilidad (sin desconexiones)
- 10% Recencia (última vez visto)

#### 4. **Watchlist** - Autores Favoritos
```sql
CREATE TABLE Watchlist (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Author TEXT NOT NULL UNIQUE,
    Enabled INTEGER NOT NULL DEFAULT 1,
    AutoDownload INTEGER NOT NULL DEFAULT 0,
    NotifyOnNew INTEGER NOT NULL DEFAULT 1,
    SearchIntervalHours REAL NOT NULL DEFAULT 24,
    LastSearch DATETIME NOT NULL,
    CreatedAt DATETIME NOT NULL
);
```

#### 5. **FileHashes** - Detección de Duplicados
```sql
CREATE TABLE FileHashes (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    FilePath TEXT NOT NULL UNIQUE,
    FileName TEXT NOT NULL,
    Size INTEGER NOT NULL,
    MD5Hash TEXT,
    SHA1Hash TEXT,
    CreatedAt DATETIME NOT NULL,
    LastModified DATETIME NOT NULL
);
```

---

## 🚀 Uso de la API

### Inicialización

```csharp
using SlskDown.Database;

// Crear instancia de la base de datos
var db = new SlskDatabase("slskdown.db");

// La base de datos se crea automáticamente si no existe
// Las tablas se crean en el constructor
```

### Operaciones de Descargas

#### Insertar Descarga
```csharp
var download = new DownloadRecord
{
    FileName = "libro.epub",
    Author = "Isaac Asimov",
    Username = "user123",
    SizeBytes = 1024000,
    Status = "Completed",
    DownloadedAt = DateTime.UtcNow,
    FilePath = @"C:\Downloads\libro.epub",
    Speed = 2.5,
    Language = "español"
};

var id = await db.InsertDownloadAsync(download);
```

#### Consultar Descargas
```csharp
// Últimas 100 descargas
var recent = await db.GetDownloadsAsync(limit: 100);

// Descargas de un autor específico
var byAuthor = await db.GetDownloadsByAuthorAsync("Isaac Asimov");

// Buscar por hash (duplicados)
var duplicate = await db.FindDownloadByHashAsync("abc123def456");

// Buscar por tamaño
var sameSize = await db.FindDownloadsBySizeAsync(1024000);
```

### Caché de Búsquedas

#### Guardar Resultados
```csharp
var results = new List<SearchCacheEntry>
{
    new SearchCacheEntry
    {
        Query = "asimov",
        Username = "user123",
        FileName = "fundacion.epub",
        Size = 500000,
        Speed = 3.0,
        CachedAt = DateTime.UtcNow,
        ExpiresAt = DateTime.UtcNow.AddDays(7)
    }
};

await db.CacheSearchResultsAsync("asimov", results);
```

#### Recuperar Caché
```csharp
// Verificar si hay caché válido
if (await db.HasCachedResultsAsync("asimov"))
{
    var cached = await db.GetCachedSearchResultsAsync("asimov");
    // Usar resultados cacheados (instantáneo)
}
else
{
    // Realizar búsqueda en red
}

// Limpiar caché expirado
await db.CleanExpiredCacheAsync();
```

### Puntuación de Fuentes

#### Actualizar Estadísticas
```csharp
// Después de cada descarga
await db.UpdateSourceStatsAsync(
    username: "user123",
    success: true,
    speed: 2.5,
    disconnected: false
);

// El score se calcula automáticamente
```

#### Consultar Mejores Fuentes
```csharp
// Top 10 usuarios más confiables
var topSources = await db.GetTopSourcesAsync(10);

foreach (var source in topSources)
{
    Console.WriteLine($"{source.Username}: {source.Score:F1} pts");
    Console.WriteLine($"  Velocidad: {source.AverageSpeed:F2} MB/s");
    Console.WriteLine($"  Éxito: {source.SuccessRate * 100:F1}%");
}

// Obtener rating de un usuario específico
var rating = await db.GetSourceRatingAsync("user123");
```

### Watchlist de Autores

#### Agregar Autor
```csharp
var entry = new WatchlistEntry
{
    Author = "Isaac Asimov",
    Enabled = true,
    AutoDownload = false,
    NotifyOnNew = true,
    SearchIntervalHours = 24,
    LastSearch = DateTime.UtcNow,
    CreatedAt = DateTime.UtcNow
};

var id = await db.AddToWatchlistAsync(entry);
```

#### Verificar Autores Pendientes
```csharp
// Obtener autores que necesitan búsqueda
var pending = await db.GetActiveWatchlistAsync();

foreach (var author in pending)
{
    // Realizar búsqueda del autor
    // ...
    
    // Actualizar última búsqueda
    await db.UpdateWatchlistLastSearchAsync(author.Id);
}
```

### Detección de Duplicados

#### Indexar Archivo
```csharp
await db.InsertFileHashAsync(
    filePath: @"C:\Downloads\libro.epub",
    fileName: "libro.epub",
    size: 1024000,
    md5Hash: "abc123def456",
    sha1Hash: null
);
```

#### Verificar Duplicado
```csharp
// Por hash (100% preciso)
var existing = await db.FindFileByHashAsync("abc123def456");
if (existing != null)
{
    Console.WriteLine($"Archivo ya existe en: {existing.FilePath}");
}

// Por tamaño (rápido, menos preciso)
var candidates = await db.FindFilesBySizeAsync(1024000);
foreach (var file in candidates)
{
    // Comparar nombres con algoritmo de similitud
    var similarity = CalculateSimilarity(newFileName, file.FileName);
    if (similarity > 0.85)
    {
        Console.WriteLine($"Posible duplicado: {file.FilePath}");
    }
}
```

### Estadísticas

```csharp
var stats = await db.GetStatisticsAsync();

Console.WriteLine($"Total descargas: {stats["TotalDownloads"]}");
Console.WriteLine($"Total GB: {stats["TotalGB"]:F2}");
Console.WriteLine($"Velocidad promedio: {stats["AverageSpeed"]:F2} MB/s");
Console.WriteLine($"Tasa de éxito: {stats["SuccessRate"]:F1}%");

var topAuthors = (List<dynamic>)stats["TopAuthors"];
foreach (var author in topAuthors)
{
    Console.WriteLine($"  {author.Author}: {author.Count} descargas");
}
```

---

## 🔧 Mantenimiento

### Optimizar Base de Datos
```csharp
// Compactar y optimizar (recuperar espacio)
await db.VacuumAsync();

// Obtener tamaño actual
var sizeBytes = await db.GetDatabaseSizeAsync();
Console.WriteLine($"Tamaño DB: {sizeBytes / 1024.0 / 1024.0:F2} MB");
```

### Backup
```csharp
// Cerrar conexión
db.Dispose();

// Copiar archivo
File.Copy("slskdown.db", $"backup_{DateTime.Now:yyyyMMdd}.db");

// Reabrir
db = new SlskDatabase("slskdown.db");
```

### Migración de Datos Existentes

```csharp
using SlskDown.Database;

var db = new SlskDatabase();
var migration = new DataMigration(db);

// Migrar desde JSON
var countJson = await migration.MigrateDownloadHistoryFromJsonAsync("download_history.json");
Console.WriteLine($"Migrados {countJson} registros desde JSON");

// Migrar desde CSV
var countCsv = await migration.MigrateDownloadHistoryFromCsvAsync("auto_search_results.csv");
Console.WriteLine($"Migrados {countCsv} registros desde CSV");

// Escanear carpeta de descargas (calcular hashes)
var countScanned = await migration.ScanDownloadFolderAsync(@"C:\Downloads");
Console.WriteLine($"Escaneados {countScanned} archivos");
```

---

## 📊 Consultas SQL Avanzadas

Si necesitas consultas personalizadas, puedes usar Dapper directamente:

```csharp
using Dapper;

// Obtener conexión interna
var connection = db.GetConnection(); // Necesitarías agregar este método público

// Consulta personalizada
var results = await connection.QueryAsync<dynamic>(@"
    SELECT 
        Author,
        COUNT(*) as Count,
        SUM(SizeBytes) as TotalSize,
        AVG(Speed) as AvgSpeed
    FROM Downloads
    WHERE Status = 'Completed'
    AND DownloadedAt > datetime('now', '-30 days')
    GROUP BY Author
    ORDER BY Count DESC
    LIMIT 20
");

foreach (var row in results)
{
    Console.WriteLine($"{row.Author}: {row.Count} descargas, {row.AvgSpeed:F2} MB/s");
}
```

---

## 🎯 Mejores Prácticas

### 1. **Usar Transacciones para Operaciones Masivas**
```csharp
using var transaction = connection.BeginTransaction();
try
{
    foreach (var download in downloads)
    {
        await db.InsertDownloadAsync(download);
    }
    transaction.Commit();
}
catch
{
    transaction.Rollback();
    throw;
}
```

### 2. **Limpiar Caché Periódicamente**
```csharp
// Ejecutar diariamente
await db.CleanExpiredCacheAsync();
```

### 3. **Vacuum Semanal**
```csharp
// Ejecutar semanalmente para optimizar
await db.VacuumAsync();
```

### 4. **Índices Personalizados**
Si necesitas consultas específicas frecuentes, agrega índices:
```sql
CREATE INDEX idx_custom ON Downloads(Author, DownloadedAt DESC);
```

### 5. **Dispose Correctamente**
```csharp
using (var db = new SlskDatabase())
{
    // Usar base de datos
}
// Se cierra automáticamente
```

---

## 🔍 Troubleshooting

### Base de Datos Bloqueada
```
SQLiteException: database is locked
```
**Solución**: Asegúrate de que solo una instancia accede a la DB. Usa `using` para cerrar conexiones.

### Archivo Corrupto
```
SQLiteException: file is not a database
```
**Solución**: Restaurar desde backup o eliminar y recrear.

### Rendimiento Lento
**Solución**: 
1. Ejecutar `VACUUM`
2. Verificar índices
3. Limitar resultados con `LIMIT`

---

## 📈 Próximas Mejoras

- [ ] Compresión de base de datos
- [ ] Replicación/sincronización
- [ ] Exportación a otros formatos
- [ ] Dashboard web con estadísticas
- [ ] API REST para acceso externo

---

## 📚 Referencias

- **SQLite**: https://www.sqlite.org/
- **Dapper**: https://github.com/DapperLib/Dapper
- **System.Data.SQLite**: https://system.data.sqlite.org/

---

**Versión**: 1.0  
**Fecha**: Noviembre 2025  
**Autor**: SlskDown Team
