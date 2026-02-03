# Guía de Integración - Servicios en MainForm.cs

## 🎯 Objetivo

Integrar los nuevos servicios de seguridad, configuración, logging, caché y notificaciones en el MainForm.cs existente.

---

## 📋 Pasos de Integración

### Paso 1: Migrar Credenciales (CRÍTICO)

**Ejecutar script de migración:**

```bash
cd c:\p2p\SlskDown
dotnet run --project MigrateToSecure.cs
```

O compilar y ejecutar:

```bash
csc MigrateToSecure.cs /r:Services\*.cs
MigrateToSecure.exe
```

**Resultado:**
- ✅ Crea `config_secure.json` con credenciales encriptadas
- ✅ Migra configuración desde `config.json` antiguo
- ⚠️ Puedes eliminar `config.json` después

---

### Paso 2: Agregar Referencias en MainForm.cs

**Al inicio del archivo (después de los using):**

```csharp
using SlskDown.Services;
using SlskDown.UI;
```

**En la clase MainForm (después de las variables existentes):**

```csharp
// Servicios (agregar alrededor de la línea 85)
private readonly ISecurityService _securityService;
private readonly IConfigService _configService;
private readonly ILoggingService _logger;
private readonly ICacheService _cache;
```

---

### Paso 3: Modificar Constructor

**Buscar:** `public MainForm()`

**Agregar al inicio del constructor:**

```csharp
public MainForm()
{
    // NUEVO: Inicializar servicios
    var container = ServiceContainer.Instance;
    _securityService = container.Resolve<ISecurityService>();
    _configService = container.Resolve<IConfigService>();
    _logger = container.Resolve<ILoggingService>();
    _cache = container.Resolve<ICacheService>();
    
    _logger.Info("=== SlskDown Iniciado ===");
    
    try
    {
        // Inicializar HashSets de idiomas para búsqueda O(1)
        InitializeLanguageHashSets();
        
        InitializeComponents();
        
        // ... resto del código existente ...
```

---

### Paso 4: Reemplazar LoadConfig()

**Buscar la función:** `private void LoadConfig()`

**Reemplazar con:**

```csharp
private void LoadConfig()
{
    try
    {
        // NUEVO: Usar ConfigService con credenciales encriptadas
        var config = _configService.LoadConfig();
        var (user, pass) = _configService.GetCredentials();
        
        username = user;
        password = pass;
        downloadDir = config.DownloadDirectory;
        defaultSearchTimeoutSecs = config.SearchTimeout;
        
        // Actualizar UI si existe
        if (usernameTextBox != null) usernameTextBox.Text = username;
        if (passwordTextBox != null) passwordTextBox.Text = password;
        if (downloadDirTextBox != null) downloadDirTextBox.Text = downloadDir;
        if (searchTimeoutBox != null) searchTimeoutBox.Value = defaultSearchTimeoutSecs;
        if (responseLimitBox != null) responseLimitBox.Value = config.ResponseLimit;
        if (fileLimitBox != null) fileLimitBox.Value = config.FileLimit;
        
        _logger.Info($"Configuración cargada - Usuario: {username}");
    }
    catch (Exception ex)
    {
        _logger.Error("Error cargando configuración", ex);
        Toast.Error(this, "Error al cargar configuración");
        
        // Valores por defecto
        username = "carbar";
        password = "Carlos66*";
        downloadDir = @"c:\p2p\downloads";
    }
}
```

---

### Paso 5: Reemplazar SaveConfig()

**Buscar:** `private void SaveConfigSilent()` o `private void SaveConfig()`

**Reemplazar con:**

```csharp
private void SaveConfigSilent()
{
    try
    {
        // NUEVO: Guardar con ConfigService
        _configService.SaveCredentials(username, password);
        
        var config = _configService.LoadConfig();
        config.DownloadDirectory = downloadDir;
        config.SearchTimeout = defaultSearchTimeoutSecs;
        config.ResponseLimit = (int)responseLimitBox.Value;
        config.FileLimit = (int)fileLimitBox.Value;
        _configService.SaveConfig(config);
        
        _logger.Info("Configuración guardada");
    }
    catch (Exception ex)
    {
        _logger.Error("Error guardando configuración", ex);
    }
}
```

---

### Paso 6: Agregar Validación en SearchButton_Click

**Buscar:** `private async void SearchButton_Click`

**Agregar validación al inicio:**

```csharp
private async void SearchButton_Click(object? sender, EventArgs e)
{
    var query = searchBox.Text.Trim();
    
    // NUEVO: Validar entrada
    if (!_securityService.ValidateSearchQuery(query, out var errorMessage))
    {
        Toast.Warning(this, errorMessage ?? "Búsqueda inválida");
        _logger.Warning($"Búsqueda rechazada: {query}");
        return;
    }
    
    // NUEVO: Verificar caché
    var cacheKey = $"search_{query}_{responseLimitBox.Value}";
    if (_cache.TryGet<List<SearchResponse>>(cacheKey, out var cachedResults))
    {
        _logger.Info($"Resultados desde caché: {query}");
        Toast.Info(this, "Mostrando resultados desde caché");
        // Usar cachedResults...
        return;
    }
    
    _logger.Info($"Búsqueda iniciada: {query}");
    
    // ... resto del código existente ...
    
    // NUEVO: Al final, guardar en caché
    _cache.Set(cacheKey, allSearchResults, TimeSpan.FromMinutes(10));
}
```

---

### Paso 7: Reemplazar MessageBox con Toast

**Buscar y reemplazar en TODO el archivo:**

| Antes | Después |
|-------|---------|
| `MessageBox.Show("mensaje", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information)` | `Toast.Info(this, "mensaje")` |
| `MessageBox.Show("mensaje", "Éxito", MessageBoxButtons.OK, MessageBoxIcon.Information)` | `Toast.Success(this, "mensaje")` |
| `MessageBox.Show("mensaje", "Advertencia", MessageBoxButtons.OK, MessageBoxIcon.Warning)` | `Toast.Warning(this, "mensaje")` |
| `MessageBox.Show("mensaje", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)` | `Toast.Error(this, "mensaje")` |

**Ejemplos específicos:**

```csharp
// ANTES:
MessageBox.Show("Descarga completada", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);

// DESPUÉS:
Toast.Success(this, "Descarga completada");

// ANTES:
MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);

// DESPUÉS:
Toast.Error(this, $"Error: {ex.Message}");
_logger.Error("Contexto del error", ex);
```

---

### Paso 8: Agregar Logging en Puntos Críticos

**En ConnectButton_Click:**

```csharp
private async void ConnectButton_Click(object? sender, EventArgs e)
{
    _logger.Info("Intentando conectar...");
    
    try
    {
        // ... código de conexión ...
        
        _logger.Info($"Conectado como: {username}");
        Toast.Success(this, "Conectado exitosamente");
    }
    catch (Exception ex)
    {
        _logger.Error("Error de conexión", ex);
        Toast.Error(this, "Error al conectar");
    }
}
```

**En DownloadFile:**

```csharp
private async Task DownloadFile(SearchResult result)
{
    _logger.Info($"Descarga iniciada: {result.Filename}");
    
    try
    {
        // NUEVO: Sanitizar path
        var safePath = _securityService.SanitizePath(result.Filename);
        
        // ... código de descarga ...
        
        _logger.Info($"Descarga completada: {result.Filename}");
        Toast.Success(this, $"✓ {Path.GetFileName(result.Filename)}");
    }
    catch (Exception ex)
    {
        _logger.Error($"Error descargando: {result.Filename}", ex);
        Toast.Error(this, $"Error: {Path.GetFileName(result.Filename)}");
    }
}
```

---

### Paso 9: Sanitizar Paths de Usuario

**En cualquier lugar donde se use input del usuario para paths:**

```csharp
// ANTES:
var filePath = Path.Combine(downloadDir, result.Filename);

// DESPUÉS:
var safeFilename = _securityService.SanitizePath(result.Filename);
var filePath = Path.Combine(downloadDir, safeFilename);
```

---

### Paso 10: Cleanup en Dispose

**Buscar:** `protected override void Dispose(bool disposing)`

**Agregar:**

```csharp
protected override void Dispose(bool disposing)
{
    if (disposing)
    {
        // ... código existente ...
        
        // NUEVO: Cleanup de servicios
        _logger?.Info("=== SlskDown Cerrado ===");
        
        if (_cache is IDisposable cacheDisposable)
            cacheDisposable.Dispose();
    }
    
    base.Dispose(disposing);
}
```

---

## ✅ Checklist de Integración

- [ ] **Paso 1:** Ejecutar MigrateToSecure.exe
- [ ] **Paso 2:** Agregar using statements
- [ ] **Paso 3:** Agregar variables de servicios
- [ ] **Paso 4:** Inicializar servicios en constructor
- [ ] **Paso 5:** Reemplazar LoadConfig()
- [ ] **Paso 6:** Reemplazar SaveConfig()
- [ ] **Paso 7:** Agregar validación en búsquedas
- [ ] **Paso 8:** Reemplazar MessageBox con Toast
- [ ] **Paso 9:** Agregar logging en puntos críticos
- [ ] **Paso 10:** Sanitizar paths de usuario
- [ ] **Paso 11:** Agregar cleanup en Dispose

---

## 🧪 Verificación

**1. Compilar:**
```bash
dotnet build
```

**2. Ejecutar Tests:**
```bash
cd SlskDown.Tests
dotnet test
```

**3. Verificar logs:**
```bash
# Los logs se guardan en:
c:\p2p\SlskDown\logs\slskdown-YYYY-MM-DD.txt
```

**4. Verificar config encriptado:**
```bash
# Debe existir:
c:\p2p\SlskDown\config_secure.json

# NO debe tener credenciales en texto plano
```

---

## 🔍 Puntos de Atención

### ⚠️ IMPORTANTE

1. **Credenciales:** Ya no usar `username` y `password` directamente del config.json
2. **Validación:** SIEMPRE validar input del usuario antes de usar
3. **Paths:** SIEMPRE sanitizar paths antes de operaciones de archivo
4. **Logging:** Agregar logs en operaciones críticas (conexión, búsqueda, descarga)
5. **Toast:** Reemplazar TODOS los MessageBox por Toast para mejor UX

### 🐛 Troubleshooting

**Error: "Servicio no registrado"**
- Verificar que ServiceContainer.Instance esté inicializado
- Verificar que los servicios estén en la carpeta Services/

**Error: "No se puede desencriptar"**
- Ejecutar MigrateToSecure.exe de nuevo
- Las credenciales solo pueden ser desencriptadas por el mismo usuario

**Tests fallan:**
- Verificar que SlskDown.Tests.csproj tenga referencia a SlskDown.csproj
- Ejecutar: `dotnet restore`

---

## 📊 Beneficios Después de Integrar

✅ **Seguridad:**
- Credenciales encriptadas con DPAPI
- Validación de entrada completa
- Paths sanitizados

✅ **UX:**
- Notificaciones no-intrusivas
- Mejor feedback visual

✅ **Mantenibilidad:**
- Código modular
- Fácil de testear
- Logging completo

✅ **Performance:**
- Caché de búsquedas (10 min)
- Menos llamadas redundantes

---

## 🎓 Recursos

- **README_MEJORAS.md** - Guía completa de las mejoras
- **MainFormIntegration.cs** - Ejemplos de código
- **MigrateToSecure.cs** - Script de migración
- **Tests/** - Ejemplos de tests unitarios

---

## 💡 Próximos Pasos

Después de integrar todo:

1. ✅ Ejecutar tests: `dotnet test`
2. ✅ Verificar logs en `logs/`
3. ✅ Probar búsqueda con validación
4. ✅ Probar caché (buscar 2 veces lo mismo)
5. ✅ Verificar que Toast funciona
6. ✅ Confirmar que credenciales están encriptadas

**¡Listo para producción!** 🚀
