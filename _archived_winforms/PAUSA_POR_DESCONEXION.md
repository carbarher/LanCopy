# Sistema de Pausa Automática por Desconexión

## 📋 Descripción

Sistema que **pausa automáticamente** las búsquedas y purgas cuando se pierde la conexión a Soulseek, y las **reanuda automáticamente** al reconectar.

---

## 🎯 Objetivo

Evitar que las búsquedas y purgas fallen masivamente cuando se pierde la conexión, permitiendo que continúen automáticamente al reconectar sin perder el progreso.

---

## ✅ Funcionalidad Implementada

### **1. Detección de Desconexión** (líneas 2425-2436)

Cuando se detecta una desconexión:

```csharp
client.Disconnected += async (sender, args) =>
{
    // ... código de desconexión ...
    
    // Pausar búsquedas y purgas automáticas
    if (autoSearchRunning)
    {
        autoSearchPausedByDisconnection = true;
        AutoLog("⏸️ Búsqueda automática PAUSADA por desconexión");
    }
    
    if (autoPurgeRunning)
    {
        autoPurgePausedByDisconnection = true;
        AutoLog("⏸️ Purga automática PAUSADA por desconexión");
    }
    
    // ... auto-reconexión ...
};
```

**Acciones:**
- ✅ Marca búsqueda como pausada si está activa
- ✅ Marca purga como pausada si está activa
- ✅ Registra en log la pausa
- ✅ NO cancela las tareas, solo las pausa

---

### **2. Espera Durante Pausa - Búsqueda** (líneas 8455-8459)

En el loop de búsqueda automática:

```csharp
while (consecutiveEmptySearches < MAX_EMPTY_SEARCHES && autoSearchRunning && !cancellationToken.IsCancellationRequested)
{
    searchIteration++;
    
    // Esperar mientras esté pausado por desconexión
    while (autoSearchPausedByDisconnection && autoSearchRunning)
    {
        await Task.Delay(1000, cancellationToken);
    }
    
    // Verificar cancelación antes de buscar
    if (!autoSearchRunning || cancellationToken.IsCancellationRequested)
    {
        return;
    }
    
    // ... realizar búsqueda ...
}
```

**Comportamiento:**
- ⏸️ Espera en loop de 1 segundo mientras esté pausado
- ✅ Continúa automáticamente cuando se reanuda
- ❌ Sale si se cancela la búsqueda

---

### **3. Espera Durante Pausa - Purga** (líneas 18047-18051)

En el loop de purga:

```csharp
await semaphore.WaitAsync(cancellationToken);
try
{
    // Esperar mientras esté pausado por desconexión
    while (autoPurgePausedByDisconnection && autoPurgeRunning)
    {
        await Task.Delay(1000, cancellationToken);
    }
    
    if (!autoPurgeRunning || cancellationToken.IsCancellationRequested)
    {
        return;
    }

    // ... realizar búsqueda de purga ...
}
```

**Comportamiento:**
- ⏸️ Espera en loop de 1 segundo mientras esté pausado
- ✅ Continúa automáticamente cuando se reanuda
- ❌ Sale si se cancela la purga

---

### **4. Reanudación al Reconectar** (líneas 2475-2486)

Cuando se reconecta exitosamente:

```csharp
connected = true;

// Reanudar búsquedas y purgas que fueron pausadas por desconexión
if (autoSearchPausedByDisconnection)
{
    autoSearchPausedByDisconnection = false;
    AutoLog("▶️ Reanudando búsqueda automática...");
}

if (autoPurgePausedByDisconnection)
{
    autoPurgePausedByDisconnection = false;
    AutoLog("▶️ Reanudando purga automática...");
}
```

**Acciones:**
- ✅ Desmarca flag de pausa
- ✅ Registra en log la reanudación
- ✅ Las tareas continúan automáticamente desde donde estaban

---

## 📊 Flujo Completo

### **Escenario: Búsqueda Automática con Desconexión**

```
1. Usuario inicia búsqueda automática
   ↓
2. Búsqueda procesando autores (50/1000)
   ↓
3. 🔴 DESCONEXIÓN DETECTADA
   ↓
4. autoSearchPausedByDisconnection = true
   ↓
5. Log: "⏸️ Búsqueda automática PAUSADA por desconexión"
   ↓
6. Loop de búsqueda entra en espera:
   while (autoSearchPausedByDisconnection)
       await Task.Delay(1000)
   ↓
7. Auto-reconexión intenta conectar...
   ↓
8. ✅ RECONEXIÓN EXITOSA
   ↓
9. autoSearchPausedByDisconnection = false
   ↓
10. Log: "▶️ Reanudando búsqueda automática..."
    ↓
11. Loop de búsqueda sale de espera
    ↓
12. Búsqueda continúa desde autor 51/1000
```

---

### **Escenario: Purga con Desconexión**

```
1. Usuario inicia purga
   ↓
2. Purga verificando autores (200/934)
   ↓
3. 🔴 DESCONEXIÓN DETECTADA
   ↓
4. autoPurgePausedByDisconnection = true
   ↓
5. Log: "⏸️ Purga automática PAUSADA por desconexión"
   ↓
6. Threads de purga entran en espera:
   while (autoPurgePausedByDisconnection)
       await Task.Delay(1000)
   ↓
7. Auto-reconexión intenta conectar...
   ↓
8. ✅ RECONEXIÓN EXITOSA
   ↓
9. autoPurgePausedByDisconnection = false
   ↓
10. Log: "▶️ Reanudando purga automática..."
    ↓
11. Threads de purga salen de espera
    ↓
12. Purga continúa desde autor 201/934
```

---

## 🔍 Variables de Control

### **Nuevas Variables** (líneas 8201-8202)

```csharp
private bool autoSearchPausedByDisconnection = false;
private bool autoPurgePausedByDisconnection = false;
```

**Propósito:**
- Rastrear si búsqueda/purga fueron pausadas por desconexión
- Distinguir entre pausa por desconexión vs cancelación manual
- Permitir reanudación automática

---

## 📈 Ventajas del Sistema

### **1. Sin Pérdida de Progreso**

**Antes:**
```
Búsqueda procesando 500/1000 autores
🔴 Desconexión
❌ Búsqueda falla completamente
❌ Usuario debe reiniciar desde cero
```

**Después:**
```
Búsqueda procesando 500/1000 autores
🔴 Desconexión
⏸️ Búsqueda pausada
✅ Reconexión
▶️ Búsqueda continúa desde autor 501
```

---

### **2. Sin Errores Masivos**

**Antes:**
```
🔴 Desconexión
❌ Error: No connection
❌ Error: No connection
❌ Error: No connection
... (cientos de errores)
```

**Después:**
```
🔴 Desconexión
⏸️ Búsqueda pausada
... (esperando reconexión)
✅ Reconexión
▶️ Continúa sin errores
```

---

### **3. Experiencia de Usuario Mejorada**

| Aspecto | Antes | Después |
|---------|-------|---------|
| **Progreso** | ❌ Perdido | ✅ Conservado |
| **Errores** | ❌ Cientos | ✅ Ninguno |
| **Intervención** | ❌ Manual | ✅ Automática |
| **Tiempo perdido** | ❌ Todo | ✅ Ninguno |

---

## 🎯 Casos de Uso

### **Caso 1: Conexión Inestable**

Usuario con conexión que se cae cada 10 minutos:

**Antes:**
- Búsqueda falla cada 10 minutos
- Usuario debe reiniciar manualmente
- Nunca completa búsquedas largas

**Después:**
- Búsqueda se pausa automáticamente
- Se reanuda al reconectar
- Completa búsquedas largas sin problemas

---

### **Caso 2: Mantenimiento del Servidor**

Servidor de Soulseek se reinicia:

**Antes:**
- Todas las búsquedas activas fallan
- Usuario pierde progreso de horas
- Debe reiniciar todo manualmente

**Después:**
- Búsquedas se pausan automáticamente
- Se reanudan cuando servidor vuelve
- Cero pérdida de progreso

---

### **Caso 3: Cambio de Red**

Usuario cambia de WiFi a datos móviles:

**Antes:**
- Desconexión causa fallo total
- Búsquedas deben reiniciarse
- Progreso perdido

**Después:**
- Pausa durante cambio de red
- Reanuda al reconectar
- Progreso intacto

---

## 🔧 Comportamiento Detallado

### **Durante la Pausa:**

1. ✅ **Búsquedas activas:** Esperan en loop de 1 segundo
2. ✅ **Purgas activas:** Esperan en loop de 1 segundo
3. ✅ **UI:** Muestra "Desconectado" (rojo)
4. ✅ **Auto-reconexión:** Se activa automáticamente
5. ✅ **Logs:** Registran pausa claramente

---

### **Durante la Reanudación:**

1. ✅ **Flags de pausa:** Se desmarcan
2. ✅ **Loops de espera:** Salen automáticamente
3. ✅ **Búsquedas:** Continúan desde donde estaban
4. ✅ **Purgas:** Continúan desde donde estaban
5. ✅ **UI:** Muestra "Conectado" (verde)
6. ✅ **Logs:** Registran reanudación

---

## 📝 Logs Esperados

### **Secuencia Completa:**

```
[16:30:00] 🔍 Búsqueda automática iniciada: 1000 autores
[16:30:05] ✅ Autor1: 50 archivos
[16:30:10] ✅ Autor2: 30 archivos
[16:30:15] 🔴 DESCONECTADO DE SOULSEEK - Razón: Connection lost
[16:30:15] ⏸️ Búsqueda automática PAUSADA por desconexión
[16:30:15] 🔄 Iniciando auto-reconexión...
[16:30:20] Intento 1/5 - Puerto: 54321
[16:30:22] ✅ CONECTADO A SOULSEEK - Usuario: carbar
[16:30:22] ▶️ Reanudando búsqueda automática...
[16:30:23] ✅ Autor3: 40 archivos
[16:30:28] ✅ Autor4: 25 archivos
```

---

## ⚠️ Consideraciones

### **1. Cancelación Manual**

Si el usuario cancela manualmente durante la pausa:
- ✅ La cancelación tiene prioridad
- ✅ No se reanuda al reconectar
- ✅ Flags de pausa se limpian

---

### **2. Múltiples Desconexiones**

Si se desconecta varias veces:
- ✅ Cada desconexión pausa
- ✅ Cada reconexión reanuda
- ✅ Progreso se conserva siempre

---

### **3. Timeout del CancellationToken**

El `Task.Delay(1000, cancellationToken)` puede lanzar `OperationCanceledException`:
- ✅ Se captura en el try-catch existente
- ✅ Se maneja como cancelación normal
- ✅ No causa problemas

---

## 🔍 Búsquedas Manuales

**Nota:** Las búsquedas manuales (no automáticas) NO se pausan porque:

1. Son de corta duración (segundos)
2. Fallan rápidamente si no hay conexión
3. Usuario puede reiniciarlas fácilmente
4. No tienen progreso que conservar

**Comportamiento:**
- ❌ Sin conexión → Falla inmediatamente
- ✅ Usuario ve error claro
- ✅ Usuario puede reintentar manualmente

---

## 📁 Archivos Modificados

**`MainForm.cs`:**
- Líneas 8201-8202: Variables de control de pausa
- Líneas 2425-2436: Detección y pausa en desconexión
- Líneas 2475-2486: Reanudación al reconectar
- Líneas 8455-8459: Espera durante pausa (búsqueda)
- Líneas 18047-18051: Espera durante pausa (purga)

**`PAUSA_POR_DESCONEXION.md`:** Este documento

---

## ✅ Resultado Final

### **Sistema Robusto:**

1. ✅ **Pausa automática** al desconectar
2. ✅ **Reanudación automática** al reconectar
3. ✅ **Sin pérdida de progreso**
4. ✅ **Sin errores masivos**
5. ✅ **Experiencia fluida** para el usuario
6. ✅ **Logs claros** de pausa/reanudación

### **Beneficios:**

- ✅ Búsquedas largas completables con conexión inestable
- ✅ Purgas completas sin reiniciar
- ✅ Cero intervención manual requerida
- ✅ Manejo elegante de desconexiones

---

**¡Sistema de pausa por desconexión implementado y funcionando!** ✅⏸️▶️

**Fecha de implementación:** 2025-01-19  
**Versión:** SlskDown v2.1 (Pause & Resume)
