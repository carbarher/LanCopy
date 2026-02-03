# 📖 Ejemplos de Uso - SlskDown v2.5

**Guía práctica para usar las nuevas funcionalidades**

---

## 🔔 1. Notificaciones Desktop

### **Uso Básico**

```csharp
// Notificar descarga completada
NotifyDownloadComplete("libro.pdf", 2500000);

// Notificar archivo grande
NotifyDownloadComplete("pelicula.mkv", 5000000000); // >100MB → notificación especial

// Notificar nuevo resultado en wishlist
NotifyWishlistResult("García Márquez", "Cien años de soledad.pdf");

// Notificar usuario conectado
NotifyUserOnline("usuario123");

// Notificar error
NotifyError("Error de Conexión", "No se pudo conectar al servidor");

// Notificar advertencia
NotifyWarning("Espacio en Disco", "Quedan menos de 1GB disponible");
```

### **Configuración**

```csharp
// Habilitar/deshabilitar notificaciones
notificationsEnabled = true;

// Configurar tipos específicos
notifyOnDownloadComplete = true;
notifyOnWishlistResult = true;
notifyOnLargeFile = true;

// Cambiar umbral de archivo grande
largeFileThresholdBytes = 200 * 1024 * 1024; // 200 MB

// Guardar configuración
SaveNotificationSettings();
```

### **Integración en Eventos**

```csharp
// En evento de descarga completada
private void OnDownloadCompleted(DownloadTask task)
{
    NotifyDownloadComplete(task.File.FileName, task.File.SizeBytes);
    
    // Si es ebook, agregar a Calibre
    if (IsEbook(task.File.FileName))
    {
        _ = calibreIntegration.AddBookAsync(task.LocalPath);
    }
}
```

---

## 📚 2. Modo Coleccionista

### **Crear Colección**

```csharp
// Crear colección de libros
var collection = collectionManager.CreateCollection(
    name: "Obras de Jorge Luis Borges",
    description: "Colección completa de cuentos y ensayos",
    type: CollectionType.Books
);

// Crear colección de música
var discografia = collectionManager.CreateCollection(
    name: "The Beatles - Discografía Completa",
    description: "Todos los álbumes de estudio",
    type: CollectionType.Music
);

// Crear colección personalizada
var custom = collectionManager.CreateCollection(
    name: "Documentales de Naturaleza",
    description: "Serie Planet Earth y similares",
    type: CollectionType.Custom
);
```

### **Agregar Items**

```csharp
// Agregar items a colección
collectionManager.AddItem(collection.Id, "El Aleph", "Borges El Aleph", required: true);
collectionManager.AddItem(collection.Id, "Ficciones", "Borges Ficciones", required: true);
collectionManager.AddItem(collection.Id, "El libro de arena", "Borges arena", required: true);
collectionManager.AddItem(collection.Id, "Historia universal de la infamia", "Borges infamia", required: false);

// Guardar
await collectionManager.SaveAsync();
```

### **Marcar Items como Encontrados/Descargados**

```csharp
// Cuando encuentras un archivo en búsqueda
collectionManager.MarkItemFound(
    collectionId: collection.Id,
    itemName: "El Aleph",
    filePath: "c:\\downloads\\Borges - El Aleph.pdf",
    fileSize: 2500000
);

// Cuando completas la descarga
collectionManager.MarkItemDownloaded(
    collectionId: collection.Id,
    itemName: "El Aleph"
);
```

### **Obtener Estadísticas**

```csharp
var stats = collectionManager.GetStats(collection.Id);

Console.WriteLine($"Colección: {stats.CollectionName}");
Console.WriteLine($"Total items: {stats.TotalItems}");
Console.WriteLine($"Descargados: {stats.DownloadedItems}");
Console.WriteLine($"Encontrados: {stats.FoundItems}");
Console.WriteLine($"Faltantes: {stats.MissingItems}");
Console.WriteLine($"Progreso: {stats.CompletionPercentage:F1}%");
Console.WriteLine($"Tamaño total: {stats.TotalSize / (1024 * 1024)} MB");
```

### **Detectar Duplicados**

```csharp
var duplicates = collectionManager.FindDuplicates(collection.Id);

foreach (var (item1, item2) in duplicates)
{
    Console.WriteLine($"Posible duplicado: '{item1.Name}' vs '{item2.Name}'");
}
```

### **Buscar Items Faltantes**

```csharp
var missing = collectionManager.GetMissingItems(collection.Id);

foreach (var item in missing)
{
    Console.WriteLine($"Faltante: {item.Name}");
    
    // Buscar automáticamente
    await SearchAsync(item.SearchTerm);
}
```

---

## 📖 3. Integración Calibre

### **Configuración Inicial**

```csharp
var calibre = new CalibreIntegration();

// Verificar si está disponible
if (calibre.IsAvailable)
{
    Console.WriteLine($"Calibre detectado: {calibre.LibraryPath}");
}
else
{
    // Configurar manualmente
    calibre.SetLibraryPath("C:\\Mi Biblioteca Calibre");
}
```

### **Agregar Libros**

```csharp
// Agregar libro simple
await calibre.AddBookAsync("c:\\downloads\\libro.epub");

// Agregar con metadata
await calibre.AddBookAsync(
    filePath: "c:\\downloads\\cien_anos.epub",
    author: "Gabriel García Márquez",
    title: "Cien años de soledad"
);
```

### **Verificar Duplicados Antes de Descargar**

```csharp
// Antes de iniciar descarga
var exists = await calibre.BookExistsAsync(
    title: "Cien años de soledad",
    author: "García Márquez"
);

if (exists)
{
    Console.WriteLine("Este libro ya está en tu biblioteca Calibre");
    // No descargar
}
else
{
    // Proceder con descarga
    await DownloadFileAsync(searchResult);
}
```

### **Buscar en Biblioteca**

```csharp
// Buscar todos los libros de un autor
var books = await calibre.GetBooksAsync("García Márquez");

foreach (var book in books)
{
    Console.WriteLine($"{book.Title} - {book.Authors}");
    Console.WriteLine($"Formatos: {string.Join(", ", book.Formats)}");
}
```

### **Sincronización Automática**

```csharp
// Sincronizar descargas recientes (últimos 7 días)
private async Task SyncRecentDownloadsToCalibre()
{
    var ebookExtensions = new[] { ".epub", ".pdf", ".mobi", ".azw3" };
    var recentFiles = Directory.GetFiles(downloadDir, "*.*", SearchOption.AllDirectories)
        .Where(f => ebookExtensions.Contains(Path.GetExtension(f).ToLower()))
        .Where(f => File.GetCreationTime(f) > DateTime.Now.AddDays(-7))
        .ToList();

    int added = 0;
    foreach (var file in recentFiles)
    {
        var fileName = Path.GetFileNameWithoutExtension(file);
        string author = null;
        
        // Extraer autor del nombre
        if (fileName.Contains(" - "))
        {
            author = fileName.Split(new[] { " - " }, StringSplitOptions.None)[0];
        }

        var success = await calibre.AddBookAsync(file, author);
        if (success)
        {
            added++;
            NotifyInfo("Libro agregado a Calibre", Path.GetFileName(file));
        }
    }

    Console.WriteLine($"Sincronización: {added}/{recentFiles.Count} libros agregados");
}
```

### **Exportar Libros**

```csharp
// Exportar libro desde Calibre
await calibre.ExportBookAsync(
    bookId: 123,
    destinationPath: "c:\\exports"
);
```

---

## ⚡ 4. Optimización Rust Core

### **Metadata de Audio MP3**

```csharp
using SlskDown.Core;

// Extraer metadata de MP3
var metadata = RustMetadataWrapper.ExtractMp3Metadata("c:\\music\\song.mp3");

if (metadata != null)
{
    Console.WriteLine($"Título: {metadata.Title}");
    Console.WriteLine($"Artista: {metadata.Artist}");
    Console.WriteLine($"Álbum: {metadata.Album}");
    Console.WriteLine($"Año: {metadata.Year}");
    Console.WriteLine($"Género: {metadata.Genre}");
    Console.WriteLine($"Bitrate: {metadata.Bitrate} kbps");
    Console.WriteLine($"Duración: {metadata.DurationSeconds} segundos");
}
```

### **Metadata de Audio FLAC**

```csharp
// Extraer metadata de FLAC
var metadata = RustMetadataWrapper.ExtractFlacMetadata("c:\\music\\song.flac");

if (metadata != null)
{
    Console.WriteLine(metadata.ToString());
    // Output: "Artist - Title (Year) [Bitrate kbps]"
}
```

### **Detección de Idioma Mejorada**

```csharp
// Detectar idioma de texto
var text = "El perro come la comida en la casa grande";
var language = RustMetadataWrapper.DetectLanguageAdvanced(text);

Console.WriteLine($"Idioma detectado: {language}"); // "spanish"

// Otros ejemplos
var en = RustMetadataWrapper.DetectLanguageAdvanced("The dog eats the food");
// Output: "english"

var fr = RustMetadataWrapper.DetectLanguageAdvanced("Le chien mange la nourriture");
// Output: "french"

var de = RustMetadataWrapper.DetectLanguageAdvanced("Der Hund frisst das Futter");
// Output: "german"
```

### **Compresión de Logs**

```csharp
// Comprimir logs
var logData = File.ReadAllBytes("app.log");
var compressed = RustMetadataWrapper.CompressLogData(logData);

Console.WriteLine($"Original: {logData.Length} bytes");
Console.WriteLine($"Comprimido: {compressed.Length} bytes");
Console.WriteLine($"Reducción: {(1 - (double)compressed.Length / logData.Length) * 100:F1}%");

// Guardar comprimido
File.WriteAllBytes("app.log.compressed", compressed);

// Descomprimir
var decompressed = RustMetadataWrapper.DecompressLogData(compressed);
```

### **Uso en Búsqueda de Archivos**

```csharp
// Al recibir resultados de búsqueda
foreach (var result in searchResults)
{
    // Detectar idioma del nombre de archivo
    var language = RustMetadataWrapper.DetectLanguageAdvanced(result.FileName);
    
    // Filtrar por idioma preferido
    if (language == "spanish")
    {
        // Priorizar resultados en español
        result.Priority += 10;
    }
    
    // Si es archivo de audio, extraer metadata
    if (result.FileName.EndsWith(".mp3"))
    {
        var metadata = RustMetadataWrapper.ExtractMp3Metadata(result.FullPath);
        if (metadata != null)
        {
            // Mostrar metadata en UI
            result.DisplayName = $"{metadata.Artist} - {metadata.Title}";
            result.AdditionalInfo = $"{metadata.Album} ({metadata.Year})";
        }
    }
}
```

---

## 📊 5. Dashboard Mejorado

### **Abrir Dashboard**

```csharp
// Desde botón en toolbar
private void btnDashboard_Click(object sender, EventArgs e)
{
    ShowEnhancedDashboard();
}

private void ShowEnhancedDashboard()
{
    var dashboard = new EnhancedDashboard(
        performanceMetrics,
        GetTopUsersForDashboard,
        GetTopFilesForDashboard,
        GetActivityByHourForDashboard
    );

    dashboard.Show();
}
```

### **Implementar Métodos de Datos**

```csharp
private List<(string, int, double)> GetTopUsersForDashboard()
{
    // Obtener top usuarios de tus datos
    return downloadHistory
        .GroupBy(d => d.Username)
        .Select(g => (
            username: g.Key,
            downloads: g.Count(),
            avgSpeed: g.Average(d => d.AverageSpeed)
        ))
        .OrderByDescending(x => x.downloads)
        .Take(10)
        .ToList();
}

private List<(string, int)> GetTopFilesForDashboard()
{
    // Obtener top tipos de archivo
    return downloadHistory
        .GroupBy(d => Path.GetExtension(d.FileName).ToLower())
        .Select(g => (
            extension: g.Key,
            count: g.Count()
        ))
        .OrderByDescending(x => x.count)
        .Take(10)
        .ToList();
}

private Dictionary<int, int> GetActivityByHourForDashboard()
{
    // Obtener actividad por hora
    return downloadHistory
        .GroupBy(d => d.CompletedAt.Hour)
        .ToDictionary(
            g => g.Key,
            g => g.Count()
        );
}
```

---

## 🌐 6. Integración OpenLibrary

### **Buscar Libros**

```csharp
var openLibrary = new OpenLibraryIntegration();

// Buscar por título
var books = await openLibrary.SearchByTitleAsync("Cien años de soledad", limit: 10);

foreach (var book in books)
{
    Console.WriteLine($"{book.Title} - {book.Author}");
    Console.WriteLine($"Año: {book.FirstPublishYear}");
    Console.WriteLine($"ISBN: {book.ISBN}");
    Console.WriteLine($"Páginas: {book.NumberOfPages}");
    Console.WriteLine($"Portada: {book.CoverUrl}");
    Console.WriteLine();
}

// Buscar por autor
var authorBooks = await openLibrary.SearchByAuthorAsync("García Márquez", limit: 20);
```

### **Obtener Información de Autor**

```csharp
// Buscar autor
var books = await openLibrary.SearchByAuthorAsync("García Márquez");
var authorKey = books.First().AuthorKey;

// Obtener información del autor
var author = await openLibrary.GetAuthorAsync(authorKey);

Console.WriteLine($"Nombre: {author.Name}");
Console.WriteLine($"Biografía: {author.Bio}");
Console.WriteLine($"Nacimiento: {author.BirthDate}");
Console.WriteLine($"Fallecimiento: {author.DeathDate}");

// Obtener todas las obras del autor
var works = await openLibrary.GetAuthorWorksAsync(authorKey, limit: 50);
```

### **Enriquecer Resultados de Búsqueda**

```csharp
// Cuando usuario busca un libro
private async Task EnrichSearchResults(List<SearchResult> results)
{
    foreach (var result in results)
    {
        // Buscar metadata en OpenLibrary
        var books = await openLibrary.SearchByTitleAsync(result.FileName, limit: 1);
        
        if (books.Any())
        {
            var book = books.First();
            
            // Agregar metadata al resultado
            result.Metadata = new Dictionary<string, string>
            {
                ["Author"] = book.Author,
                ["Year"] = book.FirstPublishYear?.ToString(),
                ["Pages"] = book.NumberOfPages?.ToString(),
                ["Publisher"] = book.Publisher,
                ["ISBN"] = book.ISBN,
                ["CoverUrl"] = book.CoverUrl
            };
            
            // Mostrar portada en UI
            if (!string.IsNullOrEmpty(book.CoverUrl))
            {
                result.ThumbnailUrl = book.CoverUrl;
            }
        }
    }
}
```

### **Descargar Portadas**

```csharp
// Descargar y mostrar portada
private async Task ShowBookCover(string isbn)
{
    var coverUrl = openLibrary.GetCoverUrlByISBN(isbn, size: "L"); // S, M, o L
    
    using (var client = new HttpClient())
    {
        var imageBytes = await client.GetByteArrayAsync(coverUrl);
        
        using (var ms = new MemoryStream(imageBytes))
        {
            pictureBox.Image = Image.FromStream(ms);
        }
    }
}
```

---

## 🔄 Flujos de Trabajo Completos

### **Flujo 1: Descargar y Organizar Ebook**

```csharp
private async Task DownloadAndOrganizeEbook(SearchResult result)
{
    // 1. Verificar si ya está en Calibre
    var exists = await calibre.BookExistsAsync(result.Title, result.Author);
    if (exists)
    {
        NotifyWarning("Libro Duplicado", "Este libro ya está en tu biblioteca");
        return;
    }
    
    // 2. Obtener metadata de OpenLibrary
    var books = await openLibrary.SearchByTitleAsync(result.Title);
    var metadata = books.FirstOrDefault();
    
    // 3. Descargar archivo
    await DownloadFileAsync(result);
    
    // 4. Notificar descarga completada
    NotifyDownloadComplete(result.FileName, result.FileSize);
    
    // 5. Agregar a Calibre con metadata
    if (metadata != null)
    {
        await calibre.AddBookAsync(
            result.LocalPath,
            author: metadata.Author,
            title: metadata.Title
        );
        
        NotifyInfo("Libro agregado a Calibre", metadata.Title);
    }
    
    // 6. Actualizar colección si aplica
    var collections = collectionManager.GetAllCollections()
        .Where(c => c.Type == CollectionType.Books);
    
    foreach (var collection in collections)
    {
        var item = collection.Items.FirstOrDefault(i => 
            i.Name.Contains(result.Title, StringComparison.OrdinalIgnoreCase));
        
        if (item != null)
        {
            collectionManager.MarkItemDownloaded(collection.Id, item.Name);
        }
    }
}
```

### **Flujo 2: Completar Colección Automáticamente**

```csharp
private async Task AutoCompleteCollection(string collectionId)
{
    // 1. Obtener items faltantes
    var missing = collectionManager.GetMissingItems(collectionId);
    
    Console.WriteLine($"Buscando {missing.Count} items faltantes...");
    
    // 2. Buscar cada item
    foreach (var item in missing)
    {
        // Marcar como buscando
        collectionManager.MarkItemSearching(collectionId, item.Name);
        
        // Buscar en Soulseek
        var results = await SearchAsync(item.SearchTerm);
        
        if (results.Any())
        {
            // Tomar mejor resultado
            var best = results.OrderByDescending(r => r.Quality).First();
            
            // Marcar como encontrado
            collectionManager.MarkItemFound(
                collectionId,
                item.Name,
                best.FullPath,
                best.FileSize
            );
            
            // Descargar automáticamente
            await DownloadFileAsync(best);
            
            // Marcar como descargado
            collectionManager.MarkItemDownloaded(collectionId, item.Name);
            
            // Notificar
            NotifyInfo($"Item completado", item.Name);
        }
        
        // Esperar entre búsquedas
        await Task.Delay(2000);
    }
    
    // 3. Mostrar resumen
    var stats = collectionManager.GetStats(collectionId);
    NotifyInfo(
        "Colección actualizada",
        $"{stats.CompletionPercentage:F1}% completo ({stats.DownloadedItems}/{stats.TotalItems})"
    );
}
```

---

## 🎯 Resumen de APIs

| Funcionalidad | Clase Principal | Métodos Clave |
|---------------|----------------|---------------|
| **Notificaciones** | `MainForm.Notifications.cs` | `NotifyDownloadComplete`, `NotifyWishlistResult`, `NotifyUserOnline` |
| **Colecciones** | `CollectionManager` | `CreateCollection`, `AddItem`, `MarkItemDownloaded`, `GetStats` |
| **Calibre** | `CalibreIntegration` | `AddBookAsync`, `BookExistsAsync`, `GetBooksAsync` |
| **Rust Metadata** | `RustMetadataWrapper` | `ExtractMp3Metadata`, `DetectLanguageAdvanced`, `CompressLogData` |
| **Dashboard** | `EnhancedDashboard` | Constructor con callbacks |
| **OpenLibrary** | `OpenLibraryIntegration` | `SearchByTitleAsync`, `GetAuthorAsync`, `GetCoverUrl` |

---

**¡Todas las funcionalidades están listas para usar!** 🚀
