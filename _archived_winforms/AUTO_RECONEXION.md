# Sistema de Auto-Reconexión

## Resumen

Se ha implementado un sistema robusto de auto-reconexión que detecta desconexiones del servidor de Soulseek y reconecta automáticamente sin intervención del usuario.

---

## 🔄 Mecanismos de Detección

### **1. Evento `Disconnected` del Cliente** (líneas 2400-2418)

El cliente de Soulseek dispara un evento cuando se desconecta:

```csharp
client.Disconnected += async (sender, args) =>
{
    Log($"⚠️ Desconexión detectada: {args.Message}");
    AutoLog($"🔴 DESCONECTADO DE SOULSEEK - Razón: {args.Message}");
    
    // Actualizar UI
    SafeBeginInvoke(() =>
    {
        lblStatus.Text = "Desconectado";
        lblStatus.ForeColor = Color.Red;
    });
    
    // Activar auto-reconexión
    if (autoReconnect && !isReconnecting)
    {
        AutoLog("🔄 Iniciando auto-reconexión...");
        await Task.Delay(5000); // Esperar 5 segundos
        _ = Task.Run(async () => await CheckAndReconnect());
    }
};
```

**Ventajas:**
- ✅ Detección inmediata de desconexión
- ✅ Captura la razón de la desconexión
- ✅ Espera 5 segundos antes de reconectar (evita loops)

---

### **2. Timer de Verificación Periódica** (líneas 11019-11035)

Un timer verifica el estado del cliente cada minuto:

```csharp
var reconnectCheckTimer = new System.Windows.Forms.Timer { Interval = 60000 }; // Cada minuto
reconnectCheckTimer.Tick += async (s, e) =>
{
    if (autoReconnect && client != null)
    {
        // Verificar si el cliente está desconectado
        if (client.State == SoulseekClientStates.Disconnected || 
            (!client.State.HasFlag(SoulseekClientStates.Connected) && 
             !client.State.HasFlag(SoulseekClientStates.LoggedIn)))
        {
            AutoLog($"⚠️ Cliente desconectado detectado (Estado: {client.State})");
            _ = Task.Run(async () => await CheckAndReconnect());
        }
    }
};
```

**Ventajas:**
- ✅ Detecta desconexiones silenciosas (sin evento)
- ✅ Verifica estados inválidos
- ✅ Backup si el evento `Disconnected` falla

---

### **3. Monitor de Conexión de Red** (líneas 10994-11009)

Detecta cuando la conexión de red se restaura:

```csharp
connectionMonitor.ConnectionRestored += async () =>
{
    SafeBeginInvoke(() =>
    {
        AutoLog("🌐 Conexión restaurada - reanudando");
        lblConnectionQuality.Text = "🟢 Conectado";
        lblConnectionQuality.ForeColor = Color.LimeGreen;
    });
    
    // Intentar reconectar si es necesario
    if (autoReconnect)
    {
        await Task.Delay(2000);
        await CheckAndReconnect();
    }
};
```

**Ventajas:**
- ✅ Detecta pérdida de conexión a Internet
- ✅ Reconecta cuando la red vuelve
- ✅ Monitorea calidad de conexión

---

## 🔧 Lógica de Reconexión

### **Método `CheckAndReconnect()`** (líneas 11176-11350)

```csharp
private async Task CheckAndReconnect()
{
    // 1. Protección contra reconexiones simultáneas
    lock (this)
    {
        if (!autoReconnect || isReconnecting || isConnecting)
            return;
        isReconnecting = true;
    }
    
    try
    {
        // 2. Verificar si necesita reconexión
        bool needsReconnect = false;
        string reason = "";
        
        if (client == null)
        {
            needsReconnect = true;
            reason = "Cliente nulo";
        }
        else if (client.State == SoulseekClientStates.Disconnected)
        {
            needsReconnect = true;
            reason = "Desconectado";
        }
        else if (!client.State.HasFlag(SoulseekClientStates.Connected) && 
                 !client.State.HasFlag(SoulseekClientStates.LoggedIn))
        {
            needsReconnect = true;
            reason = $"Estado inválido: {client.State}";
        }
        
        if (!needsReconnect)
        {
            reconnectAttempts = 0; // Resetear contador
            return;
        }
        
        // 3. Throttling: Mínimo 30 segundos entre intentos
        var timeSinceLastAttempt = (DateTime.Now - lastReconnectAttempt).TotalSeconds;
        if (timeSinceLastAttempt < 30)
        {
            Log($"⏳ Reconexión ignorada - último intento hace {timeSinceLastAttempt:F0}s");
            return;
        }
        
        // 4. Incrementar contador de intentos
        lastReconnectAttempt = DateTime.Now;
        reconnectAttempts++;
        
        // 5. Verificar límite máximo de intentos
        if (reconnectAttempts > MAX_RECONNECT_ATTEMPTS)
        {
            Log($"❌ Máximo de reintentos alcanzado ({MAX_RECONNECT_ATTEMPTS})");
            autoReconnect = false; // Deshabilitar auto-reconexión
            return;
        }
        
        // 6. Backoff exponencial: 2^attempt segundos
        int delaySeconds = (int)Math.Pow(2, reconnectAttempts - 1);
        Log($"⚠️ Conexión perdida ({reason}). Intento {reconnectAttempts}/{MAX_RECONNECT_ATTEMPTS} en {delaySeconds}s...");
        
        await Task.Delay(delaySeconds * 1000);
        
        // 7. Limpiar cliente anterior y reconectar
        client?.Dispose();
        client = null;
        
        await TryConnectWithRetry();
        
        // 8. Si conectó exitosamente, resetear contador
        if (client != null && client.State.HasFlag(SoulseekClientStates.Connected))
        {
            reconnectAttempts = 0;
            Log("✅ Reconexión exitosa");
        }
    }
    finally
    {
        isReconnecting = false;
    }
}
```

---

## 📊 Características del Sistema

### **1. Backoff Exponencial**

Los intentos de reconexión tienen delays crecientes:

| Intento | Delay |
|---------|-------|
| 1 | 1 segundo |
| 2 | 2 segundos |
| 3 | 4 segundos |
| 4 | 8 segundos |
| 5 | 16 segundos |
| 6 | 32 segundos |
| 7 | 64 segundos |
| 8 | 128 segundos |
| 9 | 256 segundos |
| 10 | 512 segundos |

**Fórmula:** `delay = 2^(attempt - 1)` segundos

**Beneficio:** Evita saturar el servidor con intentos frecuentes.

---

### **2. Throttling**

Mínimo 30 segundos entre intentos de reconexión:

```csharp
var timeSinceLastAttempt = (DateTime.Now - lastReconnectAttempt).TotalSeconds;
if (timeSinceLastAttempt < 30)
{
    Log($"⏳ Reconexión ignorada - último intento hace {timeSinceLastAttempt:F0}s");
    return;
}
```

**Beneficio:** Evita reconexiones simultáneas o muy frecuentes.

---

### **3. Límite de Intentos**

Máximo 10 intentos de reconexión (`MAX_RECONNECT_ATTEMPTS = 10`):

```csharp
if (reconnectAttempts > MAX_RECONNECT_ATTEMPTS)
{
    Log($"❌ Máximo de reintentos alcanzado ({MAX_RECONNECT_ATTEMPTS})");
    autoReconnect = false; // Deshabilitar auto-reconexión
    return;
}
```

**Beneficio:** Evita loops infinitos si hay un problema permanente.

---

### **4. Protección contra Reconexiones Simultáneas**

```csharp
lock (this)
{
    if (!autoReconnect || isReconnecting || isConnecting)
        return;
    isReconnecting = true;
}
```

**Beneficio:** Solo un intento de reconexión a la vez.

---

## 🎯 Flujo de Reconexión

```
┌─────────────────────────────────────┐
│ DESCONEXIÓN DETECTADA               │
└──────────────┬──────────────────────┘
               │
               ▼
┌─────────────────────────────────────┐
│ Evento Disconnected                 │
│ - Log: "Desconexión detectada"      │
│ - UI: Estado = "Desconectado"       │
│ - Esperar 5 segundos                │
└──────────────┬──────────────────────┘
               │
               ▼
┌─────────────────────────────────────┐
│ CheckAndReconnect()                 │
│ - Verificar si necesita reconexión  │
│ - Verificar throttling (30s mínimo) │
│ - Incrementar contador de intentos  │
└──────────────┬──────────────────────┘
               │
               ▼
┌─────────────────────────────────────┐
│ ¿Excede MAX_RECONNECT_ATTEMPTS?     │
└──────────────┬──────────────────────┘
               │
       ┌───────┴───────┐
       │               │
      Sí              No
       │               │
       ▼               ▼
┌──────────┐   ┌──────────────────────┐
│ Deshab.  │   │ Backoff Exponencial  │
│ Auto-    │   │ - Delay = 2^attempt  │
│ Reconex. │   │ - Esperar delay      │
└──────────┘   └──────────┬───────────┘
                          │
                          ▼
               ┌──────────────────────┐
               │ TryConnectWithRetry()│
               │ - Limpiar cliente    │
               │ - Crear nuevo cliente│
               │ - Conectar           │
               └──────────┬───────────┘
                          │
                  ┌───────┴───────┐
                  │               │
              Éxito           Fallo
                  │               │
                  ▼               ▼
         ┌────────────┐   ┌─────────────┐
         │ Resetear   │   │ Incrementar │
         │ contador   │   │ contador    │
         │ intentos=0 │   │ Reintentar  │
         └────────────┘   └─────────────┘
```

---

## 📝 Logs de Ejemplo

### **Desconexión Detectada:**
```
⚠️ Desconexión detectada: Connection closed by server
🔴 DESCONECTADO DE SOULSEEK - Razón: Connection closed by server
🔄 Iniciando auto-reconexión...
```

### **Intento de Reconexión:**
```
⚠️ Conexión perdida (Desconectado). Intento 1/10 en 1s...
🔄 Intentando reconectar a Soulseek...
✅ Conexión exitosa en puerto 54321
✅ CONECTADO A SOULSEEK - Usuario: carbar
✅ Reconexión exitosa
```

### **Múltiples Intentos:**
```
⚠️ Conexión perdida (Desconectado). Intento 1/10 en 1s...
❌ Intento 1 falló: Timeout
⚠️ Conexión perdida (Desconectado). Intento 2/10 en 2s...
❌ Intento 2 falló: Connection refused
⚠️ Conexión perdida (Desconectado). Intento 3/10 en 4s...
✅ Conexión exitosa en puerto 54322
✅ Reconexión exitosa
```

### **Máximo de Intentos Alcanzado:**
```
⚠️ Conexión perdida (Desconectado). Intento 10/10 en 512s...
❌ Intento 10 falló: Network unreachable
❌ Máximo de reintentos alcanzado (10). Deshabilitando auto-reconexión.
```

---

## ⚙️ Configuración

### **Variable de Control:**

```csharp
private bool autoReconnect = true; // Habilitado por defecto
```

### **Constantes:**

```csharp
private const int MAX_RECONNECT_ATTEMPTS = 10; // Máximo de intentos
```

### **Para Deshabilitar Auto-Reconexión:**

```csharp
autoReconnect = false;
```

---

## 🔍 Casos de Uso

### **Caso 1: Desconexión Temporal (Servidor Reinicia)**

```
1. Servidor se desconecta
2. Evento Disconnected dispara
3. Espera 5 segundos
4. Intento 1: Conecta exitosamente
5. Resetea contador
```

**Resultado:** ✅ Reconexión exitosa en ~6 segundos

---

### **Caso 2: Pérdida de Internet**

```
1. Internet se cae
2. ConnectionMonitor detecta pérdida
3. Evento ConnectionLost dispara
4. Espera a que Internet vuelva
5. Evento ConnectionRestored dispara
6. CheckAndReconnect() reconecta
```

**Resultado:** ✅ Reconexión automática cuando Internet vuelve

---

### **Caso 3: Problema Persistente**

```
1. Servidor no responde
2. Intento 1: Falla (1s delay)
3. Intento 2: Falla (2s delay)
4. Intento 3: Falla (4s delay)
...
10. Intento 10: Falla (512s delay)
11. Deshabilita auto-reconexión
```

**Resultado:** ⚠️ Se detiene después de 10 intentos (evita loops infinitos)

---

## ✅ Beneficios

1. **✅ Reconexión Automática**: Sin intervención del usuario
2. **✅ Detección Múltiple**: Evento + Timer + Monitor de red
3. **✅ Backoff Exponencial**: Evita saturar el servidor
4. **✅ Throttling**: Mínimo 30s entre intentos
5. **✅ Límite de Intentos**: Evita loops infinitos
6. **✅ Logs Detallados**: Visibilidad completa del proceso
7. **✅ UI Actualizada**: Estado siempre visible
8. **✅ Thread-Safe**: Protección contra reconexiones simultáneas

---

## 📁 Archivos Modificados

**`MainForm.cs`:**
- Líneas 2400-2418: Evento `Disconnected` del cliente
- Líneas 11019-11035: Timer de verificación periódica
- Líneas 11176-11350: Método `CheckAndReconnect()` (ya existía)

**`AUTO_RECONEXION.md`:** Documentación completa (este archivo)

---

## 🎯 Resultado Final

El sistema ahora:

- ✅ **Detecta desconexiones** automáticamente (3 mecanismos)
- ✅ **Reconecta automáticamente** sin intervención del usuario
- ✅ **Usa backoff exponencial** para evitar saturación
- ✅ **Tiene límite de intentos** para evitar loops infinitos
- ✅ **Logs detallados** de todo el proceso
- ✅ **UI actualizada** con estado actual

**¡La aplicación ahora se auto-reconecta automáticamente!** 🔄✨🚀

**Fecha de implementación:** 2025-01-19  
**Versión:** SlskDown v2.0 (Auto-Reconnect System)
