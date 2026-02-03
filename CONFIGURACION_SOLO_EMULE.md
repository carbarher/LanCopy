# 🟢 Configuración: Solo eMule (Sin Soulseek)

**Fecha**: 2 de diciembre de 2025, 1:28 PM

---

## ✅ Sí, Se Puede Usar Solo eMule

El código multi-red está diseñado para funcionar con cualquier combinación de redes:
- ✅ Solo Soulseek
- ✅ Solo eMule
- ✅ Ambas redes
- ✅ Más redes en el futuro

---

## 🎯 Opción 1: Configuración en UI (Más Fácil)

### Cuando Reinicies SlskDown:

1. **No conectar a Soulseek**
   - Simplemente no ingresar credenciales
   - O no hacer clic en "Conectar"

2. **Activar solo eMule**
   - Ir a Configuración
   - Activar "Habilitar eMule/ed2k"
   - Ingresar contraseña EC
   - Reiniciar

3. **Resultado**:
   ```
   🟢 eMule: Activo
   ⚪ Soulseek: Inactivo
   → Solo búsquedas en eMule
   ```

---

## 🎯 Opción 2: Modificación de Código (Permanente)

### En `NetworkOrchestrator.cs`:

```csharp
public async Task<MultiNetworkSearchResponse> SearchAsync(
    SearchRequest request,
    IEnumerable<string> networks = null,
    CancellationToken cancellationToken = default)
{
    // MODIFICAR: Solo usar eMule
    var activeNetworks = networks?.ToList() ?? new List<string> { "eMule" };
    
    // Filtrar para usar solo eMule
    activeNetworks = activeNetworks.Where(n => n == "eMule").ToList();
    
    if (!activeNetworks.Any())
    {
        activeNetworks = new List<string> { "eMule" };
    }
    
    // Resto del código igual...
}
```

---

## 🎯 Opción 3: Archivo de Configuración

### Crear `appsettings.json`:

```json
{
  "Networks": {
    "Soulseek": {
      "Enabled": false,
      "AutoConnect": false
    },
    "eMule": {
      "Enabled": true,
      "AutoConnect": true,
      "Host": "localhost",
      "Port": 4712,
      "Password": "tu_contraseña"
    }
  },
  "Search": {
    "DefaultNetwork": "eMule",
    "OnlyUseEnabled": true
  }
}
```

### Leer configuración en código:

```csharp
public class AppConfig
{
    public bool SoulseekEnabled { get; set; } = false;
    public bool EMuleEnabled { get; set; } = true;
    
    public static AppConfig Load()
    {
        var json = File.ReadAllText("appsettings.json");
        return JsonSerializer.Deserialize<AppConfig>(json);
    }
}
```

---

## 🔧 Implementación Rápida

### Agregar Variable de Control en `MainForm.cs`:

```csharp
public partial class MainForm : Form
{
    // AGREGAR ESTAS VARIABLES
    private bool _useSoulseek = false;  // ← Cambiar a false
    private bool _useEmule = true;      // ← Cambiar a true
    
    private async Task<List<SearchResult>> SearchAsync(string query)
    {
        var results = new List<SearchResult>();
        
        // Solo buscar en Soulseek si está habilitado
        if (_useSoulseek && client != null && client.State.HasFlag(SoulseekClientStates.Connected))
        {
            var soulseekResults = await SearchSoulseekAsync(query);
            results.AddRange(soulseekResults);
        }
        
        // Solo buscar en eMule si está habilitado
        if (_useEmule && _networkOrchestrator != null)
        {
            var emuleResults = await SearchEmuleAsync(query);
            results.AddRange(emuleResults);
        }
        
        return results;
    }
}
```

---

## 🎨 UI Solo para eMule

### Ocultar Controles de Soulseek:

```csharp
private void ConfigureForEmuleOnly()
{
    // Ocultar controles de Soulseek
    grpSoulseek.Visible = false;
    btnConnectSoulseek.Visible = false;
    txtSoulseekUsername.Visible = false;
    txtSoulseekPassword.Visible = false;
    
    // Mostrar solo controles de eMule
    grpEmule.Visible = true;
    chkEnableEmule.Checked = true;
    chkEnableEmule.Enabled = false; // No permitir desactivar
    
    // Actualizar título
    this.Text = "SlskDown - eMule Edition";
}
```

---

## 📊 Ventajas de Solo eMule

### Ventajas:
- ✅ Más simple (una sola red)
- ✅ No necesita credenciales Soulseek
- ✅ Menos uso de recursos
- ✅ Enfoque en red ed2k
- ✅ Ideal para archivos grandes

### Desventajas:
- ⚠️ Menos resultados (solo una red)
- ⚠️ eMule puede ser más lento que Soulseek
- ⚠️ Menos fuentes disponibles

---

## 🚀 Ejemplo Completo: Solo eMule

```csharp
public class SlskDownEmuleOnly : Form
{
    private EMuleClient _emuleClient;
    private EMuleConnectionPool _emulePool;
    private PersistentCache _cache;
    
    public SlskDownEmuleOnly()
    {
        InitializeComponent();
        InitializeEmuleOnly();
    }
    
    private void InitializeEmuleOnly()
    {
        // Solo eMule, sin Soulseek
        _emulePool = new EMuleConnectionPool(
            host: "localhost",
            port: 4712,
            password: GetEmulePassword(),
            maxConnections: 5
        );
        
        _cache = new PersistentCache();
        
        Log("🟢 Modo: Solo eMule");
        Log("✅ eMule inicializado");
    }
    
    private async void btnSearch_Click(object sender, EventArgs e)
    {
        var query = txtSearch.Text;
        
        // Intentar caché
        var results = _cache.Get(query);
        if (results != null)
        {
            Log($"⚡ {results.Count} resultados desde caché");
            DisplayResults(results);
            return;
        }
        
        // Buscar solo en eMule
        Log("🔍 Buscando en eMule...");
        
        using var connection = await _emulePool.GetConnectionAsync();
        results = await connection.Client.SearchAsync(query);
        
        // Guardar en caché
        _cache.Set(query, results);
        
        // Mostrar resultados
        DisplayResults(results);
        Log($"✅ {results.Count} resultados de eMule");
    }
    
    private void DisplayResults(List<SearchResult> results)
    {
        dgvResults.Rows.Clear();
        foreach (var result in results)
        {
            dgvResults.Rows.Add(
                "🟢", // Icono eMule
                result.FileName,
                FormatFileSize(result.SizeBytes),
                result.Username
            );
        }
    }
}
```

---

## 🎯 Configuración Recomendada

### Para Probar Solo eMule:

```csharp
// En MainForm.cs o archivo de configuración

public class NetworkConfig
{
    // CONFIGURACIÓN SOLO EMULE
    public static readonly bool USE_SOULSEEK = false;
    public static readonly bool USE_EMULE = true;
    
    // Configuración eMule
    public static readonly string EMULE_HOST = "localhost";
    public static readonly int EMULE_PORT = 4712;
    public static readonly string EMULE_PASSWORD = "tu_contraseña";
    
    // Optimizaciones
    public static readonly int EMULE_MAX_CONNECTIONS = 5;
    public static readonly int SEARCH_TIMEOUT_SECONDS = 30;
    public static readonly int CACHE_EXPIRATION_MINUTES = 30;
}
```

---

## 📋 Checklist: Configurar Solo eMule

### Opción Rápida (Sin Código):
- [ ] No conectar a Soulseek
- [ ] Activar solo eMule en configuración
- [ ] Listo para usar

### Opción Código (Permanente):
- [ ] Establecer `_useSoulseek = false`
- [ ] Establecer `_useEmule = true`
- [ ] Ocultar controles de Soulseek (opcional)
- [ ] Compilar proyecto
- [ ] Probar búsquedas solo en eMule

---

## 🧪 Probar Solo eMule

### Pasos:

1. **Asegurar eMule está corriendo**:
   ```cmd
   netstat -ano | findstr :4712
   ```
   Debe mostrar: `LISTENING`

2. **Iniciar SlskDown**:
   - No conectar a Soulseek
   - Solo activar eMule

3. **Buscar algo**:
   ```
   Buscar: "machine learning"
   Esperado: Resultados solo de eMule (🟢)
   ```

4. **Verificar logs**:
   ```
   🔍 Buscando en eMule...
   ✅ 15 resultados de eMule
   ```

---

## 📊 Comparación

### Solo Soulseek:
```
Velocidad: ⭐⭐⭐⭐⭐ (muy rápido)
Resultados: ⭐⭐⭐⭐ (muchos)
Archivos grandes: ⭐⭐⭐ (limitado)
```

### Solo eMule:
```
Velocidad: ⭐⭐⭐ (moderado)
Resultados: ⭐⭐⭐ (buenos)
Archivos grandes: ⭐⭐⭐⭐⭐ (excelente)
```

### Ambas Redes:
```
Velocidad: ⭐⭐⭐⭐ (rápido)
Resultados: ⭐⭐⭐⭐⭐ (máximo)
Archivos grandes: ⭐⭐⭐⭐⭐ (excelente)
```

---

## ✅ Conclusión

**Sí, puedes usar SlskDown solo con eMule/ed2k sin Soulseek.**

### Opciones:
1. **Más fácil**: No conectar a Soulseek, solo activar eMule
2. **Permanente**: Modificar código para deshabilitar Soulseek
3. **Configurable**: Usar archivo de configuración

### Recomendación:
- **Para probar**: Opción 1 (más fácil)
- **Para producción**: Opción 2 o 3 (más limpio)

---

**¿Quieres que modifique el código para que sea solo eMule por defecto?** 🟢
