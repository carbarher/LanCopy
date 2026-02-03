# 📚 Guía de Integración con Calibre

## ¿Qué es Calibre?

**Calibre** es el gestor de biblioteca de eBooks más popular:
- ✅ Gestiona EPUB, PDF, MOBI, AZW3, etc.
- ✅ Convierte entre formatos
- ✅ Edita metadata (autor, título, portada)
- ✅ Sincroniza con Kindle, Kobo, etc.
- ✅ Servidor web para acceso remoto

**Web:** https://calibre-ebook.com/

---

## 🔗 Integración con SlskDown

### Flujo Completo

```
1. SlskDown busca → "Isaac Asimov Foundation"
2. Descarga EPUB → c:\p2p\downloads\Foundation.epub
3. SlskDown detecta descarga completada
4. Automáticamente agrega a Calibre
5. Calibre organiza en biblioteca
6. ✅ Listo para leer
```

---

## 🚀 Opción 1: Carpeta Monitoreada (Simple)

### Configuración en Calibre

1. **Abrir Calibre**
2. **Preferencias** → **Agregar libros** → **Auto-agregar**
3. **Agregar carpeta monitoreada:**
   ```
   c:\p2p\downloads
   ```
4. **Configurar opciones:**
   - ✅ Mover archivos a biblioteca
   - ✅ Eliminar archivos originales
   - ✅ Auto-agregar metadata

5. **Guardar**

### Ventajas
- ✅ Muy simple
- ✅ No requiere código
- ✅ Funciona automáticamente

### Desventajas
- ❌ Calibre debe estar abierto
- ❌ No hay control desde SlskDown

---

## 💻 Opción 2: API de Calibre (Avanzada)

### Instalación

1. **Instalar Calibre:**
   - Descargar de https://calibre-ebook.com/
   - Instalar normalmente

2. **Verificar instalación:**
   ```bash
   # Abrir CMD
   calibredb --version
   ```

3. **Agregar `CalibreIntegration.cs` a SlskDown** ✅ (Ya creado)

### Uso en MainForm.cs

```csharp
// En constructor de MainForm
private CalibreIntegration? _calibre;

public MainForm()
{
    // ... código existente ...
    
    // Inicializar Calibre
    _calibre = new CalibreIntegration(logger: _logger);
    
    if (_calibre.IsAvailable)
    {
        _logger?.Info("✅ Calibre detectado y listo");
    }
    else
    {
        _logger?.Warning("⚠️ Calibre no detectado");
    }
}
```

### Agregar Libro Automáticamente

```csharp
// Cuando se completa una descarga
private async Task OnDownloadCompleted(string filePath, string author)
{
    // ... código existente ...
    
    // Agregar a Calibre si está disponible
    if (_calibre?.IsAvailable == true)
    {
        // Detectar si es un eBook
        var extension = Path.GetExtension(filePath).ToLower();
        var ebookExtensions = new[] { ".epub", ".pdf", ".mobi", ".azw3", ".fb2" };
        
        if (ebookExtensions.Contains(extension))
        {
            var title = Path.GetFileNameWithoutExtension(filePath);
            var tags = new[] { "SlskDown", "Auto-agregado" };
            
            var added = await _calibre.AddBookAsync(
                filePath: filePath,
                author: author,
                title: title,
                tags: tags
            );
            
            if (added)
            {
                _logger?.Info($"📚 Agregado a Calibre: {title}");
                _notificationManager?.ShowNotification(
                    "Libro agregado a Calibre",
                    $"{title} por {author}",
                    ToolTipIcon.Info
                );
            }
        }
    }
}
```

---

## 🎯 Funcionalidades Disponibles

### 1. Agregar Libro

```csharp
// Agregar con metadata completa
await _calibre.AddBookAsync(
    filePath: @"c:\p2p\downloads\Foundation.epub",
    author: "Isaac Asimov",
    title: "Foundation",
    tags: new[] { "Ciencia Ficción", "Clásico", "Español" }
);
```

### 2. Buscar Libros

```csharp
// Buscar en biblioteca
var books = _calibre.SearchBooks("asimov");

foreach (var book in books)
{
    Console.WriteLine($"{book.Title} por {book.Authors}");
}
```

### 3. Obtener Información

```csharp
// Obtener libro por ID
var book = _calibre.GetBookById(42);

if (book != null)
{
    Console.WriteLine($"Título: {book.Title}");
    Console.WriteLine($"Autor: {book.Authors}");
    Console.WriteLine($"Tags: {string.Join(", ", book.Tags)}");
}
```

### 4. Actualizar Metadata

```csharp
// Actualizar campo
_calibre.UpdateMetadata(
    bookId: 42,
    field: "rating",
    value: "5"
);
```

### 5. Abrir en Calibre

```csharp
// Abrir Calibre en un libro específico
_calibre.OpenInCalibre(bookId: 42);
```

### 6. Estadísticas

```csharp
// Obtener estadísticas de biblioteca
var stats = _calibre.GetLibraryStats();

Console.WriteLine($"Total libros: {stats.TotalBooks}");
Console.WriteLine($"Autores: {stats.Authors}");
Console.WriteLine($"Tags: {stats.Tags}");
```

---

## 🎨 UI en SlskDown

### Botón "Agregar a Calibre"

```csharp
// En pestaña de Descargas
var addToCalibreButton = new Button
{
    Text = "📚 Agregar a Calibre",
    Location = new Point(10, 10),
    Size = new Size(150, 30)
};

addToCalibreButton.Click += async (s, e) =>
{
    if (downloadsListView.SelectedItems.Count > 0)
    {
        var item = downloadsListView.SelectedItems[0];
        var filePath = item.Tag as string;
        var author = item.SubItems[1].Text;
        
        if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
        {
            var added = await _calibre.AddBookAsync(filePath, author);
            
            if (added)
            {
                MessageBox.Show("✅ Libro agregado a Calibre", "Éxito");
            }
        }
    }
};
```

### CheckBox "Auto-agregar a Calibre"

```csharp
// En pestaña Config
var autoAddToCalibreCheckBox = new CheckBox
{
    Text = "Auto-agregar descargas a Calibre",
    Location = new Point(10, 200),
    Checked = true
};

autoAddToCalibreCheckBox.CheckedChanged += (s, e) =>
{
    // Guardar preferencia
    SavePreference("autoAddToCalibre", autoAddToCalibreCheckBox.Checked);
};
```

### Label de Estado

```csharp
// Mostrar estado de Calibre
var calibreStatusLabel = new Label
{
    Text = _calibre?.IsAvailable == true 
        ? "✅ Calibre: Conectado" 
        : "❌ Calibre: No detectado",
    Location = new Point(10, 230),
    AutoSize = true
};
```

---

## 📊 Casos de Uso

### Caso 1: Biblioteca Automática

```
Usuario descarga 100 libros de Asimov
→ SlskDown los agrega automáticamente a Calibre
→ Calibre organiza por autor/serie
→ Usuario tiene biblioteca ordenada sin esfuerzo
```

### Caso 2: Sincronización con Kindle

```
Usuario descarga EPUB
→ SlskDown agrega a Calibre
→ Calibre convierte a MOBI
→ Usuario sincroniza con Kindle
→ Lee en Kindle
```

### Caso 3: Gestión de Metadata

```
Usuario descarga libro sin portada
→ SlskDown agrega a Calibre
→ Calibre busca metadata en internet
→ Descarga portada automáticamente
→ Libro queda completo
```

---

## 🔧 Comandos de Calibre

### Comandos Útiles

```bash
# Agregar libro
calibredb add "Foundation.epub" --authors "Isaac Asimov"

# Listar libros
calibredb list

# Buscar
calibredb list --search "asimov"

# Ver metadata
calibredb show_metadata 42

# Actualizar metadata
calibredb set_metadata 42 --field="rating:5"

# Exportar
calibredb export 42 --to-dir="c:\exports"

# Estadísticas
calibredb list --for-machine
```

---

## 🎯 Integración Completa en SlskDown

### Flujo Recomendado

```csharp
// 1. Al completar descarga
private async Task OnDownloadCompleted(DownloadInfo info)
{
    var filePath = info.FilePath;
    var author = info.Author;
    
    // 2. Verificar si es eBook
    if (IsEBook(filePath))
    {
        // 3. Agregar a Calibre
        if (_calibre?.IsAvailable == true && 
            GetPreference("autoAddToCalibre", true))
        {
            var added = await _calibre.AddBookAsync(
                filePath: filePath,
                author: author,
                title: Path.GetFileNameWithoutExtension(filePath),
                tags: new[] { "SlskDown", DetectGenre(filePath) }
            );
            
            if (added)
            {
                // 4. Notificar usuario
                _notificationManager?.ShowNotification(
                    "📚 Agregado a Calibre",
                    $"{Path.GetFileName(filePath)}",
                    ToolTipIcon.Info
                );
                
                // 5. Registrar en dashboard
                _performanceDashboard?.RecordEvent("calibre_add");
                
                // 6. Log
                _logger?.Info($"Libro agregado a Calibre: {filePath}");
            }
        }
    }
}

private bool IsEBook(string filePath)
{
    var extension = Path.GetExtension(filePath).ToLower();
    return new[] { ".epub", ".pdf", ".mobi", ".azw3", ".fb2" }.Contains(extension);
}

private string DetectGenre(string filePath)
{
    var filename = Path.GetFileName(filePath).ToLower();
    
    if (filename.Contains("scifi") || filename.Contains("ciencia ficcion"))
        return "Ciencia Ficción";
    if (filename.Contains("fantasy") || filename.Contains("fantasia"))
        return "Fantasía";
    if (filename.Contains("terror") || filename.Contains("horror"))
        return "Terror";
    
    return "General";
}
```

---

## 📖 Recursos

### Documentación Oficial
- **Calibre Manual:** https://manual.calibre-ebook.com/
- **calibredb CLI:** https://manual.calibre-ebook.com/generated/en/calibredb.html
- **API de Calibre:** https://manual.calibre-ebook.com/server.html

### Alternativas
- **Calibre Content Server:** API REST completa
- **Calibre-Web:** Interfaz web moderna
- **COPS:** PHP-based OPDS server

---

## ✅ Checklist de Implementación

- [x] Crear `CalibreIntegration.cs`
- [ ] Agregar variable `_calibre` en MainForm
- [ ] Inicializar en constructor
- [ ] Llamar en `OnDownloadCompleted`
- [ ] Agregar botón "Agregar a Calibre" en UI
- [ ] Agregar checkbox "Auto-agregar" en Config
- [ ] Agregar label de estado
- [ ] Probar con libro de prueba
- [ ] Documentar en README

---

## 🎉 Resultado Final

**Con esta integración:**
- ✅ Descargas automáticas a Calibre
- ✅ Metadata organizada
- ✅ Biblioteca profesional
- ✅ Sincronización con eReaders
- ✅ Conversión de formatos
- ✅ Acceso remoto vía web

**SlskDown + Calibre = Biblioteca perfecta** 📚

---

**Fecha:** 30 Octubre 2025  
**Versión:** 4.0  
**Estado:** ✅ Código listo para integrar
