# 🚀 Guía de Activación - SlskDown v2.5

**Pasos para activar todas las funcionalidades avanzadas**

---

## ✅ Checklist de Activación

- [ ] 1. Actualizar `MainForm.cs` con inicialización
- [ ] 2. Compilar proyecto
- [ ] 3. Verificar funcionalidades
- [ ] 4. Configurar Calibre (opcional)
- [ ] 5. Probar notificaciones
- [ ] 6. Crear primera colección

---

## 📝 Paso 1: Actualizar MainForm.cs

### **Opción A: Buscar método de inicialización existente**

Busca en `MainForm.cs` un método como `MainForm_Load`, `InitializeComponent`, o similar donde se inicializan los componentes.

### **Opción B: Agregar en constructor**

Si no existe un método de carga, agrega en el constructor de `MainForm`:

```csharp
public MainForm()
{
    InitializeComponent();
    
    // ACTIVAR FUNCIONALIDADES V2.5
    InitializeAdvancedFeatures();
}
```

### **Código a Agregar**

```csharp
private void InitializeApplication()
{
    try
    {
        // ... código existente ...
        
        // ============================================
        // NUEVAS FUNCIONALIDADES V2.5
        // ============================================
        InitializeAdvancedFeatures();
        
        Log("✅ SlskDown v2.5 - Advanced Features Edition iniciado");
        
        // ... resto del código ...
    }
    catch (Exception ex)
    {
        Log($"❌ Error en inicialización: {ex.Message}");
    }
}
```

---

## 🔧 Paso 2: Verificar Dependencias

### **Verificar que existen estos archivos:**

```
✅ MainForm.Notifications.cs
✅ MainForm.Metrics.cs
✅ MainForm.Browse.cs
✅ MainForm.UIIntegration.cs
✅ Core/Collections/CollectionManager.cs
✅ Core/Integrations/CalibreIntegration.cs
✅ Core/Integrations/OpenLibraryIntegration.cs
✅ Core/RustMetadataWrapper.cs
✅ UI/EnhancedDashboard.cs
✅ UI/PerformanceDashboard.cs
✅ rust_core/src/metadata.rs
```

### **Verificar NuGet Packages:**

Si usas ScottPlot para gráficos en el dashboard:

```bash
dotnet add package ScottPlot.WinForms
```

---

## 🏗️ Paso 3: Compilar Proyecto

### **Compilar C#:**

```bash
cd c:\p2p\SlskDown
dotnet build -c Release
```

**Resultado esperado**: ✅ Build succeeded

### **Compilar Rust (si usas metadata):**

```bash
cd c:\p2p\SlskDown\rust_core
cargo build --release
```

**Resultado esperado**: `rust_core.dll` en `target/release/`

---

## 🎯 Paso 4: Verificar Funcionalidades

### **Al iniciar la aplicación, deberías ver:**

1. **En el log:**
   ```
   ✅ Sistema de notificaciones inicializado
   ✅ CollectionManager inicializado
   ✅ Calibre detectado: C:\Calibre Library
   ✅ OpenLibrary inicializado
   ✅ UI de funcionalidades avanzadas creada
   ✅ Funcionalidades avanzadas inicializadas
   ```

2. **En el toolbar:**
   - Botón "📊 Dashboard"
   - Botón "📚 Colecciones"
   - Botón "📖 Calibre" (si Calibre está disponible)

3. **En la bandeja del sistema:**
   - Icono de SlskDown
   - Menú contextual con opciones

---

## 📖 Paso 5: Configurar Calibre (Opcional)

### **Si Calibre no se detecta automáticamente:**

1. Abrir SlskDown
2. Ir a Configuración/Ajustes
3. Buscar sección "Integraciones"
4. Ingresar ruta de biblioteca Calibre manualmente

**O en código:**

```csharp
// En algún método de configuración
if (!calibreIntegration.IsAvailable)
{
    var path = ShowFolderDialog("Selecciona tu biblioteca Calibre");
    calibreIntegration.SetLibraryPath(path);
}
```

### **Ubicaciones comunes de Calibre:**

- `C:\Calibre Library`
- `C:\Users\[Usuario]\Documents\Calibre Library`
- `C:\Users\[Usuario]\Calibre Library`

---

## 🔔 Paso 6: Probar Notificaciones

### **Prueba Manual:**

```csharp
// Agregar botón de prueba temporal
private void btnTestNotifications_Click(object sender, EventArgs e)
{
    NotifyDownloadComplete("test.pdf", 2500000);
    NotifyWishlistResult("Test", "Resultado de prueba");
    NotifyUserOnline("usuario_test");
}
```

### **Verificar:**

1. Minimizar ventana
2. Hacer clic en botón de prueba
3. Ver notificaciones en bandeja del sistema

---

## 📚 Paso 7: Crear Primera Colección

### **Desde UI:**

1. Clic en "📚 Colecciones"
2. Clic en "Nueva Colección"
3. Llenar formulario:
   - Nombre: "Prueba"
   - Descripción: "Colección de prueba"
   - Tipo: Books
4. Clic en "Crear"

### **Desde código:**

```csharp
// Crear colección de prueba
var collection = collectionManager.CreateCollection(
    "Obras de Borges",
    "Colección completa de Jorge Luis Borges",
    CollectionType.Books
);

// Agregar algunos items
collectionManager.AddItem(collection.Id, "El Aleph", "Borges El Aleph");
collectionManager.AddItem(collection.Id, "Ficciones", "Borges Ficciones");
collectionManager.AddItem(collection.Id, "El libro de arena", "Borges arena");

// Guardar
await collectionManager.SaveAsync();

// Ver estadísticas
var stats = collectionManager.GetStats(collection.Id);
Console.WriteLine($"Colección creada: {stats.TotalItems} items");
```

---

## 📊 Paso 8: Abrir Dashboard

### **Desde UI:**

1. Clic en "📊 Dashboard" en toolbar
2. Ver gráficos y estadísticas

### **Verificar que muestra:**

- 8 cards de estadísticas
- Gráfico de velocidad
- Gráfico de actividad por hora
- Top 10 usuarios
- Top 10 tipos de archivo

---

## 🔍 Paso 9: Probar OpenLibrary

### **Prueba rápida:**

```csharp
private async void btnTestOpenLibrary_Click(object sender, EventArgs e)
{
    var books = await openLibraryIntegration.SearchByTitleAsync("Cien años de soledad");
    
    if (books.Any())
    {
        var book = books.First();
        MessageBox.Show(
            $"Título: {book.Title}\n" +
            $"Autor: {book.Author}\n" +
            $"Año: {book.FirstPublishYear}\n" +
            $"ISBN: {book.ISBN}\n" +
            $"Portada: {book.CoverUrl}",
            "OpenLibrary Test"
        );
    }
}
```

---

## ⚡ Paso 10: Probar Rust Metadata (Opcional)

### **Si compilaste Rust Core:**

```csharp
private void btnTestRustMetadata_Click(object sender, EventArgs e)
{
    // Probar detección de idioma
    var spanish = RustMetadataWrapper.DetectLanguageAdvanced("El perro come la comida");
    var english = RustMetadataWrapper.DetectLanguageAdvanced("The dog eats the food");
    
    MessageBox.Show(
        $"Español detectado: {spanish}\n" +
        $"Inglés detectado: {english}",
        "Rust Metadata Test"
    );
    
    // Probar metadata MP3 (si tienes un archivo)
    var metadata = RustMetadataWrapper.ExtractMp3Metadata("c:\\music\\test.mp3");
    if (metadata != null)
    {
        MessageBox.Show(
            $"Artista: {metadata.Artist}\n" +
            $"Título: {metadata.Title}\n" +
            $"Bitrate: {metadata.Bitrate} kbps",
            "MP3 Metadata"
        );
    }
}
```

---

## 🎯 Integración con Eventos Existentes

### **Conectar notificaciones a descargas:**

```csharp
// En tu método de descarga completada
private void OnDownloadCompleted(DownloadTask task)
{
    // Código existente...
    
    // NUEVO: Notificar
    NotifyDownloadComplete(task.File.FileName, task.File.SizeBytes);
    
    // NUEVO: Si es ebook, agregar a Calibre
    if (IsEbook(task.File.FileName) && calibreIntegration?.IsAvailable == true)
    {
        _ = calibreIntegration.AddBookAsync(task.LocalPath);
    }
    
    // NUEVO: Actualizar colecciones
    UpdateCollectionsIfApplicable(task.File.FileName);
}

private bool IsEbook(string fileName)
{
    var ebookExtensions = new[] { ".epub", ".pdf", ".mobi", ".azw3", ".djvu" };
    return ebookExtensions.Contains(Path.GetExtension(fileName).ToLower());
}

private void UpdateCollectionsIfApplicable(string fileName)
{
    var collections = collectionManager.GetAllCollections();
    
    foreach (var collection in collections)
    {
        var item = collection.Items.FirstOrDefault(i => 
            fileName.Contains(i.Name, StringComparison.OrdinalIgnoreCase));
        
        if (item != null)
        {
            collectionManager.MarkItemDownloaded(collection.Id, item.Name);
        }
    }
}
```

---

## 🐛 Solución de Problemas

### **Problema: Botones no aparecen en toolbar**

**Solución:**
```csharp
// Verificar que el toolbar existe
var toolbar = Controls.Find("mainToolbar", true).FirstOrDefault() as ToolStrip;
if (toolbar == null)
{
    Log("⚠️ Toolbar no encontrado, creando botones alternativos");
    CreateAlternativeButtons();
}
```

### **Problema: Calibre no se detecta**

**Solución:**
```csharp
// Configurar manualmente
calibreIntegration.SetLibraryPath("C:\\Mi Biblioteca Calibre");
```

### **Problema: Notificaciones no aparecen**

**Solución:**
```csharp
// Verificar que NotifyIcon está inicializado
if (notifyIcon == null)
{
    Log("⚠️ NotifyIcon no inicializado");
    InitializeNotifications();
}

// Verificar que están habilitadas
notificationsEnabled = true;
```

### **Problema: Dashboard no muestra datos**

**Solución:**
```csharp
// Implementar métodos de datos con valores de prueba
private List<(string, int, double)> GetTopUsersForDashboard()
{
    // Datos de prueba si no hay datos reales
    return new List<(string, int, double)>
    {
        ("usuario1", 10, 1500000),
        ("usuario2", 8, 1200000),
        ("usuario3", 5, 900000)
    };
}
```

---

## ✅ Verificación Final

### **Checklist de funcionalidades activas:**

- [ ] Notificaciones aparecen al completar descargas
- [ ] Icono en bandeja del sistema funciona
- [ ] Botones en toolbar son visibles y funcionales
- [ ] Dashboard se abre y muestra datos
- [ ] Gestor de colecciones funciona
- [ ] Se pueden crear nuevas colecciones
- [ ] Calibre sincroniza (si está configurado)
- [ ] OpenLibrary devuelve resultados
- [ ] Rust metadata funciona (si está compilado)

---

## 🎉 ¡Listo!

**SlskDown v2.5 - Advanced Features Edition está completamente activado.**

### **Próximos pasos sugeridos:**

1. **Crear tu primera colección real**
   - Ejemplo: "Obras de tu autor favorito"
   - Agregar 10-20 items
   - Dejar que el sistema busque automáticamente

2. **Configurar notificaciones a tu gusto**
   - Ajustar umbral de archivos grandes
   - Habilitar/deshabilitar tipos específicos

3. **Sincronizar con Calibre**
   - Si tienes ebooks descargados
   - Ejecutar sincronización de últimos 7 días

4. **Explorar el dashboard**
   - Identificar patrones de uso
   - Ver tus usuarios más rápidos
   - Optimizar horarios de búsqueda

---

## 📚 Documentación Adicional

- **Funcionalidades técnicas**: `NUEVAS_FUNCIONALIDADES_2026.md`
- **Integración UI**: `INTEGRACION_UI_COMPLETA.md`
- **Ejemplos de uso**: `EJEMPLOS_USO_V2.5.md`
- **README general**: `README_V2.5.md`

---

**¿Necesitas ayuda?** Consulta los ejemplos en `EJEMPLOS_USO_V2.5.md` 🚀
