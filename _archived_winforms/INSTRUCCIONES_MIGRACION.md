# Instrucciones de Migración - Credenciales Seguras

## 🚀 Migración Rápida (2 minutos)

### Opción 1: Script Automático (Recomendado)

```bash
cd c:\p2p\SlskDown
migrate_simple.bat
```

**¿Qué hace?**
1. ✅ Lee tu `config.json` actual
2. ✅ Crea `config_secure.json` nuevo
3. ✅ Guarda credenciales temporalmente
4. ✅ Al iniciar SlskDown, se encriptan automáticamente

---

### Opción 2: Manual (Si prefieres hacerlo a mano)

**1. Crear `config_secure.json`:**

```json
{
  "DownloadDirectory": "c:\\p2p\\downloads",
  "SearchTimeout": 450,
  "ResponseLimit": 50,
  "FileLimit": 1000,
  "AutoConnect": true,
  "EncryptedUsername": null,
  "EncryptedPassword": null
}
```

**2. Crear `.credentials_temp`:**

```
carbar
Carlos66*
```

**3. Iniciar SlskDown**
- Las credenciales se encriptarán automáticamente
- El archivo `.credentials_temp` se eliminará

---

## 🔧 Integración en MainForm.cs

### Cambios Mínimos Necesarios

**1. Agregar al inicio del archivo:**

```csharp
using SlskDown.Services;
using SlskDown.UI;
```

**2. Agregar variables de servicios (línea ~85):**

```csharp
// Servicios
private ISecurityService? _securityService;
private IConfigService? _configService;
private ILoggingService? _logger;
private ICacheService? _cache;
```

**3. En el constructor, ANTES de InitializeComponents():**

```csharp
public MainForm()
{
    try
    {
        // Inicializar servicios
        var container = ServiceContainer.Instance;
        _securityService = container.Resolve<ISecurityService>();
        _configService = container.Resolve<IConfigService>();
        _logger = container.Resolve<ILoggingService>();
        _cache = container.Resolve<ICacheService>();
        
        _logger.Info("=== SlskDown Iniciado ===");
        
        // Migrar credenciales si existen temporales
        MigrateCredentialsIfNeeded();
    }
    catch (Exception ex)
    {
        // Si falla, continuar sin servicios (modo legacy)
        Console.WriteLine($"Advertencia: Servicios no disponibles - {ex.Message}");
    }
    
    // Inicializar HashSets de idiomas para búsqueda O(1)
    InitializeLanguageHashSets();
    
    InitializeComponents();
    
    // ... resto del código ...
}
```

**4. Agregar método de migración automática:**

```csharp
private void MigrateCredentialsIfNeeded()
{
    try
    {
        var tempFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".credentials_temp");
        
        if (File.Exists(tempFile))
        {
            var lines = File.ReadAllLines(tempFile);
            if (lines.Length >= 2)
            {
                var username = lines[0].Trim();
                var password = lines[1].Trim();
                
                if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
                {
                    _configService?.SaveCredentials(username, password);
                    _logger?.Info("Credenciales encriptadas automáticamente");
                    
                    // Eliminar archivo temporal
                    File.Delete(tempFile);
                }
            }
        }
    }
    catch (Exception ex)
    {
        _logger?.Error("Error migrando credenciales", ex);
    }
}
```

**5. Modificar LoadConfig() existente:**

```csharp
private void LoadConfig()
{
    try
    {
        // Intentar cargar con servicios nuevos
        if (_configService != null)
        {
            var config = _configService.LoadConfig();
            var (user, pass) = _configService.GetCredentials();
            
            username = user;
            password = pass;
            downloadDir = config.DownloadDirectory;
            defaultSearchTimeoutSecs = config.SearchTimeout;
            
            _logger?.Info($"Configuración cargada - Usuario: {username}");
            return;
        }
    }
    catch (Exception ex)
    {
        _logger?.Error("Error con config nuevo, usando legacy", ex);
    }
    
    // Fallback: Cargar config.json antiguo (modo legacy)
    try
    {
        string configFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
        if (File.Exists(configFile))
        {
            string json = File.ReadAllText(configFile);
            var config = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(json);
            
            if (config != null)
            {
                if (config.TryGetValue("Username", out var u)) username = u.ToString() ?? "carbar";
                if (config.TryGetValue("Password", out var p)) password = p.ToString() ?? "Carlos66*";
                if (config.TryGetValue("DownloadDirectory", out var d)) downloadDir = d.ToString() ?? "c:\\p2p\\downloads";
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error cargando config: {ex.Message}");
        // Usar valores por defecto
        username = "carbar";
        password = "Carlos66*";
        downloadDir = "c:\\p2p\\downloads";
    }
}
```

---

## ✅ Verificación

**1. Ejecutar migración:**
```bash
cd c:\p2p\SlskDown
migrate_simple.bat
```

**2. Compilar:**
```bash
dotnet build
```

**3. Ejecutar SlskDown:**
```bash
bin\Release\net8.0-windows\SlskDown.exe
```

**4. Verificar:**
- ✅ Debe conectar normalmente
- ✅ Debe crear `logs/slskdown-YYYY-MM-DD.txt`
- ✅ Debe eliminar `.credentials_temp`
- ✅ `config_secure.json` debe tener `EncryptedUsername` y `EncryptedPassword` con datos

---

## 🔍 Troubleshooting

### Error: "Servicio no registrado"

**Causa:** Los archivos de Services/ no están compilados

**Solución:**
```bash
dotnet build
```

### Error: "No se puede desencriptar"

**Causa:** Las credenciales fueron encriptadas por otro usuario

**Solución:**
```bash
# Eliminar y recrear
del config_secure.json
migrate_simple.bat
```

### SlskDown no inicia

**Causa:** Error en los servicios

**Solución:** El código tiene fallback a modo legacy, debería funcionar igual

---

## 📊 Comparación

### Antes (config.json)
```json
{
  "Username": "carbar",           ❌ Texto plano
  "Password": "Carlos66*",        ❌ Texto plano
  "DownloadDirectory": "c:\\p2p\\downloads"
}
```

### Después (config_secure.json)
```json
{
  "DownloadDirectory": "c:\\p2p\\downloads",
  "SearchTimeout": 450,
  "ResponseLimit": 50,
  "FileLimit": 1000,
  "AutoConnect": true,
  "EncryptedUsername": "AQAAANCMnd8BFdERjHoAwE...",  ✅ Encriptado
  "EncryptedPassword": "AQAAANCMnd8BFdERjHoAwE..."   ✅ Encriptado
}
```

---

## 🎯 Próximos Pasos

Después de migrar:

1. ✅ Verificar que funciona
2. ✅ Eliminar `config.json` antiguo
3. ✅ Agregar más integraciones (Toast, Validación, etc.)
4. ✅ Seguir `GUIA_INTEGRACION.md` para el resto

---

## 💡 Notas Importantes

- Las credenciales encriptadas **solo funcionan para el usuario actual**
- Si cambias de usuario de Windows, debes migrar de nuevo
- El archivo `.credentials_temp` es temporal y se elimina automáticamente
- Modo legacy: Si los servicios fallan, SlskDown funciona como antes

---

## 🚀 Comando Rápido

```bash
cd c:\p2p\SlskDown && migrate_simple.bat && dotnet build && bin\Release\net8.0-windows\SlskDown.exe
```

**¡Listo!** 🎉
