# 🎯 Integración: Selector de Redes en Configuración

**Fecha**: 2 de diciembre de 2025, 2:32 PM

---

## ✅ Archivos Creados

1. **✅ `Core/NetworkConfiguration.cs`** - Modelo de configuración
2. **✅ `UI/NetworkConfigurationForm.cs`** - Formulario de configuración

---

## 🔧 Integración en MainForm.cs

### Paso 1: Agregar Variable de Configuración

```csharp
public partial class MainForm : Form
{
    // AGREGAR esta variable
    private NetworkConfiguration _networkConfig;
    
    // Constructor
    public MainForm()
    {
        InitializeComponent();
        
        // AGREGAR: Cargar configuración de redes
        _networkConfig = NetworkConfiguration.Load();
        
        // Aplicar configuración
        ApplyNetworkConfiguration();
    }
}
```

---

### Paso 2: Método para Aplicar Configuración

```csharp
private void ApplyNetworkConfiguration()
{
    // Mostrar modo actual en log
    Log(_networkConfig.GetModeDescription());
    
    // Configurar auto-conexión Soulseek
    if (_networkConfig.SoulseekEnabled && _networkConfig.SoulseekAutoConnect)
    {
        // Auto-conectar a Soulseek
        _ = Task.Run(async () =>
        {
            await Task.Delay(2000); // Esperar 2s
            await ConnectToSoulseek(_networkConfig.SoulseekUsername, 
                                   _networkConfig.SoulseekPassword);
        });
    }
    
    // Configurar auto-conexión eMule
    if (_networkConfig.EMuleEnabled && _networkConfig.EMuleAutoConnect)
    {
        // Auto-conectar a eMule
        _ = Task.Run(async () =>
        {
            await Task.Delay(3000); // Esperar 3s
            await ConnectToEmule();
        });
    }
}
```

---

### Paso 3: Modificar Lógica de Búsqueda

```csharp
private async Task<List<SearchResult>> SearchAsync(string query)
{
    var allResults = new List<SearchResult>();
    
    // Verificar que hay al menos una red activa
    if (!_networkConfig.HasActiveNetworks())
    {
        Log("⚠️ No hay redes activas. Configure al menos una red.");
        return allResults;
    }
    
    // Buscar en Soulseek (si está habilitado)
    if (_networkConfig.SoulseekEnabled)
    {
        if (client != null && client.State.HasFlag(SoulseekClientStates.Connected))
        {
            Log("🔵 Buscando en Soulseek...");
            var soulseekResults = await SearchSoulseekAsync(query);
            allResults.AddRange(soulseekResults);
            Log($"   ✅ {soulseekResults.Count} resultados de Soulseek");
        }
        else
        {
            Log("⚠️ Soulseek habilitado pero no conectado");
        }
    }
    
    // Buscar en eMule (si está habilitado)
    if (_networkConfig.EMuleEnabled)
    {
        if (_networkOrchestrator != null)
        {
            Log("🟢 Buscando en eMule...");
            var emuleResults = await SearchEmuleAsync(query);
            allResults.AddRange(emuleResults);
            Log($"   ✅ {emuleResults.Count} resultados de eMule");
        }
        else
        {
            Log("⚠️ eMule habilitado pero no conectado");
        }
    }
    
    return allResults;
}
```

---

### Paso 4: Agregar Botón de Configuración

```csharp
// En el diseñador o en InitializeComponent()
private Button btnNetworkConfig;

private void InitializeNetworkConfigButton()
{
    btnNetworkConfig = new Button
    {
        Text = "⚙️ Configurar Redes",
        Location = new Point(20, 20),
        Size = new Size(150, 35)
    };
    btnNetworkConfig.Click += BtnNetworkConfig_Click;
    
    // Agregar al formulario o panel de configuración
    this.Controls.Add(btnNetworkConfig);
}

private void BtnNetworkConfig_Click(object sender, EventArgs e)
{
    using var configForm = new NetworkConfigurationForm();
    if (configForm.ShowDialog() == DialogResult.OK)
    {
        // Recargar configuración
        _networkConfig = NetworkConfiguration.Load();
        ApplyNetworkConfiguration();
        
        Log("✅ Configuración de redes actualizada");
        Log(_networkConfig.GetModeDescription());
    }
}
```

---

### Paso 5: Mostrar Estado de Redes en UI

```csharp
private void UpdateNetworkStatus()
{
    // Actualizar label de estado
    if (lblNetworkStatus != null)
    {
        lblNetworkStatus.Text = _networkConfig.GetModeDescription();
        
        // Cambiar color según modo
        if (_networkConfig.SoulseekEnabled && _networkConfig.EMuleEnabled)
            lblNetworkStatus.ForeColor = Color.Green;
        else if (_networkConfig.SoulseekEnabled)
            lblNetworkStatus.ForeColor = Color.Blue;
        else if (_networkConfig.EMuleEnabled)
            lblNetworkStatus.ForeColor = Color.DarkGreen;
        else
            lblNetworkStatus.ForeColor = Color.Red;
    }
}
```

---

## 🎨 Ejemplo de Uso Completo

```csharp
public partial class MainForm : Form
{
    private NetworkConfiguration _networkConfig;
    private Button btnNetworkConfig;
    private Label lblNetworkStatus;
    
    public MainForm()
    {
        InitializeComponent();
        InitializeNetworkConfiguration();
    }
    
    private void InitializeNetworkConfiguration()
    {
        // 1. Cargar configuración
        _networkConfig = NetworkConfiguration.Load();
        
        // 2. Crear botón de configuración
        btnNetworkConfig = new Button
        {
            Text = "⚙️ Redes",
            Location = new Point(20, 20),
            Size = new Size(100, 30)
        };
        btnNetworkConfig.Click += BtnNetworkConfig_Click;
        
        // 3. Crear label de estado
        lblNetworkStatus = new Label
        {
            Location = new Point(130, 25),
            Size = new Size(300, 20),
            Font = new Font(this.Font, FontStyle.Bold)
        };
        
        // 4. Agregar a UI
        this.Controls.Add(btnNetworkConfig);
        this.Controls.Add(lblNetworkStatus);
        
        // 5. Aplicar configuración
        ApplyNetworkConfiguration();
        UpdateNetworkStatus();
    }
    
    private void BtnNetworkConfig_Click(object sender, EventArgs e)
    {
        using var configForm = new NetworkConfigurationForm();
        if (configForm.ShowDialog() == DialogResult.OK)
        {
            _networkConfig = NetworkConfiguration.Load();
            ApplyNetworkConfiguration();
            UpdateNetworkStatus();
        }
    }
    
    private void ApplyNetworkConfiguration()
    {
        Log(_networkConfig.GetModeDescription());
        
        // Auto-conectar según configuración
        if (_networkConfig.SoulseekEnabled && _networkConfig.SoulseekAutoConnect)
        {
            _ = ConnectToSoulseekAsync();
        }
        
        if (_networkConfig.EMuleEnabled && _networkConfig.EMuleAutoConnect)
        {
            _ = ConnectToEmuleAsync();
        }
    }
    
    private void UpdateNetworkStatus()
    {
        lblNetworkStatus.Text = _networkConfig.GetModeDescription();
        
        if (_networkConfig.SoulseekEnabled && _networkConfig.EMuleEnabled)
            lblNetworkStatus.ForeColor = Color.Green;
        else if (_networkConfig.SoulseekEnabled)
            lblNetworkStatus.ForeColor = Color.Blue;
        else if (_networkConfig.EMuleEnabled)
            lblNetworkStatus.ForeColor = Color.DarkGreen;
        else
            lblNetworkStatus.ForeColor = Color.Red;
    }
    
    private async void btnSearch_Click(object sender, EventArgs e)
    {
        var query = txtSearch.Text;
        var results = new List<SearchResult>();
        
        // Buscar según redes habilitadas
        if (_networkConfig.SoulseekEnabled)
        {
            var soulseekResults = await SearchSoulseekAsync(query);
            results.AddRange(soulseekResults);
        }
        
        if (_networkConfig.EMuleEnabled)
        {
            var emuleResults = await SearchEmuleAsync(query);
            results.AddRange(emuleResults);
        }
        
        DisplayResults(results);
    }
}
```

---

## 📊 Flujo de Usuario

### Escenario 1: Solo Soulseek

```
1. Usuario: Clic en "⚙️ Redes"
2. Sistema: Abre NetworkConfigurationForm
3. Usuario: 
   - ✅ Habilitar Soulseek
   - ❌ Deshabilitar eMule
   - Ingresar usuario/contraseña
   - Clic "Guardar"
4. Sistema: 
   - Guarda configuración
   - Muestra "🔵 Modo: Solo Soulseek"
   - Solo busca en Soulseek
```

### Escenario 2: Solo eMule

```
1. Usuario: Clic en "⚙️ Redes"
2. Sistema: Abre NetworkConfigurationForm
3. Usuario:
   - ❌ Deshabilitar Soulseek
   - ✅ Habilitar eMule
   - Ingresar contraseña EC
   - Clic "Guardar"
4. Sistema:
   - Guarda configuración
   - Muestra "🟢 Modo: Solo eMule"
   - Solo busca en eMule
```

### Escenario 3: Ambas Redes

```
1. Usuario: Clic en "⚙️ Redes"
2. Sistema: Abre NetworkConfigurationForm
3. Usuario:
   - ✅ Habilitar Soulseek
   - ✅ Habilitar eMule
   - Configurar ambas
   - Clic "Guardar"
4. Sistema:
   - Guarda configuración
   - Muestra "🌐 Modo: Multi-Red (Soulseek + eMule)"
   - Busca en ambas redes
```

---

## 🎯 Características del Formulario

### Validaciones:
- ✅ Al menos una red debe estar habilitada
- ✅ Soulseek requiere usuario
- ✅ eMule requiere contraseña EC
- ✅ Puerto válido (1-65535)

### Funciones:
- ✅ Guardar/Cargar configuración automáticamente
- ✅ Probar conexión antes de guardar
- ✅ Mostrar modo actual en tiempo real
- ✅ Auto-conectar al iniciar (opcional)
- ✅ Configurar caché
- ✅ Red preferida

---

## 📁 Ubicación del Archivo de Configuración

```
Windows:
C:\Users\[Usuario]\AppData\Roaming\SlskDown\network_config.json

Contenido ejemplo:
{
  "SoulseekEnabled": true,
  "EMuleEnabled": true,
  "SoulseekUsername": "mi_usuario",
  "SoulseekPassword": "mi_contraseña",
  "SoulseekAutoConnect": true,
  "EMuleHost": "localhost",
  "EMulePort": 4712,
  "EMulePassword": "mi_contraseña_ec",
  "EMuleAutoConnect": true,
  "PreferredNetwork": "Both",
  "SearchTimeoutSeconds": 30,
  "UseCache": true,
  "CacheExpirationMinutes": 30
}
```

---

## ✅ Checklist de Integración

### Paso 1: Archivos
- [x] NetworkConfiguration.cs creado
- [x] NetworkConfigurationForm.cs creado

### Paso 2: MainForm
- [ ] Agregar variable `_networkConfig`
- [ ] Cargar configuración en constructor
- [ ] Agregar botón "Configurar Redes"
- [ ] Agregar label de estado
- [ ] Implementar `ApplyNetworkConfiguration()`
- [ ] Modificar lógica de búsqueda

### Paso 3: Compilar
- [ ] Compilar proyecto
- [ ] Verificar sin errores

### Paso 4: Probar
- [ ] Abrir configuración de redes
- [ ] Probar solo Soulseek
- [ ] Probar solo eMule
- [ ] Probar ambas redes
- [ ] Verificar persistencia

---

## 🎁 Beneficios

### Para el Usuario:
- ✅ Control total sobre qué redes usar
- ✅ Configuración visual intuitiva
- ✅ Prueba de conexión integrada
- ✅ Auto-conexión opcional
- ✅ Configuración persiste

### Para el Desarrollador:
- ✅ Código limpio y modular
- ✅ Fácil agregar más redes
- ✅ Configuración centralizada
- ✅ Validación automática

---

**¡Sistema de configuración de redes implementado!** 🎉

**Ahora el usuario puede elegir:**
- 🔵 Solo Soulseek
- 🟢 Solo eMule
- 🌐 Ambas redes

**Todo desde una interfaz visual clara y fácil de usar.** ✨
