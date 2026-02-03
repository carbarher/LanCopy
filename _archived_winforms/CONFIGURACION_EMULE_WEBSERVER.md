# 🌐 Configuración de eMule con Servidor Web

## ✅ **TU CONFIGURACIÓN ACTUAL**

Según tu captura de pantalla, tienes:
- ✅ Servidor Web: **Activado**
- ✅ Puerto: **4711**
- ✅ Contraseña: **Configurada**

**¡Esto es suficiente para SlskDown!**

---

## 🔌 **CÓMO CONECTAR SLSKDOWN**

### **Código de integración:**

```csharp
// Usar EmuleWebClient en lugar de EmuleClient
var emuleClient = new EmuleWebClient(
    host: "127.0.0.1",
    port: 4711,              // Puerto del Servidor Web
    password: "tu_password"  // La contraseña que configuraste
);

// Conectar
await emuleClient.ConnectAsync();

if (emuleClient.IsConnected)
{
    Console.WriteLine("✅ eMule conectado vía WebServer");
    
    // Registrar proveedores
    var emuleSearchProvider = new EmuleSearchProvider(emuleClient);
    var emuleDownloadProvider = new EmuleDownloadProvider(emuleClient);
    
    networkOrchestrator.RegisterSearchProvider(emuleSearchProvider);
    networkOrchestrator.RegisterDownloadProvider(emuleDownloadProvider);
}
```

---

## 🧪 **PROBAR LA CONEXIÓN**

### **Opción 1: Desde navegador**
Abre en tu navegador:
```
http://127.0.0.1:4711
```

Deberías ver la interfaz web de eMule. Si pide contraseña, usa la que configuraste.

### **Opción 2: Desde SlskDown**
Ejecuta el código de arriba y verifica que aparece:
```
✅ eMule conectado vía WebServer
```

---

## 📊 **DIFERENCIAS CON EXTERNAL CONNECTION**

| Característica | External Connection | Servidor Web |
|---------------|---------------------|--------------|
| **Puerto** | 4712 | 4711 |
| **Protocolo** | Binario (EC) | HTTP |
| **Velocidad** | Más rápido | Más lento |
| **Compatibilidad** | aMule | eMule + aMule |
| **Para SlskDown** | ✅ Funciona | ✅ Funciona |

**Ambos funcionan igual para SlskDown**, solo cambia el cliente que usas.

---

## ⚠️ **IMPORTANTE**

- Mantén eMule **abierto y conectado** a servidores
- La contraseña debe ser la misma en eMule y en el código
- El puerto 4711 debe estar abierto (solo localhost, no internet)

---

## 🎯 **PRÓXIMO PASO**

Ahora solo necesitas:
1. Compilar SlskDown con el nuevo `EmuleWebClient`
2. Ejecutar y probar la conexión
3. ¡Disfrutar de búsquedas multi-red!
