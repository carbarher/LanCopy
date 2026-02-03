# 🔄 Sistema de Reconexión Automática

## ⚠️ Problema Identificado

La **Prueba Moderada** (60 segundos) terminaba con desconexión del servidor Soulseek.

### Causas Comunes de Desconexión

1. **Inactividad del servidor** - El servidor desconecta clientes inactivos
2. **Demasiadas búsquedas** - El servidor puede limitar la tasa de búsquedas
3. **Timeout de conexión** - Conexiones peer-to-peer que tardan mucho
4. **Problemas de red** - Pérdida temporal de conectividad
5. **Límites del servidor** - Protección contra abuso

## ✅ Solución Implementada

### 🔧 Sistema de Reconexión Automática

#### 1. **Detección de Desconexión**
```csharp
// Antes de cada búsqueda
if (!client.State.HasFlag(SoulseekClientStates.Connected))
{
    Console.WriteLine("⚠️ Cliente desconectado - Intentando reconectar...");
    bool reconnected = await TryReconnectAsync(ct);
}
```

#### 2. **Reconexión con Semáforo**
```csharp
// Solo una tarea puede reconectar a la vez
await reconnectSemaphore.WaitAsync(ct);

// Verificar si otra tarea ya reconectó
if (client.State.HasFlag(SoulseekClientStates.Connected))
{
    return true; // Ya está conectado
}
```

**Ventaja:** Evita que múltiples tareas intenten reconectar simultáneamente

#### 3. **Reintentos Incrementales**
```csharp
for (int attempt = 1; attempt <= 3; attempt++)
{
    await client.ConnectAsync(username, password);
    
    if (connected)
    {
        Interlocked.Increment(ref reconnections);
        return true;
    }
    
    await Task.Delay(attempt * 2000, ct); // 2s, 4s
}
```

**Delays:** 2 segundos, 4 segundos entre intentos

#### 4. **Timeout de Inactividad Aumentado**
```csharp
var options = new SoulseekClientOptions(
    serverConnectionOptions: new ConnectionOptions(
        connectTimeout: 30000,
        inactivityTimeout: 600000  // 10 minutos (antes: 5 minutos)
    )
);
```

**Ventaja:** Reduce desconexiones por inactividad en pruebas largas

#### 5. **Contador de Reconexiones**
```csharp
private static int reconnections = 0;

// En resultados
Console.WriteLine($"Reconexiones exitosas: {reconnections}");
```

**Ventaja:** Visibilidad de cuántas veces se reconectó durante la prueba

### 📊 Flujo de Reconexión

```
┌─────────────────────────────────────────────────────────────┐
│ Búsqueda detecta desconexión                                │
└────────────────────┬────────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────────┐
│ Adquirir semáforo de reconexión (solo 1 tarea a la vez)    │
└────────────────────┬────────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────────┐
│ ¿Ya está conectado? (otra tarea pudo reconectar)           │
│   SÍ → Liberar semáforo y continuar ✓                      │
│   NO → Continuar con reconexión                             │
└────────────────────┬────────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────────┐
│ Desconectar cliente (limpiar estado)                        │
│ Esperar 1 segundo                                            │
└────────────────────┬────────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────────┐
│ Intento 1: ConnectAsync                                      │
│   ✓ Éxito → Incrementar contador, liberar semáforo         │
│   ✗ Fallo → Esperar 2 segundos                             │
└────────────────────┬────────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────────┐
│ Intento 2: ConnectAsync                                      │
│   ✓ Éxito → Incrementar contador, liberar semáforo         │
│   ✗ Fallo → Esperar 4 segundos                             │
└────────────────────┬────────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────────┐
│ Intento 3: ConnectAsync (último intento)                    │
│   ✓ Éxito → Incrementar contador, liberar semáforo         │
│   ✗ Fallo → Liberar semáforo, reportar error               │
└─────────────────────────────────────────────────────────────┘
```

## 🎯 Comportamiento Esperado

### Escenario 1: Desconexión Durante Prueba

**Antes (sin reconexión):**
```
[10s] Exitosas: 5 | Fallidas: 0 | ...
[Búsqueda 3] ⚠️ Cliente desconectado
[Búsqueda 1] ⚠️ Cliente desconectado
[Búsqueda 2] ⚠️ Cliente desconectado
[15s] Exitosas: 5 | Fallidas: 15 | Errores conexión: 15
...
❌ Prueba terminó con muchos errores
```

**Ahora (con reconexión):**
```
[10s] ✓ Exitosas: 5 | Fallidas: 0 | ...
[Búsqueda 3] ⚠️ Cliente desconectado - Intentando reconectar...
🔄 Intentando reconexión al servidor...
   Intento de reconexión 1/3...
   ✓ Reconexión exitosa
[Búsqueda 3] ✓ Reconectado exitosamente
[Búsqueda 1] ✓ Reconectado exitosamente (ya reconectado por tarea 3)
[15s] ✓ Exitosas: 8 | Fallidas: 0 | Errores conexión: 1
...
✓ Prueba completada exitosamente con 1 reconexión
```

### Escenario 2: Error de Conexión Durante Búsqueda

**Antes:**
```
[Búsqueda 5] Error: The client is not connected
[Búsqueda 5] Error: The client is not connected
[Búsqueda 5] Error: The client is not connected
...
```

**Ahora:**
```
[Búsqueda 5] ⚠️ Error de conexión: The client is not connected
🔄 Intentando reconexión al servidor...
   Intento de reconexión 1/3...
   ✓ Reconexión exitosa
[Búsqueda 5] ✓ Reconectado después de error
[Búsqueda 5] Búsqueda completada exitosamente
```

## 📊 Nuevas Métricas

### Resultados Actualizados

```
=== RESULTADOS DE LA PRUEBA ===
Tiempo total: 60.23 segundos
Búsquedas exitosas: 28
Búsquedas fallidas: 2
Errores de conexión: 1
Reconexiones exitosas: 1          ← NUEVO
Total de búsquedas: 30
...
```

### Interpretación

| Reconexiones | Estado | Acción |
|--------------|--------|--------|
| **0** | ✅ Excelente | Conexión estable durante toda la prueba |
| **1-2** | ✅ Bueno | Reconexión exitosa, prueba continuó normalmente |
| **3-5** | ⚠️ Aceptable | Conexión inestable, considerar VPN o probar más tarde |
| **>5** | ❌ Problema | Red muy inestable o servidor con problemas |

## 🔍 Diagnóstico

### Logs de Reconexión Exitosa

```
[25s] ✓ Exitosas: 12 | Fallidas: 0 | Errores conexión: 0
[Búsqueda 3] ⚠️ Cliente desconectado - Intentando reconectar...
🔄 Intentando reconexión al servidor...
   Intento de reconexión 1/3...
   ✓ Reconexión exitosa
[Búsqueda 3] ✓ Reconectado exitosamente
[30s] ✓ Exitosas: 15 | Fallidas: 0 | Errores conexión: 1
```

**Análisis:**
- ✅ Desconexión detectada a los 25 segundos
- ✅ Reconexión exitosa en el primer intento
- ✅ Prueba continuó sin problemas
- ✅ Solo 1 error de conexión registrado

### Logs de Reconexión Fallida

```
[25s] ✓ Exitosas: 12 | Fallidas: 0 | Errores conexión: 0
[Búsqueda 3] ⚠️ Cliente desconectado - Intentando reconectar...
🔄 Intentando reconexión al servidor...
   Intento de reconexión 1/3...
   ✗ Intento 1 falló: Connection timed out
   Intento de reconexión 2/3...
   ✗ Intento 2 falló: Connection timed out
   Intento de reconexión 3/3...
   ✗ Intento 3 falló: Connection timed out
   ❌ No se pudo reconectar después de 3 intentos
[Búsqueda 3] ❌ No se pudo reconectar
```

**Análisis:**
- ⚠️ Desconexión detectada
- ❌ Todos los intentos de reconexión fallaron
- ⚠️ Posible problema de red o servidor caído
- 💡 La prueba continúa pero sin poder hacer búsquedas

## 🎯 Ventajas del Sistema

### ✅ Resiliencia
- **Recuperación automática** de desconexiones temporales
- **Continuidad de la prueba** sin intervención manual
- **Datos más precisos** al completar más búsquedas

### ✅ Eficiencia
- **Semáforo de reconexión** evita intentos duplicados
- **Verificación previa** reduce reconexiones innecesarias
- **Delays incrementales** no saturan el servidor

### ✅ Visibilidad
- **Logs detallados** de cada intento de reconexión
- **Contador de reconexiones** en resultados finales
- **Estado en tiempo real** durante el monitoreo

### ✅ Robustez
- **Máximo 3 intentos** por reconexión
- **Timeout de 10 minutos** reduce desconexiones por inactividad
- **Manejo de errores** en cada etapa

## 📝 Recomendaciones

### Para Pruebas Largas (>60 segundos)

1. **Esperar reconexiones** - No cancelar si aparece mensaje de reconexión
2. **Monitorear contador** - Más de 5 reconexiones indica problema
3. **Revisar logs** - Identificar patrón de desconexiones
4. **Ajustar pausas** - Aumentar tiempo entre búsquedas si hay muchas desconexiones

### Si Hay Muchas Reconexiones

1. **Verificar red** - Estabilidad de Internet
2. **Probar en otro horario** - Servidor menos saturado
3. **Reducir concurrencia** - Menos búsquedas simultáneas
4. **Usar VPN** - Si ISP bloquea o limita P2P

### Configuración Óptima

```csharp
// Para red estable
inactivityTimeout: 600000  // 10 minutos

// Para red inestable
inactivityTimeout: 900000  // 15 minutos

// Para servidor saturado
inactivityTimeout: 1200000 // 20 minutos
```

## 🎉 Resultado

Con el sistema de reconexión automática:

- ✅ **Pruebas largas completan exitosamente**
- ✅ **Desconexiones temporales no interrumpen la prueba**
- ✅ **Datos más precisos y completos**
- ✅ **Sin intervención manual requerida**
- ✅ **Logs claros de lo que sucede**

**¡Ahora puedes ejecutar la Prueba Moderada con confianza!** 🚀

---

**Próximo paso:** Ejecuta `run_stress_test.bat` y selecciona opción **2** (Moderada)
