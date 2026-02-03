# Fix: Reconexión Infinita Bloqueada

## 🐛 Problemas Identificados

### Problema 1: Flag isReconnecting bloqueado
La aplicación quedaba atascada en un estado de reconexión permanente (`isReconnecting=True`), bloqueando todos los intentos de reconexión posteriores.

### Problema 2: Cooldown de 600s en primer intento
El sistema aplicaba un cooldown de 600 segundos (10 minutos) **inmediatamente en el primer intento de conexión** debido a datos históricos de sesiones anteriores, impidiendo la conexión inicial.

### Problema 3: Flag isConnecting bloqueado durante esperas largas ⚠️ **CRÍTICO**
El flag `isConnecting` se establecía en `true` al inicio de `ConnectToSoulseek()` pero no se reseteaba durante las esperas largas (600s, 15min), bloqueando `CheckAndReconnect()` indefinidamente con el mensaje "Reconexión ignorada - isConnecting=True".

### Síntomas - Problema 1 y 3
```
[19:47:41] ⚠️ Reconexión ignorada - autoReconnect=True, isReconnecting=True, isConnecting=True
[19:48:41] ⚠️ Reconexión ignorada - autoReconnect=True, isReconnecting=True, isConnecting=True
[19:49:41] ⚠️ Reconexión ignorada - autoReconnect=True, isReconnecting=True, isConnecting=True
... (indefinidamente durante 600 segundos o más)
```

### Causa Raíz - Problema 1

1. **Flag establecido dentro de lock**: El flag `isReconnecting` se establecía en `true` dentro de un bloque `lock(this)` (línea 15929)

2. **Returns tempranos sin reset**: Había 4 `return` tempranos que salían del método sin resetear el flag:
   - Circuit Breaker abierto (línea 15945)
   - Máximo de intentos alcanzado (línea 15960)
   - Cliente ya conectado (línea 16009)
   - **Cooldown activo (línea 16019)** ← Principal causa

3. **Finally no alcanzado**: El bloque `finally` que reseteaba el flag (línea 16301) nunca se ejecutaba en estos casos

4. **Race conditions**: Los resets estaban fuera del `lock`, permitiendo condiciones de carrera

### Causa Raíz - Problema 2

El sistema carga cooldowns históricos de sesiones anteriores (línea 1492):
```csharp
cooldownHistorySeconds.Value.AddRange(data.cooldowns.TakeLast(50));
Log($"⏱️ Cooldowns históricos cargados: {cooldownHistorySeconds.Value.Count} muestras");
```

Luego aplica el cooldown recomendado **inmediatamente en el primer intento** (línea 16042):
```csharp
var recommendedCooldown = GetRecommendedCooldownSeconds();  // Retorna 600s desde historial
if (recommendedCooldown > 0)
{
    backoffDelay = TimeSpan.FromSeconds(recommendedCooldown);  // ❌ 600s en 1er intento
}
```

### Causa Raíz - Problema 3

El método `ConnectToSoulseek()` tiene un loop con 5 intentos. Cuando hay timeout:

1. **Flag establecido al inicio** (línea 4533):
   ```csharp
   isConnecting = true;  // Se establece al inicio del método
   ```

2. **Esperas largas sin resetear flag**:
   - Espera de 600s por timeout (línea 5117)
   - Espera de 15 minutos por ventana de bloqueo (línea 5087)
   - Espera por fallo TCP (línea 4832)

3. **Finally solo al final**: El `finally` que resetea `isConnecting` (línea 5154) solo se ejecuta cuando terminan **todos los intentos**, no entre intentos

4. **CheckAndReconnect() bloqueado**: Durante las esperas, `CheckAndReconnect()` se ejecuta cada minuto pero ve `isConnecting=True` y se ignora

## ✅ Solución Implementada

### Cambios Aplicados

**1. Circuit Breaker abierto** (línea 15945):
```csharp
if (connectionCircuitBreaker != null && !connectionCircuitBreaker.AllowRequest())
{
    var timeUntilRetry = connectionCircuitBreaker.GetTimeUntilRetry();
    Log($"⛔ Circuit Breaker ABIERTO - Esperando {timeUntilRetry.TotalSeconds:F0}s...");
    lock (this) { isReconnecting = false; }  // ✅ AGREGADO
    return;
}
```

**2. Máximo de intentos alcanzado** (línea 15960):
```csharp
if (reconnectAttempts >= MAX_RECONNECT_ATTEMPTS)
{
    Log($"❌ Máximo de intentos alcanzado ({MAX_RECONNECT_ATTEMPTS})...");
    autoReconnect = false;
    SafeBeginInvoke(() => { /* ... */ });
    lock (this) { isReconnecting = false; }  // ✅ AGREGADO
    return;
}
```

**3. Cliente ya conectado** (línea 16009):
```csharp
// Resetear contador y backoff si está conectado
reconnectAttempts = 0;
if (reconnectBackoff != null)
    reconnectBackoff.Reset();
if (connectionCircuitBreaker != null)
    connectionCircuitBreaker.Reset();
lock (this) { isReconnecting = false; }  // ✅ AGREGADO
return;
```

**4. Cooldown activo** (línea 16019) - **CRÍTICO**:
```csharp
if (now < nextAllowedReconnect)
{
    var wait = nextAllowedReconnect - now;
    Log($"⏳ Reconexión aplazada {wait.TotalSeconds:F0}s - respetando ventana de backoff");
    lock (this) { isReconnecting = false; }  // ✅ AGREGADO
    return;
}
```

**5. Finally protegido** (línea 16301):
```csharp
finally
{
    lock (this)  // ✅ AGREGADO
    {
        isReconnecting = false;
        Log($"[CheckAndReconnect] Finally: isReconnecting establecido en false");
    }
}
```

**6. Cooldown recomendado solo después del 2do intento** (línea 16042):
```csharp
// Respetar cooldown por posible ban (solo después del 2do intento)
double recommendedCooldown = 0;
if (reconnectAttempts > 2)  // ✅ AGREGADO: Solo después del 2do intento
{
    recommendedCooldown = GetRecommendedCooldownSeconds();
    if (recommendedCooldown > 0)
    {
        var recDelay = TimeSpan.FromSeconds(recommendedCooldown);
        if (recDelay > backoffDelay)
        {
            backoffDelay = recDelay;
            Log($"⚠️ Usando cooldown recomendado: {backoffDelay.TotalSeconds:F0}s (posible ban detectado)");
        }
    }
}
```

**7. Reset de isConnecting antes de esperas largas** (líneas 5110-5124, 4825-4839, 5079-5094):
```csharp
// Antes de espera de 600s por timeout
lock (this)
{
    isConnecting = false;
    Log($"[DEBUG] isConnecting reseteado a false antes de espera de {totalDelay.TotalSeconds:F0}s");
}

await DelayWithCancellation(totalDelay, appCts?.Token ?? CancellationToken.None);

// Después de espera, restablecer para siguiente intento
lock (this)
{
    isConnecting = true;
    Log($"[DEBUG] isConnecting establecido a true para siguiente intento");
}
```

Se aplica en 3 lugares:
- Espera por timeout (600s)
- Espera por fallo TCP
- Espera por ventana de bloqueo (15 minutos)

## 🎯 Resultado Esperado

### Antes del Fix - Problema 1
```
[19:00:39] ⏳ Reconexión aplazada 600s - respetando ventana de backoff
[19:00:39] ℹ️ [DEBUG] CheckAndReconnect() llamado
[19:00:39] ⚠️ Reconexión ignorada - isReconnecting=True  ← BLOQUEADO
[19:01:39] ⚠️ Reconexión ignorada - isReconnecting=True  ← BLOQUEADO
... (indefinidamente)
```

### Antes del Fix - Problema 2
```
[19:26:46] 🔄 Intento de reconexión #1/10 - Razón: Cliente nulo
[19:26:46] ⏱️ Backoff exponencial: 5,1s (con jitter)
[19:26:46] ⚠️ Usando cooldown recomendado: 600s (posible ban)  ← ❌ EN 1ER INTENTO
[19:26:46] ⚠️ Conexión perdida (Cliente nulo). Reintentando en 600,0s...
```

### Antes del Fix - Problema 3
```
[19:47:03] ⏳ Esperando 600s antes del siguiente intento...
[19:47:41] ⚠️ Reconexión ignorada - isConnecting=True  ← BLOQUEADO
[19:48:41] ⚠️ Reconexión ignorada - isConnecting=True  ← BLOQUEADO
[19:49:41] ⚠️ Reconexión ignorada - isConnecting=True  ← BLOQUEADO
... (durante toda la espera de 600s)
```

### Después del Fix
```
[19:26:46] 🔄 Intento de reconexión #1/10 - Razón: Cliente nulo
[19:26:46] ⏱️ Backoff exponencial: 5,1s (con jitter)
[19:26:46] ⚠️ Conexión perdida (Cliente nulo). Reintentando en 5,1s...  ← ✅ BACKOFF NORMAL
[19:26:51] 🔄 Conectando a Soulseek...
[19:26:52] ✅ CONEXIÓN EXITOSA
```

```
[19:47:03] ⏳ Esperando 600s antes del siguiente intento...
[19:47:03] [DEBUG] isConnecting reseteado a false antes de espera de 600s  ← ✅ RESETEADO
[19:47:41] [DEBUG] CheckAndReconnect() llamado  ← ✅ YA NO SE BLOQUEA
[19:47:41] [CheckAndReconnect] Finally: isReconnecting establecido en false
[19:57:03] [DEBUG] isConnecting establecido a true para siguiente intento
[19:57:03] 🔄 Conectando a Soulseek...
```

## 📋 Checklist de Verificación

### Problema 1: Flag isReconnecting
- [x] `isReconnecting` se resetea en Circuit Breaker abierto
- [x] `isReconnecting` se resetea en máximo de intentos
- [x] `isReconnecting` se resetea cuando cliente ya está conectado
- [x] `isReconnecting` se resetea en cooldown activo (CRÍTICO)
- [x] `isReconnecting` se resetea en bloque finally
- [x] Todos los resets están protegidos con `lock(this)`
- [x] No hay race conditions

### Problema 2: Cooldown en primer intento
- [x] Cooldown recomendado solo se aplica después del 2do intento
- [x] Primer intento usa backoff exponencial normal (5-10s)
- [x] Variable `recommendedCooldown` declarada fuera del if para scope correcto

### Problema 3: Flag isConnecting bloqueado
- [x] `isConnecting` se resetea antes de espera de 600s por timeout
- [x] `isConnecting` se resetea antes de espera por fallo TCP
- [x] `isConnecting` se resetea antes de espera de 15 min por bloqueo
- [x] `isConnecting` se restablece después de cada espera para siguiente intento
- [x] Todos los resets están protegidos con `lock(this)`
- [x] `CheckAndReconnect()` puede ejecutarse durante esperas largas

### General
- [x] Compilación exitosa sin errores

## 🧪 Cómo Probar

1. **Ejecutar**: `COMPILAR_Y_PROBAR_FIX_RECONEXION.bat`

2. **Escenario de prueba - Primer intento**:
   - Iniciar la aplicación (debería conectar automáticamente)
   - Verificar que conecta en 5-10 segundos (NO en 600s)

3. **Escenario de prueba - Reconexión**:
   - Dejar que la aplicación se conecte
   - Esperar desconexión o desconectar manualmente
   - Observar que reintenta con backoff normal (5-10s)
   - Solo después del 3er intento fallido aplicará cooldown de 600s

4. **Logs esperados - Primer intento**:
   ```
   🔄 Intento de reconexión #1/10 - Razón: Cliente nulo
   ⏱️ Backoff exponencial: 5,1s (con jitter)
   ⚠️ Conexión perdida (Cliente nulo). Reintentando en 5,1s...
   🔄 Conectando a Soulseek...
   ✅ CONEXIÓN EXITOSA
   ```

5. **Logs esperados - Después del 3er intento**:
   ```
   🔄 Intento de reconexión #3/10 - Razón: Cliente nulo
   ⏱️ Backoff exponencial: 15,2s (con jitter)
   ⚠️ Usando cooldown recomendado: 600s (posible ban detectado)
   ⚠️ Conexión perdida (Cliente nulo). Reintentando en 600,0s...
   ```

6. **Escenario de prueba - Timeout con espera larga**:
   - Forzar timeout (desconectar red temporalmente)
   - Observar que durante la espera de 600s:
     * `isConnecting` se resetea a `false`
     * `CheckAndReconnect()` puede ejecutarse
     * NO aparece "Reconexión ignorada - isConnecting=True"

7. **NO debe aparecer**:
   ```
   ⚠️ Reconexión ignorada - isReconnecting=True
   ⚠️ Reconexión ignorada - isConnecting=True  ← Durante esperas largas
   ⚠️ Usando cooldown recomendado: 600s (posible ban)  ← En 1er o 2do intento
   ```

## 🔍 Archivos Modificados

- `MainForm.cs`:
  - **Problema 1 - isReconnecting**:
    * Línea 15945: Reset en Circuit Breaker con lock
    * Línea 15960: Reset en máximo intentos con lock
    * Línea 16009: Reset cuando conectado con lock
    * Línea 16019: Reset en cooldown con lock (CRÍTICO)
    * Línea 16301: Reset en finally con lock
  
  - **Problema 2 - Cooldown en 1er intento**:
    * Línea 16042: Cooldown recomendado solo después del 2do intento (CRÍTICO)
  
  - **Problema 3 - isConnecting bloqueado** (CRÍTICO):
    * Líneas 5110-5124: Reset antes/después de espera por timeout (600s)
    * Líneas 4825-4839: Reset antes/después de espera por fallo TCP
    * Líneas 5079-5094: Reset antes/después de espera por bloqueo (15 min)

## 📊 Impacto

### Problema 1: Flag isReconnecting
- **Severidad**: CRÍTICA - Bloqueaba reconexión permanentemente
- **Frecuencia**: 100% cuando se activaba cooldown de 600s
- **Usuarios afectados**: Todos los que experimentaban desconexiones
- **Tiempo de bloqueo**: Indefinido (hasta reinicio de aplicación)

### Problema 2: Cooldown en primer intento
- **Severidad**: CRÍTICA - Impedía conexión inicial
- **Frecuencia**: 100% si había datos históricos de sesiones anteriores
- **Usuarios afectados**: Todos los usuarios al iniciar la aplicación
- **Tiempo de bloqueo**: 600 segundos (10 minutos) en cada inicio

### Problema 3: Flag isConnecting bloqueado
- **Severidad**: CRÍTICA - Bloqueaba CheckAndReconnect() durante esperas largas
- **Frecuencia**: 100% cuando había timeouts con espera de 600s o 15min
- **Usuarios afectados**: Todos los que experimentaban timeouts
- **Tiempo de bloqueo**: Durante toda la espera (600s o 15min)
- **Impacto**: Sistema de reconexión automática completamente inoperativo

## ✅ Estado

**RESUELTO** - Los 3 problemas críticos solucionados, compilación exitosa, listo para pruebas
