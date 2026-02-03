# 🔧 Configuración de eMule para SlskDown

Esta guía explica cómo configurar eMule para que funcione con SlskDown.

---

## 📋 **REQUISITOS PREVIOS**

### **Opción 1: eMule (Windows)**
1. Descargar eMule desde: https://www.emule-project.net/
2. Instalar eMule en tu sistema
3. Configurar External Connection (EC)

### **Opción 2: aMule (Multiplataforma - RECOMENDADO)**
1. Descargar aMule desde: http://www.amule.org/
2. Instalar aMule
3. Configurar External Connection (EC)

---

## ⚙️ **CONFIGURACIÓN DE EMULE/AMULE**

### **1. Habilitar External Connection (EC)**

**En eMule:**
1. Abrir eMule
2. Ir a **Preferencias** → **External Connection**
3. Marcar **"Activar External Connection"**
4. Configurar:
   - **Puerto EC**: `4712` (por defecto)
   - **Contraseña EC**: Establecer una contraseña segura
5. Aplicar y reiniciar eMule

**En aMule:**
1. Abrir aMule
2. Ir a **Preferencias** → **External Connections**
3. Marcar **"Accept external connections"**
4. Configurar:
   - **Puerto EC**: `4712` (por defecto)
   - **Contraseña EC**: Establecer una contraseña
5. Aplicar y reiniciar aMule

---

## 🔌 **INTEGRACIÓN CON SLSKDOWN**

### **Configuración en código:**

```csharp
// En MainForm.cs o donde inicialices la aplicación

// 1. Crear cliente eMule
var emuleClient = new EmuleClient(
    host: "127.0.0.1",      // localhost
    port: 4712,             // Puerto EC
    password: "tu_password" // Contraseña EC configurada
);

// 2. Crear proveedores
var emuleSearchProvider = new EmuleSearchProvider(emuleClient);
var emuleDownloadProvider = new EmuleDownloadProvider(emuleClient);

// 3. Registrar en NetworkOrchestrator
networkOrchestrator.RegisterClient("eMule", emuleClient);
networkOrchestrator.RegisterSearchProvider(emuleSearchProvider);
networkOrchestrator.RegisterDownloadProvider(emuleDownloadProvider);

// 4. Conectar
await emuleClient.ConnectAsync();
```

---

## 🧪 **VERIFICAR QUE EMULE FUNCIONA**

### **1. Verificar que eMule está corriendo:**

```cmd
tasklist | findstr /I "emule"
```

Deberías ver algo como:
```
emule.exe                    12345 Console                 1    150,000 K
```

### **2. Verificar puerto EC:**

```cmd
netstat -ano | findstr :4712
```

Deberías ver:
```
TCP    127.0.0.1:4712         0.0.0.0:0              LISTENING       12345
```

### **3. Probar conexión desde SlskDown:**

```csharp
try
{
    var emuleClient = new EmuleClient("127.0.0.1", 4712, "tu_password");
    await emuleClient.ConnectAsync();
    
    if (emuleClient.IsConnected)
    {
        Console.WriteLine("✅ eMule conectado correctamente");
        
        // Probar búsqueda
        var searchId = await emuleClient.SearchAsync("test");
        Console.WriteLine($"✅ Búsqueda iniciada: {searchId}");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Error: {ex.Message}");
}
```

---

## 🚀 **USO EN SLSKDOWN**

Una vez configurado, SlskDown buscará automáticamente en **Soulseek Y eMule** simultáneamente:

```csharp
// Búsqueda multi-red automática
var results = await networkOrchestrator.SearchAsync(new SearchRequest
{
    Query = "tu búsqueda",
    Networks = new[] { "Soulseek", "eMule" } // Buscar en ambas redes
});

// Los resultados incluirán la columna "Red" indicando el origen
foreach (var result in results)
{
    Console.WriteLine($"{result.FileName} - Red: {result.NetworkSource}");
}
```

---

## 🔍 **TROUBLESHOOTING**

### **Error: "Cliente eMule no está conectado"**
- Verificar que eMule/aMule está corriendo
- Verificar que External Connection está habilitado
- Verificar puerto y contraseña

### **Error: "Connection refused"**
- Verificar firewall de Windows
- Verificar que el puerto 4712 no está bloqueado
- Probar con `telnet 127.0.0.1 4712`

### **Error: "Autenticación fallida"**
- Verificar que la contraseña EC es correcta
- Verificar que External Connection acepta conexiones locales

### **eMule no aparece en búsquedas**
- Verificar que eMule está conectado a servidores ed2k
- Verificar que eMule tiene Kad habilitado
- Esperar unos minutos para que eMule se conecte a la red

---

## 📊 **VENTAJAS DE USAR EMULE + SOULSEEK**

| Característica | Soulseek | eMule |
|---------------|----------|-------|
| **Velocidad** | ⭐⭐⭐⭐⭐ | ⭐⭐⭐ |
| **Contenido** | Música, Libros | Todo tipo |
| **Usuarios** | ~100K | ~3M |
| **Disponibilidad** | Media | Alta |
| **Privacidad** | Alta | Media |

**Combinando ambas redes obtienes:**
- ✅ Más resultados
- ✅ Mayor disponibilidad
- ✅ Fuentes alternativas
- ✅ Mejor redundancia

---

## 🎯 **PRÓXIMOS PASOS**

1. ✅ Configurar eMule/aMule con External Connection
2. ✅ Verificar conexión con SlskDown
3. ✅ Realizar búsqueda de prueba
4. ✅ Verificar que aparecen resultados de ambas redes
5. ✅ Disfrutar de búsquedas multi-red

---

## 📝 **NOTAS IMPORTANTES**

- **eMule es más lento** que Soulseek pero tiene más contenido
- **Kad** (red descentralizada) es más confiable que servidores ed2k
- **SlskDown prioriza automáticamente** las fuentes más rápidas
- **La columna "Red"** en la grilla muestra el origen de cada resultado

---

**¿Necesitas ayuda?** Revisa los logs de SlskDown en `logs/` para más detalles.
